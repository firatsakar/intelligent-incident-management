import { HubConnectionBuilder, HubConnectionState, type HubConnection } from '@microsoft/signalr'
import type { QueryClient } from '@tanstack/react-query'

import type {
  CountsByKey,
  ErrorSignature,
  EvidenceWindow,
  Incident,
  IncidentDayBucket,
  IncidentPriority,
  IncidentStats,
  IncidentStatus,
  IngestionTick,
  Integration,
  NotificationDelivery,
  NotificationStats,
  PagedResult,
  Signal,
  SignalPage,
  TelemetrySource,
} from '@/types/api'

// Where a pushed message becomes a cache write.
//
// Payloads, not ids. A socket exists to remove HTTP round trips; pushing an id and making the
// client fetch would cost one request per change, which is worse than polling — polling at least
// coalesces. The hub sends the same DTO the endpoint returns, so there is no second contract.
//
// Three ways a message is allowed to reach a screen, in order of preference:
//
//   1. Write the payload into the cache. Used wherever the DTO *is* the thing on screen.
//   2. Apply a delta to an aggregate — but only where the delta has exactly one meaning, and
//      only to the counters the pushed record can actually move.
//   3. Invalidate, coalesced. The honest answer wherever a push is ambiguous: a median, an
//      argmax, a server-side ordering, or a transition whose previous state the client never saw.
//
// A hand-maintained delta that drifts from the server is worse than a refetch, so each aggregate
// below says which of the three it gets and why.

export interface HubDefinition {
  name: string
  url: string
  /** Query keys to invalidate after a reconnect, to close the gap of missed messages. */
  recoverKeys: unknown[][]
  handlers: (client: QueryClient) => Record<string, (payload: never) => void>
}

/**
 * Ingestion ticks, newest first, kept in the query cache because that is already the channel the
 * hubs write through — one mechanism for pushed state rather than two.
 *
 * Nothing fetches this key. It is read with `queryFn: skipToken`, so a screen subscribes to it
 * without ever asking the network for it.
 */
export const ingestionKey = ['ingestion'] as const

/** Enough to survive a screen being left open through a burst; older ticks answer nothing. */
const maxTicks = 100

/**
 * How long a coalesced invalidate waits, and the longest it may ever be held back.
 *
 * The wait is what turns a thirty-incident storm into one refetch. The ceiling is what stops a
 * storm that never pauses from holding the timer open indefinitely: the screens fed only by an
 * invalidate have no delta behind them, so without it they would sit frozen through exactly the
 * minutes an operator is watching them.
 */
const settleWait = 2000
const settleCeiling = 10_000

/**
 * One trailing invalidate per key, however many pushes arrive in the meantime.
 *
 * Built per connection rather than per module so the timers die with the socket that made them,
 * which is also what keeps a logout from leaving work scheduled against the next session.
 */
function coalescedInvalidator(client: QueryClient) {
  const pending = new Map<string, { timer: ReturnType<typeof setTimeout>; since: number }>()

  return (key: readonly string[]) => {
    const id = key.join('/')
    const now = Date.now()
    const existing = pending.get(id)
    const since = existing?.since ?? now

    if (existing) {
      // Already at the ceiling: leave the timer that is about to fire alone rather than pushing
      // it out again, which is what an unbounded debounce would do.
      if (now - since >= settleCeiling) return

      clearTimeout(existing.timer)
    }

    const delay = Math.max(0, Math.min(settleWait, since + settleCeiling - now))

    pending.set(id, {
      since,
      timer: setTimeout(() => {
        pending.delete(id)
        void client.invalidateQueries({ queryKey: key })
      }, delay),
    })
  }
}

/** Patches one incident into every cached list page that already contains it. */
function patchIncidentLists(client: QueryClient, incident: Incident) {
  const entries = client.getQueriesData<PagedResult<Incident>>({ queryKey: ['incidents'] })

  for (const [key, page] of entries) {
    if (!page?.items.some((item) => item.id === incident.id)) continue

    client.setQueryData<PagedResult<Incident>>(key, {
      ...page,
      items: page.items.map((item) => (item.id === incident.id ? incident : item)),
    })
  }
}

