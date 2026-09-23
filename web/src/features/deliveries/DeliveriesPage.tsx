import { CircleCheckIcon, ClockIcon, CircleSlashIcon, TriangleAlertIcon } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { useSearchParams } from 'react-router-dom'

import { BarRows, type BarRow } from '@/components/chart/BarRows'
import { ProportionBar } from '@/components/chart/ProportionBar'
import { Badge } from '@/components/ui/badge'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { WindowSelect } from '@/components/WindowSelect'
import {
  formatCount,
  formatDateTime,
  formatRelative,
  formatSeconds,
  statusTier,
} from '@/lib/format'
import { useT, type Dictionary } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { resolveWindowPreset } from '@/lib/window'
import type { IntegrationHealth, NotificationStats } from '@/types/api'

import { defaultDeliveryWindow, useNotificationStats } from './queries'

/**
 * Whether each notification channel is working — the question one incident's delivery strip can
 * never answer, because it only ever shows the one incident.
 *
 * Three things the payload hands this screen, all of which are easy to render wrongly:
 *
 * `name` and `channel` are null once the integration has been deleted. The deliveries survive on
 * purpose — they are the record that somebody was told — so the row has to stand up without a
 * name rather than being filtered out.
 *
 * `medianDispatchSeconds` is null when nothing succeeded, which is the opposite of zero. It prints
 * as an em dash everywhere, and the card says once what the dash means instead of putting the
 * sentence on every row.
 *
 * And the rows arrive worst-first from the server, so this screen does not re-sort them. Ordering
 * is a decision that was already taken with the failure counts in hand.
 */
export function DeliveriesPage() {
  const [params] = useSearchParams()
  const { deliveries: t, window: windowText } = useT()

  const preset = params.get('window') ?? defaultDeliveryWindow
  const query = useNotificationStats(preset)

  const preset_ = resolveWindowPreset(preset)
  const scope = windowText.scope[preset_]
  // The totals card's description is now only the scope, so it starts a line and takes the
  // capitalised entry rather than being upper-cased here.
  const scopeCap = windowText.scopeCap[preset_]

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-x-6 gap-y-3">
        <div className="max-w-2xl">
          <h1 className="text-2xl font-semibold tracking-tight">{t.title}</h1>
          <p className="text-muted-foreground text-sm">{t.intro}</p>
        </div>

        <WindowSelect value={preset} />
      </div>

      {query.isError && (
        <Card>
          <CardContent className="text-alarm-ink py-6 text-sm">
            {query.error instanceof Error ? query.error.message : t.loadError}
          </CardContent>
        </Card>
      )}

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-12">
        <div className="lg:col-span-7">
          {query.data ? (
            <TotalsCard stats={query.data} scope={scopeCap} />
          ) : (
            <Skeleton className="h-64 w-full" />
          )}
        </div>

        <div className="lg:col-span-5">
          {query.data ? (
            <DispatchCard stats={query.data} scope={scope} />
          ) : (
            <Skeleton className="h-64 w-full" />
          )}
        </div>

        <div className="lg:col-span-12">
          {query.data ? (
            <IntegrationsCard stats={query.data} scope={scope} />
          ) : (
            <Skeleton className="h-72 w-full" />
          )}
        </div>
      </div>
    </div>
  )
}

// The two hand-rolled pluralisers this file carried — one appending an "s", one special-casing
// "delivery/deliveries" — are gone. Both were English grammar written into a component, and the
// second existed only because the first was wrong about a word.

