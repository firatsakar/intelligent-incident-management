import { useSearchParams } from 'react-router-dom'

// Moved up to the chart primitives once the funnel and the delivery screens needed the same bar:
// three callers is a shared thing, and three copies of a ratio-to-pixels rule is how two of them
// end up rounding differently.
import { ProportionBar } from '@/components/chart/ProportionBar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { formatSeconds, priorityBackground } from '@/lib/format'
import { cn } from '@/lib/utils'
import {
  incidentPriorities,
  type CountsByKey,
  type DetectionLatency,
  type IncidentSource,
  type IncidentStats,
} from '@/types/api'

import { IncidentsByDayChart } from './IncidentsByDayChart'
import { LatestIncidents } from './LatestIncidents'
import { useIncidentStats } from './queries'
import { dayWindowLabel, dayWindows, resolveDayWindow, type DayWindow } from './window'

/**
 * The home screen.
 *
 * Reading order is the order an operator asks the questions in, not the order the endpoint returns
 * them: what is broken now, how that compares with the last few weeks, what has just arrived, and
 * only then the two answers about the platform's own behaviour. The first card is the only one that
 * ignores the window, and it is first because a Critical incident opened six weeks ago and still
 * open outranks anything that happened today.
 *
 * Every card states its own scope in words. A screen where one number is windowed and the one
 * beside it is not, with nothing on screen saying which is which, is a screen that will eventually
 * be read wrong at 3am — so the window is named on each windowed card rather than being implied by
 * the picker at the top.
 */
export function DashboardPage() {
  // In the URL, like every other filter in this console: a dashboard someone is looking at should
  // be a link they can send, and it should survive a refresh.
  const [params, setParams] = useSearchParams()

  const days = resolveDayWindow(params.get('days'))
  const query = useIncidentStats(days)

  function setWindow(value: DayWindow) {
    const next = new URLSearchParams(params)
    next.set('days', String(value))

    setParams(next)
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-x-6 gap-y-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Dashboard</h1>
          <p className="text-muted-foreground text-sm">
            What is open now, what arrived, and how much of it the platform found on its own.
          </p>
        </div>

        {/* A segmented group rather than a select: three short options, and showing all three at
            once costs one row and saves a click. Each is a real button, so the group is three tab
            stops and the pressed state is announced rather than inferred from the fill. */}
        <div
          role="group"
          aria-label="Time window"
          className="bg-muted flex items-center gap-0.5 rounded-lg p-0.5"
        >
          {dayWindows.map((option) => (
            <Button
              key={option}
              size="sm"
              variant={option === days ? 'outline' : 'ghost'}
              aria-pressed={option === days}
              onClick={() => setWindow(option)}
              className={cn('tabular-nums', option === days && 'bg-card shadow-sm')}
            >
              {option} days
            </Button>
          ))}
        </div>
      </div>

      {query.isError && (
        <Card>
          <CardContent className="text-alarm-ink py-6 text-sm">
            {query.error instanceof Error ? query.error.message : 'Could not load the figures'}
          </CardContent>
        </Card>
      )}

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-12">
        <div className="lg:col-span-12">
          {query.data ? <OpenRightNow stats={query.data} /> : <CardSkeleton height="h-36" />}
        </div>

        <div className="lg:col-span-8">
          {query.data ? (
            <IncidentsByDate stats={query.data} days={days} />
          ) : (
            <CardSkeleton height="h-80" />
          )}
        </div>

        <div className="lg:col-span-4">
          {/* Independent of the window and of the aggregate, so it renders on its own clock. */}
          <LatestIncidents />
        </div>

        <div className="lg:col-span-6">
          {query.data ? (
            <DetectionCard detection={query.data.detection} days={days} />
          ) : (
            <CardSkeleton height="h-56" />
          )}
        </div>

        <div className="lg:col-span-6">
          {query.data ? (
            <SourcesCard bySource={query.data.bySource} total={query.data.total} days={days} />
          ) : (
            <CardSkeleton height="h-56" />
          )}
        </div>
      </div>
    </div>
  )
}

function CardSkeleton({ height }: { height: string }) {
  return <Skeleton className={cn('w-full', height)} />
}

function OpenRightNow({ stats }: { stats: IncidentStats }) {
  const segments = incidentPriorities.map((priority) => ({
    key: priority,
    value: stats.openByPriority[priority] ?? 0,
    className: priorityBackground[priority],
  }))

  return (
    <Card>
      <CardHeader>
        <CardTitle>Open right now</CardTitle>
        <CardDescription>
          Every incident still Open or In progress, however old. One opened six weeks ago and never
          closed is the one you most need to see, so this count deliberately ignores the window.
        </CardDescription>

        <CardAction>
          {/* On the card, not in a tooltip. Which numbers move with the picker is the kind of thing
              a reader has to be able to check at a glance rather than by hovering. */}
          <Badge variant="outline" className="text-muted-foreground">
            not windowed
          </Badge>
        </CardAction>
      </CardHeader>

      <CardContent className="space-y-3">
        <div className="flex flex-wrap items-end gap-x-8 gap-y-3">
          <div className="flex items-baseline gap-2">
            <span className="text-3xl leading-none font-semibold tabular-nums">
              {stats.openTotal}
            </span>
            <span className="text-muted-foreground text-sm">open</span>
          </div>

          <dl className="flex flex-wrap items-baseline gap-x-6 gap-y-2">
            {incidentPriorities.map((priority) => {
              const count = stats.openByPriority[priority] ?? 0

              return (
                <div key={priority} className="flex items-baseline gap-1.5">
                  {/* The swatch repeats the bar's colour so the two can be matched, and the word
                      beside it means nobody has to. */}
                  <span
                    aria-hidden
                    className={cn('size-2.5 shrink-0 rounded-[3px]', priorityBackground[priority])}
                  />
                  <dt className="text-muted-foreground text-sm">{priority}</dt>
                  <dd
                    className={cn(
                      'text-sm font-medium tabular-nums',
                      count === 0 && 'text-dim-foreground',
                    )}
                  >
                    {count}
                  </dd>
                </div>
              )
            })}
          </dl>
        </div>

        <ProportionBar segments={segments} />
      </CardContent>
    </Card>
  )
}