// ---- incident aggregate ----------------------------------------------------------------------
//
// `['incident-stats']` used to be invalidated on every incident message. That worked and it was a
// refetch per push: a thirty-incident storm asked the server thirty times for a payload whose
// counters move by exactly one each time.
//
// Split by what the push actually determines:
//
//   counts                  a delta. A new incident is +1 in its priority, its status, its source
//                           and its own UTC day, and +1 open if it arrived open. There is one
//                           possible answer, so the dashboard moves at the instant of the push.
//   detection percentiles   an invalidate. A median is not a counter; it cannot be moved by a
//                           record without the other records, and a hand-rolled approximation on
//                           screen would be a number nobody could reconcile with the server.
//   day axis, window edge   the same invalidate. Which bucket a UTC midnight rollover belongs in,
//                           and whether an arrival lands inside the span the server was asked
//                           about, are the server's calls.
//
// So both run: the counters are written immediately and one coalesced invalidate re-settles the
// rest. During a storm that is N instant updates and one request.

const openStatuses: IncidentStatus[] = ['Open', 'InProgress']

const isOpen = (status: IncidentStatus) => openStatuses.includes(status)

/** The UTC date the server buckets by. Parsed rather than sliced, so an offset form still lands
 *  in the same bucket the server put it in. */
const utcDay = (iso: string) => new Date(iso).toISOString().slice(0, 10)

/**
 * A counter moved by `by`, created when the server has never sent that key.
 *
 * Clamped at zero: a delta that would go negative means this file's model of the window and the
 * server's have parted company, and a negative count on a dashboard is a worse way to find that
 * out than a stale one. The coalesced invalidate behind every delta is what corrects it.
 */
function bump(counts: CountsByKey, key: string, by: number): CountsByKey {
  return { ...counts, [key]: Math.max(0, (counts[key] ?? 0) + by) }
}

function bumpDay(
  days: IncidentDayBucket[],
  day: string,
  priority: IncidentPriority,
  by: number,
): IncidentDayBucket[] {
  // A day the axis does not hold is left alone. Growing the axis here would draw a column the
  // chart was not built with — at a UTC midnight the settle invalidate adds it properly.
  if (!days.some((bucket) => bucket.day === day)) return days

  return days.map((bucket) =>
    bucket.day === day
      ? {
          ...bucket,
          total: Math.max(0, bucket.total + by),
          byPriority: bump(bucket.byPriority, priority, by),
        }
      : bucket,
  )
}

/**
 * Whether the incident falls inside the span these counters were cut from.
 *
 * Parsed rather than compared as text: these carry a variable number of fractional digits, and
 * `…:00Z` sorts *after* `…:00.1234567Z` on a string comparison.
 */
const inWindow = (stats: IncidentStats, incident: Incident) =>
  Date.parse(incident.createdAt) >= Date.parse(stats.from)

function addIncident(stats: IncidentStats, incident: Incident): IncidentStats {
  // The windowed counters only ever held incidents created at or after `from`. An arrival is
  // always newer than that; the guard is here because the same arithmetic runs in reverse below,
  // where it is load-bearing.
  const windowed = inWindow(stats, incident)
  const open = isOpen(incident.status)

  return {
    ...stats,
    total: windowed ? stats.total + 1 : stats.total,
    byPriority: windowed ? bump(stats.byPriority, incident.priority, 1) : stats.byPriority,
    byStatus: windowed ? bump(stats.byStatus, incident.status, 1) : stats.byStatus,
    bySource: windowed ? bump(stats.bySource, incident.source, 1) : stats.bySource,
    days: windowed
      ? bumpDay(stats.days, utcDay(incident.createdAt), incident.priority, 1)
      : stats.days,
    // Open counts ignore the window on the server too — an incident opened six weeks ago and
    // still open is counted whatever span was asked for.
    openTotal: open ? stats.openTotal + 1 : stats.openTotal,
    openByPriority: open
      ? bump(stats.openByPriority, incident.priority, 1)
      : stats.openByPriority,
    detection: {
      ...stats.detection,
      // Counters, so they move. The two percentiles beside them are not and do not.
      noticedCount: stats.detection.noticedCount + (incident.detectedAt ? 1 : 0),
      toldCount: stats.detection.toldCount + (incident.detectedAt ? 0 : 1),
    },
  }
}