function TotalsCard({ stats, scope }: { stats: NotificationStats; scope: string }) {
  const t = useT().deliveries.totals

  const total = stats.totalSent + stats.totalFailed + stats.totalPending
  const channels = stats.integrations.length
  const failing = stats.integrations.filter((row) => row.failed > 0).length

  return (
    <Card className="h-full">
      <CardHeader>
        <CardTitle>{t.title}</CardTitle>
        <CardDescription>{t.description(scope)}</CardDescription>
      </CardHeader>

      <CardContent className="space-y-3">
        {total === 0 ? (
          <p className="max-w-2xl text-sm">
            <span className="text-muted-foreground">{t.emptyLead}</span>
            {t.empty}
          </p>
        ) : (
          <>
            {/* Same shape as the funnel's headline, and for the same reason: the denominator is on
                the line, so a zero cannot be read as "nothing was attempted". */}
            <p className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
              <span
                className={cn(
                  'text-5xl leading-none font-semibold tabular-nums',
                  stats.totalFailed > 0 ? 'text-alarm-ink' : 'text-dim-foreground',
                )}
              >
                {formatCount(stats.totalFailed)}
              </span>
              <span className="text-muted-foreground text-sm">
                {t.failedOf(formatCount(total), total, formatCount(channels), channels)}
              </span>
            </p>

            {/* Failed is the one segment allowed the solid alarm fill. A delivery that did not
                arrive is a verdict the system reached, not a shade of normal. */}
            <ProportionBar
              segments={[
                { key: 'failed', value: stats.totalFailed, className: 'bg-alarm' },
                { key: 'sent', value: stats.totalSent, className: 'bg-foreground/55' },
                { key: 'pending', value: stats.totalPending, className: 'bg-foreground/20' },
              ]}
            />

            <dl className="flex flex-wrap items-baseline gap-x-6 gap-y-2 text-sm">
              <Total swatch="bg-alarm" label={t.failed} value={stats.totalFailed} />
              <Total swatch="bg-foreground/55" label={t.sent} value={stats.totalSent} />
              <Total swatch="bg-foreground/20" label={t.pending} value={stats.totalPending} />
            </dl>

            <p className="text-muted-foreground max-w-2xl text-sm">
              {stats.totalFailed > 0 ? (
                t.someFailing(formatCount(failing), failing)
              ) : (
                <>
                  {t.allThrough}
                  {stats.totalPending > 0
                    ? t.stillQueued(formatCount(stats.totalPending), stats.totalPending)
                    : t.nothingQueued}
                </>
              )}
            </p>
          </>
        )}
      </CardContent>
    </Card>
  )
}

function Total({ swatch, label, value }: { swatch: string; label: string; value: number }) {
  return (
    <div className="flex items-baseline gap-1.5">
      <span aria-hidden className={cn('size-2.5 shrink-0 rounded-[3px]', swatch)} />
      <dt className="text-muted-foreground">{label}</dt>
      <dd className={cn('font-medium tabular-nums', value === 0 && 'text-dim-foreground')}>
        {formatCount(value)}
      </dd>
    </div>
  )
}

function DispatchCard({ stats, scope }: { stats: NotificationStats; scope: string }) {
  const { deliveries } = useT()
  const t = deliveries.dispatch

  const measured = stats.integrations.filter((row) => row.medianDispatchSeconds !== null)

  const rows: BarRow[] = stats.integrations.map((row) => ({
    key: row.integrationId,
    label: nameOf(deliveries, row),
    value: row.medianDispatchSeconds,
    display: formatSeconds(row.medianDispatchSeconds),
    dim: row.medianDispatchSeconds === null,
  }))

  return (
    <Card className="h-full">
      <CardHeader>
        <CardTitle>{t.title}</CardTitle>
        <CardDescription>{t.description(scope)}</CardDescription>
      </CardHeader>

      <CardContent className="space-y-3">
        {stats.integrations.length === 0 ? (
          <p className="text-muted-foreground text-sm">{t.empty}</p>
        ) : measured.length === 0 ? (
          <p className="text-sm">
            <span className="text-muted-foreground">{t.noneSucceededLead}</span>
            {t.noneSucceeded}
          </p>
        ) : (
          <>
            {/* The bars are here for the ratio rather than the absolute: twenty-to-one between a
                webhook and a mail server is the fact worth seeing, and it is the one thing a
                column of durations does not show. Each row prints its own exact figure. */}
            <BarRows
              rows={rows}
              label={t.chartLabel(
                scope,
                measured
                  .map(
                    (row) =>
                      `${nameOf(deliveries, row)} ${formatSeconds(row.medianDispatchSeconds)}`,
                  )
                  .join(', '),
              )}
            />

            {measured.length < stats.integrations.length && (
              <p className="text-muted-foreground text-xs">{t.dashNote}</p>
            )}
          </>
        )}
      </CardContent>
    </Card>
  )
}