function IncidentsByDate({ stats, days }: { stats: IncidentStats; days: DayWindow }) {
  return (
    <Card className="h-full">
      <CardHeader>
        <CardTitle>Incidents by date</CardTitle>
        <CardDescription>
          When incidents were opened, stacked by priority — {dayWindowLabel(days)}. Days are cut in{' '}
          <strong className="font-medium">UTC</strong> on the server, not in your zone, and the last
          column is today, still filling.
        </CardDescription>
      </CardHeader>

      <CardContent>
        <IncidentsByDayChart days={stats.days} />
      </CardContent>
    </Card>
  )
}

function DetectionCard({ detection, days }: { detection: DetectionLatency; days: DayWindow }) {
  const seen = detection.noticedCount + detection.toldCount
  const share = seen > 0 ? Math.round((detection.noticedCount / seen) * 100) : null

  return (
    <Card className="h-full">
      <CardHeader>
        <CardTitle>Detection</CardTitle>
        <CardDescription>
          How much the platform noticed by itself rather than being told, and how long it took to
          open what it noticed — {dayWindowLabel(days)}, UTC.
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-3">
        {seen === 0 ? (
          <p className="text-muted-foreground text-sm">
            No incidents in this window, so there is nothing to have noticed. Try a longer window.
          </p>
        ) : (
          <>
            <div className="flex items-baseline gap-2">
              <span className="text-3xl leading-none font-semibold tabular-nums">{share}%</span>
              <span className="text-muted-foreground text-sm">noticed by the platform itself</span>
            </div>

            {/* Both segments neutral. "Somebody filed it" is a different origin, not a failure, and
                painting one green and the other grey would be this screen passing a judgement the
                numbers do not support. */}
            <ProportionBar
              segments={[
                { key: 'noticed', value: detection.noticedCount, className: 'bg-foreground/70' },
                { key: 'told', value: detection.toldCount, className: 'bg-foreground/25' },
              ]}
            />

            <dl className="grid grid-cols-2 gap-x-6 gap-y-2 text-sm">
              <Figure label="Noticed" value={String(detection.noticedCount)} />
              <Figure label="Filed by hand" value={String(detection.toldCount)} />
              <Figure label="Median latency" value={formatSeconds(detection.medianSeconds)} />
              <Figure label="95th percentile" value={formatSeconds(detection.p95Seconds)} />
            </dl>

            {detection.medianSeconds === null && (
              // An em dash with no explanation invites the reader to supply one, and the one they
              // supply is "zero". These are opposite facts, so the reason is spelled out.
              <p className="text-muted-foreground text-sm">
                {detection.noticedCount === 0
                  ? 'Nothing in this window was noticed automatically, so there is no latency to measure. That is not a latency of zero — it is the absence of one.'
                  : 'Every detection in this window came back with the record filed before the problem started, which is two clocks disagreeing rather than a latency. Those rows sit out of the percentiles.'}
              </p>
            )}
          </>
        )}
      </CardContent>
    </Card>
  )
}

function Figure({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline justify-between gap-3">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="font-medium tabular-nums">{value}</dd>
    </div>
  )
}

// Fixed order, most autonomous first, because the order is the point being made: the platform
// found these, an alert raised those, a person filed the rest. Sorting by count would reshuffle
// the sentence every time the data moved.
const sourceOrder: { key: IncidentSource; meaning: string }[] = [
  { key: 'Telemetry', meaning: 'the platform found it in the log stream' },
  { key: 'Alert', meaning: 'an external alert raised it' },
  { key: 'Manual', meaning: 'somebody opened it by hand' },
]

function SourcesCard({
  bySource,
  total,
  days,
}: {
  bySource: CountsByKey
  total: number
  days: DayWindow
}) {
  return (
    <Card className="h-full">
      <CardHeader>
        <CardTitle>Where incidents come from</CardTitle>
        <CardDescription>
          How each incident got opened — {dayWindowLabel(days)}, UTC.
        </CardDescription>
      </CardHeader>

      <CardContent>
        {total === 0 ? (
          <p className="text-muted-foreground text-sm">
            No incidents in this window, from any source.
          </p>
        ) : (
          <ul className="space-y-3">
            {sourceOrder.map(({ key, meaning }) => {
              const count = bySource[key] ?? 0
              const share = Math.round((count / total) * 100)

              return (
                <li key={key} className="space-y-1">
                  <div className="flex items-baseline justify-between gap-3">
                    <span className="text-sm font-medium">{key}</span>
                    <span className="text-muted-foreground shrink-0 text-sm tabular-nums">
                      {count} · {share}%
                    </span>
                  </div>

                  {/* One neutral fill for all three. A source is not a verdict, and colour here is
                      reserved for the things the system committed to. */}
                  <div aria-hidden className="bg-muted h-1.5 w-full overflow-hidden rounded-full">
                    <div className="bg-foreground/55 h-full rounded-full" style={{ width: `${share}%` }} />
                  </div>

                  <p className="text-muted-foreground text-xs">{meaning}</p>
                </li>
              )
            })}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}