/**
 * Whether a change touches anything the aggregate counts.
 *
 * False is the common case and it is the point: most `incidentChanged` pushes are an AI analysis
 * landing or a team being assigned, and the dashboard counts neither. Before this, every one of
 * them cost a refetch.
 *
 * Priority is in here because `ApplyAiAnalysis` rewrites it — the analysis re-prioritises, so a
 * Medium incident becoming Critical is a routine push, not a hypothetical. Source, createdAt and
 * detectedAt are the aggregate's other inputs and none of them is writable after creation.
 */
const movesStats = (previous: Incident, next: Incident) =>
  previous.status !== next.status || previous.priority !== next.priority

/** The same aggregate after a change `movesStats` has already accepted. */
function changeIncident(
  stats: IncidentStats,
  previous: Incident,
  next: Incident,
): IncidentStats {
  const statusMoved = previous.status !== next.status
  const priorityMoved = previous.priority !== next.priority

  // Whether this incident was ever in the windowed counters at all. Without it, resolving an
  // incident opened two months ago would decrement a seven-day window that never held it.
  const windowed = inWindow(stats, next)

  let byStatus = stats.byStatus
  let byPriority = stats.byPriority
  let days = stats.days

  if (windowed && statusMoved) {
    byStatus = bump(bump(byStatus, previous.status, -1), next.status, 1)
  }

  if (windowed && priorityMoved) {
    const day = utcDay(next.createdAt)

    byPriority = bump(bump(byPriority, previous.priority, -1), next.priority, 1)
    days = bumpDay(bumpDay(days, day, previous.priority, -1), day, next.priority, 1)
  }

  const wasOpen = isOpen(previous.status)
  const nowOpen = isOpen(next.status)

  let openByPriority = stats.openByPriority
  let openTotal = stats.openTotal

  if (wasOpen) {
    openByPriority = bump(openByPriority, previous.priority, -1)
    openTotal -= 1
  }

  if (nowOpen) {
    openByPriority = bump(openByPriority, next.priority, 1)
    openTotal += 1
  }

  return {
    ...stats,
    byStatus,
    byPriority,
    days,
    openByPriority,
    openTotal: Math.max(0, openTotal),
  }
}

/** Applies one change to every cached window, since all of them end now. */
function patchStats(client: QueryClient, apply: (stats: IncidentStats) => IncidentStats) {
  for (const [key, stats] of client.getQueriesData<IncidentStats>({
    queryKey: ['incident-stats'],
  })) {
    if (!stats) continue

    client.setQueryData<IncidentStats>(key, apply(stats))
  }
}

// ---- integrations ------------------------------------------------------------------------------

function upsertIntegration(client: QueryClient, integration: Integration) {
  client.setQueryData<Integration[]>(['integrations'], (current) => {
    if (!current) return current

    const index = current.findIndex((row) => row.id === integration.id)

    if (index < 0) return [...current, integration]

    const next = [...current]
    next[index] = integration

    return next
  })
}

/**
 * The name, channel and enabled flag a delivery-health row carries.
 *
 * Copied fields with one possible value, so they are written rather than refetched: the rest of
 * that row — the counts, the median, the worst-first ordering — cannot move because an
 * integration was renamed, so there is nothing else to re-ask for.
 */
function patchIntegrationInStats(
  client: QueryClient,
  integrationId: string,
  fields: Pick<NotificationStats['integrations'][number], 'name' | 'channel' | 'isEnabled'>,
) {
  for (const [key, stats] of client.getQueriesData<NotificationStats>({
    queryKey: ['notification-stats'],
  })) {
    if (!stats?.integrations.some((row) => row.integrationId === integrationId)) continue

    client.setQueryData<NotificationStats>(key, {
      ...stats,
      integrations: stats.integrations.map((row) =>
        row.integrationId === integrationId ? { ...row, ...fields } : row,
      ),
    })
  }
}

// ---- signatures and sources ---------------------------------------------------------------------

/**
 * A changed signature, wherever one is already on screen.
 *
 * Replace-in-place only. Whether a signature that has just appeared belongs in an evidence window
 * is a server question — that list is derived from the signals the window returned, not from the
 * signature table — so inserting one here would show a row the response never claimed.
 */