/**
 * The name a deleted integration has left.
 *
 * Deleting an integration removes the name and the channel, not the deliveries — those are the
 * record that somebody was told, and they are kept on purpose. So the row survives without a name
 * rather than disappearing, which would quietly delete the evidence along with the config.
 */
type DeliveriesText = Dictionary['deliveries']

const nameOf = (t: DeliveriesText, row: IntegrationHealth) => row.name ?? t.deleted

type Verdict = 'failing' | 'recovered' | 'delivering' | 'queued' | 'silent'

/**
 * What this channel is doing, as a word.
 *
 * A failure count alone is not a verdict: an integration that failed twice on Monday and has
 * delivered forty times since is working, and calling it "failing" all week teaches the operator
 * to ignore the word. So the last failure is compared against the last success, and only a channel
 * whose most recent word from the far end was an error is called failing.
 */
function verdictOf(row: IntegrationHealth): Verdict {
  if (row.failed > 0) {
    const failedAt = row.lastErrorAt ? Date.parse(row.lastErrorAt) : 0
    const sentAt = row.lastSentAt ? Date.parse(row.lastSentAt) : 0

    return sentAt > failedAt ? 'recovered' : 'failing'
  }

  if (row.sent > 0) return 'delivering'
  if (row.pending > 0) return 'queued'

  return 'silent'
}

// Only `failing` reaches the solid alarm fill. Recovered is a real caveat and gets the amber tint;
// everything else is the calm baseline. Each carries a word and a shape as well as the colour —
// the word comes from the dictionary, the colour and the shape stay here.
const verdictStyle: Record<Verdict, { className: string; icon: LucideIcon }> = {
  failing: { className: statusTier.alarm, icon: TriangleAlertIcon },
  recovered: { className: statusTier.caution, icon: CircleCheckIcon },
  delivering: { className: statusTier.nominal, icon: CircleCheckIcon },
  queued: { className: statusTier.inert, icon: ClockIcon },
  silent: { className: statusTier.inert, icon: CircleSlashIcon },
}

function IntegrationsCard({ stats, scope }: { stats: NotificationStats; scope: string }) {
  const t = useT().deliveries.byIntegration

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t.title}</CardTitle>
        <CardDescription>{t.description(scope)}</CardDescription>
      </CardHeader>

      <CardContent className="space-y-4">
        {stats.integrations.length === 0 ? (
          <div className="py-6 text-center">
            <p className="text-sm font-medium">{t.emptyTitle}</p>
            <p className="text-muted-foreground mx-auto mt-1 max-w-md text-sm">{t.empty}</p>
          </div>
        ) : (
          <ul className="divide-border -my-1 divide-y">
            {stats.integrations.map((row) => (
              <IntegrationRow key={row.integrationId} row={row} />
            ))}
          </ul>
        )}

        {/* The em dash is explained by the dispatch card above rather than here. That card
            renders `noneSucceeded` when every median is null and `dashNote` when only some are,
            so exactly one of the two is already on screen whenever this note would have been —
            the fact was being stated twice, always. */}
      </CardContent>
    </Card>
  )
}

