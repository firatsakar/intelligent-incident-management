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
import { cn } from '@/lib/utils'
import { useT } from '@/lib/i18n'
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

  const preset = params.get('window') ?? defaultDeliveryWindow
  const query = useNotificationStats(preset)

  const scope = useT().window.scope[resolveWindowPreset(preset)]

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-x-6 gap-y-3">
        <div className="max-w-2xl">
          <h1 className="text-2xl font-semibold tracking-tight">Delivery health</h1>
          <p className="text-muted-foreground text-sm">
            Whether each channel is getting through — across the whole window, not one incident at
            a time.
          </p>
        </div>

        <WindowSelect value={preset} />
      </div>

      {query.isError && (
        <Card>
          <CardContent className="text-alarm-ink py-6 text-sm">
            {query.error instanceof Error ? query.error.message : 'Could not load delivery health'}
          </CardContent>
        </Card>
      )}

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-12">
        <div className="lg:col-span-7">
          {query.data ? (
            <TotalsCard stats={query.data} scope={scope} />
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

const plural = (count: number, word: string) => `${word}${count === 1 ? '' : 's'}`

/** Not `plural`: the one noun on this screen whose plural is not the singular with an s on it. */
const deliveries = (count: number) => (count === 1 ? 'delivery' : 'deliveries')

function TotalsCard({ stats, scope }: { stats: NotificationStats; scope: string }) {
  const total = stats.totalSent + stats.totalFailed + stats.totalPending
  const channels = stats.integrations.length
  const failing = stats.integrations.filter((row) => row.failed > 0).length

  return (
    <Card className="h-full">
      <CardHeader>
        <CardTitle>Deliveries</CardTitle>
        <CardDescription>
          Every notification this platform attempted — {scope}.
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-3">
        {total === 0 ? (
          <p className="max-w-2xl text-sm">
            <span className="text-muted-foreground">Nothing was sent in this window. </span>
            Notifications go out when an analysis finishes, so a window with no incidents in it and
            a dispatcher that has stopped look identical from here. The incidents screen says which
            of the two this is.
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
                of {formatCount(total)} {deliveries(total)} failed, across{' '}
                {formatCount(channels)} {plural(channels, 'integration')}
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
              <Total swatch="bg-alarm" label="failed" value={stats.totalFailed} />
              <Total swatch="bg-foreground/55" label="sent" value={stats.totalSent} />
              <Total swatch="bg-foreground/20" label="pending" value={stats.totalPending} />
            </dl>

            <p className="text-muted-foreground max-w-2xl text-sm">
              {stats.totalFailed > 0 ? (
                <>
                  {formatCount(failing)} {plural(failing, 'integration')} recorded a failure in
                  this window. The rows below say which, when, and what the channel said back.
                </>
              ) : (
                <>
                  Every delivery in this window got through.{' '}
                  {stats.totalPending > 0 ? (
                    <>
                      {formatCount(stats.totalPending)} {deliveries(stats.totalPending)}{' '}
                      {stats.totalPending === 1 ? 'is' : 'are'} still queued and{' '}
                      {stats.totalPending === 1 ? 'has' : 'have'} not been attempted yet — queued
                      is not sent.
                    </>
                  ) : (
                    <>Nothing is queued and nothing is outstanding.</>
                  )}
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
  const measured = stats.integrations.filter((row) => row.medianDispatchSeconds !== null)

  const rows: BarRow[] = stats.integrations.map((row) => ({
    key: row.integrationId,
    label: nameOf(row),
    value: row.medianDispatchSeconds,
    display: formatSeconds(row.medianDispatchSeconds),
    dim: row.medianDispatchSeconds === null,
  }))

  return (
    <Card className="h-full">
      <CardHeader>
        <CardTitle>Dispatch time</CardTitle>
        <CardDescription>
          How long each channel took to accept a notification, median — {scope}. Measured from the
          delivery being written to the channel acknowledging it.
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-3">
        {stats.integrations.length === 0 ? (
          <p className="text-muted-foreground text-sm">
            Nothing was dispatched in this window, so there is nothing to have timed.
          </p>
        ) : measured.length === 0 ? (
          <p className="text-sm">
            <span className="text-muted-foreground">Nothing succeeded in this window, </span>
            so there is no dispatch time to draw. That is not a dispatch time of zero — it is the
            absence of one.
          </p>
        ) : (
          <>
            {/* The bars are here for the ratio rather than the absolute: twenty-to-one between a
                webhook and a mail server is the fact worth seeing, and it is the one thing a
                column of durations does not show. Each row prints its own exact figure. */}
            <BarRows
              rows={rows}
              label={`Median dispatch time per integration, ${scope}. ${measured
                .map((row) => `${nameOf(row)} ${formatSeconds(row.medianDispatchSeconds)}`)
                .join(', ')}.`}
            />

            {measured.length < stats.integrations.length && (
              <p className="text-muted-foreground text-xs">
                An em dash is an integration that had nothing succeed in this window, so it has no
                median. It is not a dispatch time of zero.
              </p>
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
const nameOf = (row: IntegrationHealth) => row.name ?? 'Deleted integration'

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
// everything else is the calm baseline. Each carries a word and a shape as well as the colour.
const verdictStyle: Record<Verdict, { label: string; className: string; icon: LucideIcon }> = {
  failing: { label: 'Failing', className: statusTier.alarm, icon: TriangleAlertIcon },
  recovered: { label: 'Recovered', className: statusTier.caution, icon: CircleCheckIcon },
  delivering: { label: 'Delivering', className: statusTier.nominal, icon: CircleCheckIcon },
  queued: { label: 'Queued', className: statusTier.inert, icon: ClockIcon },
  silent: { label: 'Nothing sent', className: statusTier.inert, icon: CircleSlashIcon },
}

function IntegrationsCard({ stats, scope }: { stats: NotificationStats; scope: string }) {
  const anyMissingMedian = stats.integrations.some((row) => row.medianDispatchSeconds === null)

  return (
    <Card>
      <CardHeader>
        <CardTitle>By integration</CardTitle>
        <CardDescription>
          One row per integration that attempted a delivery — {scope}. In the order the server
          returned them, which is worst first.
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-4">
        {stats.integrations.length === 0 ? (
          <div className="py-6 text-center">
            <p className="text-sm font-medium">No integration attempted a delivery.</p>
            <p className="text-muted-foreground mx-auto mt-1 max-w-md text-sm">
              An integration only appears here once it has something to report. One configured and
              enabled but never reached in this window is not on this list — Settings › Integrations
              is the roll of what exists.
            </p>
          </div>
        ) : (
          <ul className="divide-border -my-1 divide-y">
            {stats.integrations.map((row) => (
              <IntegrationRow key={row.integrationId} row={row} />
            ))}
          </ul>
        )}

        {anyMissingMedian && (
          // Said once, here, rather than on every row that has one. An em dash with no explanation
          // invites the reader to supply one, and the one they supply is "zero".
          <p className="text-muted-foreground text-xs">
            A median dispatch of — means nothing succeeded for that integration in this window.
            That is the absence of a measurement, not a measurement of zero.
          </p>
        )}
      </CardContent>
    </Card>
  )
}

function IntegrationRow({ row }: { row: IntegrationHealth }) {
  const deleted = row.name === null
  const verdict = verdictStyle[verdictOf(row)]

  return (
    <li className="space-y-2 py-3">
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
        <Badge variant="outline" className={cn('border', verdict.className)}>
          <verdict.icon aria-hidden />
          {verdict.label}
        </Badge>

        <span
          className={cn(
            'min-w-0 flex-1 truncate text-sm font-medium',
            deleted && 'text-muted-foreground font-normal italic',
          )}
          title={nameOf(row)}
        >
          {nameOf(row)}
        </span>

        {row.channel && <span className="text-dim-foreground shrink-0 text-xs">{row.channel}</span>}

        {/* Only for an integration that still exists. A deleted one reads as disabled through the
            same field, and saying "disabled" about something that is gone is two wrong words. */}
        {!deleted && !row.isEnabled && (
          <Badge variant="outline" className={cn('border', statusTier.caution)}>
            disabled
          </Badge>
        )}
      </div>

      {deleted && (
        <p className="text-muted-foreground text-xs">
          This integration has been deleted. Its deliveries are kept on purpose — they are the
          record that somebody was told — so the counts below are still true, and the name and
          channel they belonged to are gone.{' '}
          <span className="text-dim-foreground" title={row.integrationId}>
            id {row.integrationId.slice(0, 8)}
          </span>
        </p>
      )}

      {/* The same grid on every row, so the figures line up down the page and can be compared by
          eye. That comparison is the reason this screen exists. */}
      <dl className="grid grid-cols-2 gap-x-6 gap-y-2 sm:grid-cols-3 lg:grid-cols-5">
        <Metric label="Sent" value={formatCount(row.sent)} zero={row.sent === 0} />
        <Metric
          label="Failed"
          value={formatCount(row.failed)}
          zero={row.failed === 0}
          className={row.failed > 0 ? 'text-alarm-ink' : undefined}
        />
        <Metric label="Pending" value={formatCount(row.pending)} zero={row.pending === 0} />
        <Metric
          label="Median dispatch"
          value={formatSeconds(row.medianDispatchSeconds)}
          zero={row.medianDispatchSeconds === null}
        />
        <Metric
          label="Last sent"
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
            Last failure{' '}
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