function patchSignature(client: QueryClient, signature: ErrorSignature) {
  const replace = (rows: ErrorSignature[]) =>
    rows.map((row) => (row.id === signature.id ? signature : row))

  for (const [key, evidence] of client.getQueriesData<EvidenceWindow>({
    queryKey: ['evidence'],
  })) {
    if (!evidence?.signatures.some((row) => row.id === signature.id)) continue

    client.setQueryData<EvidenceWindow>(key, {
      ...evidence,
      signatures: replace(evidence.signatures),
    })
  }

  // The incident's score panel holds its own copy, keyed by incident rather than by window.
  for (const [key, panel] of client.getQueriesData<{
    signal: Signal | null
    signatures: ErrorSignature[]
  }>({ queryKey: ['incident-signal'] })) {
    if (!panel?.signatures.some((row) => row.id === signature.id)) continue

    client.setQueryData(key, { ...panel, signatures: replace(panel.signatures) })
  }
}

function upsertSource(client: QueryClient, source: TelemetrySource) {
  client.setQueryData<TelemetrySource[]>(['telemetry-sources'], (current) => {
    if (!current) return current

    const index = current.findIndex((row) => row.id === source.id)

    if (index < 0) return [...current, source]

    const next = [...current]
    next[index] = source

    return next
  })
}