function IntegrationRow({ row }: { row: IntegrationHealth }) {
  const { deliveries, labels } = useT()
  const t = deliveries.byIntegration

  const deleted = row.name === null
  const kind = verdictOf(row)
  const verdict = verdictStyle[kind]
  const name = nameOf(deliveries, row)

  return (
    <li className="space-y-2 py-3">
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
        <Badge variant="outline" className={cn('border', verdict.className)}>
          <verdict.icon aria-hidden />
          {deliveries.verdict[kind]}
        </Badge>

        <span
          className={cn(
            'min-w-0 flex-1 truncate text-sm font-medium',
            deleted && 'text-muted-foreground font-normal italic',
          )}
          title={name}
        >
          {name}
        </span>

        {row.channel && (
          <span className="text-dim-foreground shrink-0 text-xs">
            {labels.channel[row.channel as keyof typeof labels.channel] ?? row.channel}
          </span>
        )}

        {/* Only for an integration that still exists. A deleted one reads as disabled through the
            same field, and saying "disabled" about something that is gone is two wrong words. */}
        {!deleted && !row.isEnabled && (
          <Badge variant="outline" className={cn('border', statusTier.caution)}>
            {t.disabled}
          </Badge>
        )}
      </div>

      {deleted && (
        <p className="text-muted-foreground text-xs">
          {t.deletedNote}{' '}
          <span className="text-dim-foreground" title={row.integrationId}>
            {t.id(row.integrationId.slice(0, 8))}
          </span>
        </p>
      )}

      {/* The same grid on every row, so the figures line up down the page and can be compared by
          eye. That comparison is the reason this screen exists. */}
      <dl className="grid grid-cols-2 gap-x-6 gap-y-2 sm:grid-cols-3 lg:grid-cols-5">
        <Metric label={t.sent} value={formatCount(row.sent)} zero={row.sent === 0} />
        <Metric
          label={t.failed}
          value={formatCount(row.failed)}
          zero={row.failed === 0}
          className={row.failed > 0 ? 'text-alarm-ink' : undefined}
        />
        <Metric label={t.pending} value={formatCount(row.pending)} zero={row.pending === 0} />
        <Metric
          label={t.median}
          value={formatSeconds(row.medianDispatchSeconds)}
          zero={row.medianDispatchSeconds === null}
        />
        <Metric
          label={t.lastSent}
          value={formatRelative(row.lastSentAt)}
          title={row.lastSentAt ? formatDateTime(row.lastSentAt) : undefined}
          zero={row.lastSentAt === null}
        />
      </dl>

      {row.lastError && (
        // Not the solid alarm fill: this is somebody else's SMTP or HTTP error, it runs to several
        // lines, and it is a report rather than a verdict the platform reached. Same treatment the
        // incident's delivery strip and the settings screens give a failed connection test.
        <div className="border-alarm-border/70 bg-alarm/10 rounded-md border px-2 py-1.5">
          {/* muted rather than dim: the tinted panel lifts the background, and the dim ink that
              clears AA on a card does not clear it here. */}
          <p className="text-muted-foreground text-xs tabular-nums">
            {t.lastFailure}{' '}
            <span title={row.lastErrorAt ? formatDateTime(row.lastErrorAt) : undefined}>
              {formatRelative(row.lastErrorAt)}
            </span>
          </p>
          <p className="text-alarm-ink text-xs break-words">{row.lastError}</p>
        </div>
      )}
    </li>
  )
}

function Metric({
  label,
  value,
  title,
  zero,
  className,
}: {
  label: string
  value: string
  title?: string
  /** Dimmed rather than hidden: a zero and an em dash are both measurements worth reading. */
  zero?: boolean
  className?: string
}) {
  return (
    <div className="min-w-0">
      <dt className="text-muted-foreground text-[0.6875rem] font-medium tracking-wider uppercase">
        {label}
      </dt>
      <dd
        className={cn(
          'truncate text-sm font-medium tabular-nums',
          zero && 'text-dim-foreground',
          className,
        )}
        title={title}
      >
        {value}
      </dd>
    </div>
  )
}