export const hubs: HubDefinition[] = [
  {
    name: 'incidents',
    url: '/hubs/incidents',
    recoverKeys: [['incidents'], ['incident'], ['incident-stats']],
    handlers: (client) => {
      const settle = coalescedInvalidator(client)

      return {
        incidentCreated: (incident: Incident) => {
          // The list invalidate stays: whether a new incident belongs on page one of whatever
          // filter is open is a server question — filter, sort and page boundary — and guessing
          // client-side gives a list that disagrees with the server as soon as somebody pages.
          void client.invalidateQueries({ queryKey: ['incidents'] })

          // The payload is the whole incident now, so opening the row that has just appeared
          // costs nothing. It is also what makes the next message exact: `incidentChanged` can
          // only compute a transition if the state before it is in the cache.
          client.setQueryData(['incident', incident.id], incident)

          patchStats(client, (stats) => addIncident(stats, incident))
          settle(['incident-stats'])
        },
        incidentChanged: (incident: Incident) => {
          const previous = client.getQueryData<Incident>(['incident', incident.id])

          client.setQueryData(['incident', incident.id], incident)
          patchIncidentLists(client, incident)

          if (!previous) {
            // Never saw the state before this one, so the transition is unknowable. This is the
            // ambiguous case the invalidate exists for.
            settle(['incident-stats'])

            return
          }

          // Nothing the aggregate counts moved — an analysis landing, a team assignment, an
          // error being recorded. Those are most of the traffic on this hub, and each one used
          // to cost a refetch of the dashboard.
          if (!movesStats(previous, incident)) return

          patchStats(client, (stats) => changeIncident(stats, previous, incident))
        },
      }
    },
  },
  {
    name: 'notifications',
    url: '/hubs/notifications',
    recoverKeys: [['deliveries'], ['integrations'], ['notification-stats']],
    handlers: (client) => {
      const settle = coalescedInvalidator(client)

      return {
        deliveryRecorded: (delivery: NotificationDelivery) => {
          client.setQueryData<NotificationDelivery[]>(
            ['deliveries', delivery.incidentId],
            (current) => {
              if (!current) return current

              // At-least-once means the same row can arrive twice; replace rather than append.
              const existing = current.findIndex((row) => row.id === delivery.id)

              if (existing >= 0) {
                const next = [...current]
                next[existing] = delivery

                return next
              }

              return [...current, delivery]
            },
          )

          // Delivery health is an invalidate, not a delta, and deliberately.
          //
          // Every delivery arrives twice — once Pending, once Sent or Failed — so a push is as
          // often a move between buckets as it is an arrival, and which of the two it is cannot
          // be told without the row's previous status, which is only cached while that one
          // incident's page is open. On top of that the totals carry a median and the rows come
          // back in a server-side worst-first order. Three ways to be wrong, so it re-asks.
          settle(['notification-stats'])
        },
        integrationChanged: (integration: Integration) => {
          upsertIntegration(client, integration)
          patchIntegrationInStats(client, integration.id, {
            name: integration.name,
            channel: integration.channel,
            isEnabled: integration.isEnabled,
          })
        },
        integrationDeleted: (integrationId: string) => {
          client.setQueryData<Integration[]>(['integrations'], (current) =>
            current?.filter((row) => row.id !== integrationId),
          )

          // The deliveries outlive the integration on purpose — they are the record that somebody
          // was told — and the endpoint returns them with a null name and channel once it is
          // gone. Written rather than refetched because that is exactly what the server will say.
          patchIntegrationInStats(client, integrationId, {
            name: null,
            channel: null,
            isEnabled: false,
          })
        },
      }
    },
  },
  {
    name: 'signals',
    url: '/hubs/signals',
    // `incident-signal` is here because the incident's score panel reads the evidence endpoint
    // under a key of its own. It was in no recover list at all, so a socket that dropped and came
    // back left that panel stale until the page was remounted.
    recoverKeys: [
      ['signals'],
      ['evidence'],
      ['incident-signal'],
      ['telemetry-sources'],
      ['telemetry-stats'],
    ],
    handlers: (client) => {
      const settle = coalescedInvalidator(client)

      return {
        signalRecorded: (signal: Signal) => {
          // Every cached window gets it. A signal detected now belongs in any window that is still
          // open, and the heat map recomputes from this list — so the map updates with no request.
          //
          // The total moves with the page. It is the count the window was cut from, so a signal
          // arriving means there is one more to have been cut from, whether or not it is shown.
          for (const [key, current] of client.getQueriesData<SignalPage>({
            queryKey: ['signals'],
          })) {
            if (!current || current.items.some((row) => row.id === signal.id)) continue

            client.setQueryData<SignalPage>(key, {
              items: [signal, ...current.items],
              totalCount: current.totalCount + 1,
            })
          }

          // The funnel is an invalidate, not a delta, and deliberately.
          //
          // The signal counters would be easy — +1 signal, +1 on its verdict — but the two stages
          // above them are not: log records and distinct signatures are only knowable from
          // ingestion, and the per-service rollup carries a top signature, which is an argmax.
          // The funnel screen is entirely about the *ratio* between the stages, so a stage that
          // moves while the one above it does not is a screen lying about what fingerprinting
          // bought. Half an aggregate is worse than a coalesced refetch.
          settle(['telemetry-stats'])
        },
        signatureChanged: (signature: ErrorSignature) => patchSignature(client, signature),
        sourceChanged: (source: TelemetrySource) => upsertSource(client, source),
        sourceDeleted: (sourceId: string) => {
          client.setQueryData<TelemetrySource[]>(['telemetry-sources'], (current) =>
            current?.filter((row) => row.id !== sourceId),
          )
        },
        ingestionCompleted: (tick: IngestionTick) => {
          // Recorded, not acted on. Silently refetching the evidence window here would put back
          // exactly the traffic this summary message exists to remove — one poll per source every
          // few seconds, multiplied by every open client. The screen shows what arrived and lets
          // the operator decide whether to re-read.
          client.setQueryData<IngestionTick[]>(ingestionKey, (current) =>
            [tick, ...(current ?? [])].slice(0, maxTicks),
          )

          settle(['telemetry-stats'])
        },
      }
    },
  },
]

export function connect(
  hub: HubDefinition,
  client: QueryClient,
  onStateChange: (name: string, state: HubConnectionState) => void,
): HubConnection {
  const connection = new HubConnectionBuilder()
    .withUrl(hub.url)
    .withAutomaticReconnect()
    .build()

  for (const [method, handler] of Object.entries(hub.handlers(client))) {
    connection.on(method, handler as (...args: unknown[]) => void)
  }

  connection.onreconnecting(() => onStateChange(hub.name, HubConnectionState.Reconnecting))

  connection.onreconnected(() => {
    onStateChange(hub.name, HubConnectionState.Connected)

    // The real cost of pushing payloads: anything sent while the socket was down never arrives.
    // One invalidation per affected key closes the gap, and only runs on a reconnect.
    //
    // `ingestion` is deliberately not on any of those lists. A tick is a notice about a moment
    // rather than a resource, so there is nothing to re-ask for — and the evidence key beside it
    // already refetches the thing the notice was about.
    for (const key of hub.recoverKeys) {
      void client.invalidateQueries({ queryKey: key })
    }
  })

  connection.onclose(() => onStateChange(hub.name, HubConnectionState.Disconnected))

  return connection
}
