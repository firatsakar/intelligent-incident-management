import { useSearchParams } from 'react-router-dom'

import { BarRows, type BarRow } from '@/components/chart/BarRows'
import { ProportionBar } from '@/components/chart/ProportionBar'
import { Badge } from '@/components/ui/badge'
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { WindowSelect } from '@/components/WindowSelect'
import { formatCount, formatPercent, severityClass, signalStatusClass } from '@/lib/format'
import { useT } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { logSeverities, type Funnel, type LogSeverity, type SignalStatus } from '@/types/api'

import { defaultStatsWindow, useTelemetryStats } from './queries'

/**
 * The screen that turns the product's central claim into a number.
 *
 * The claim is that this is not an alerting rule: it reads everything, scores what bursts by
 * arithmetic you can check, and then deliberately does not wake anybody for most of it.
 * `notRaised` is that last part counted, so it leads and it is the largest thing on the page.
 *
 * Which means the screen has to survive that number being zero, and on the window this thing ships
 * with, it is. A bare "0" under the word "Not raised" reads as *nothing was examined* — the exact
 * opposite of the claim it is there to support. The fix is structural rather than a matter of
 * wording: **the denominator travels with the numerator**. The headline is never "0", it is "0 of
 * 11 signals the gate scored", on one line, at one glance. A reader cannot take that as an empty
 * window because the count of what was examined is inside the same sentence.
 *
 * Below it the funnel does the other half: 260 records folding into 3 signatures is what
 * fingerprinting bought, and a screen that did not make that visible would have missed its own
 * point.
 */
export function FunnelPage() {
  const [params] = useSearchParams()
  const { telemetry } = useT()
  const t = telemetry.funnel

  const preset = params.get('window') ?? defaultStatsWindow
  const query = useTelemetryStats(preset)

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-x-6 gap-y-3">
        <div className="max-w-2xl">
          <h1 className="text-2xl font-semibold tracking-tight">{t.title}</h1>
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
        <div className="lg:col-span-12">
          {query.data ? (
            <RestraintCard funnel={query.data.funnel} />
          ) : (
            <Skeleton className="h-44 w-full" />
          )}
        </div>

        <div className="lg:col-span-7">
          {query.data ? (
            <StagesCard funnel={query.data.funnel} />
          ) : (
            <Skeleton className="h-80 w-full" />
          )}
        </div>

        <div className="lg:col-span-5">
          {query.data ? (
            <VerdictsCard funnel={query.data.funnel} />
          ) : (
            <Skeleton className="h-80 w-full" />
          )}
        </div>
      </div>
    </div>
  )
}

// The hand-rolled `plural` this file used to carry is gone: an English "s" appended to a word
// is not a rule any other language shares, and the dictionary's own `plural` knows which language
// it is writing in.

function RestraintCard({ funnel }: { funnel: Funnel }) {
  const t = useT().telemetry.funnel

  const actedOn = funnel.signals - funnel.notRaised

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t.notRaised}</CardTitle>
      </CardHeader>

      <CardContent className="space-y-3">
        {funnel.signals === 0 ? (
          <NothingScored funnel={funnel} />
        ) : (
          <>
            {/* The denominator sits on the same baseline as the headline, not under it and not in
                the description. It is the whole defence against a zero here being read as an empty
                window, so it cannot be somewhere the eye can skip. */}
            <p className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
              <span
                className={cn(
                  'text-5xl leading-none font-semibold tabular-nums',
                  funnel.notRaised === 0 && 'text-dim-foreground',
                )}
              >
                {formatCount(funnel.notRaised)}
              </span>
              <span className="text-muted-foreground text-sm">
                {t.ofScored(funnel.signals)}
              </span>
            </p>

            {/* Both fills neutral, in the two weights the dashboard's detection card already
                uses. "Held back" and "acted on" are two correct outcomes of the same arithmetic,
                not a pass and a fail, and painting one green would be this screen passing a
                judgement the numbers do not support. Held back is the heavier and comes first
                because it is the subject — and on this data it is the smaller share, so it is the
                one that needs the help. */}
            <ProportionBar
              segments={[
                { key: 'notRaised', value: funnel.notRaised, className: 'bg-foreground/70' },
                { key: 'actedOn', value: actedOn, className: 'bg-foreground/25' },
              ]}
            />

            <dl className="flex flex-wrap items-baseline gap-x-6 gap-y-2 text-sm">
              <Split
                fill="bg-foreground/70"
                label={t.heldBack}
                value={funnel.notRaised}
                total={funnel.signals}
              />
              <Split
                fill="bg-foreground/25"
                label={t.actedOn}
                value={actedOn}
                total={funnel.signals}
              />
            </dl>

          </>
        )}
      </CardContent>
    </Card>
  )
}

function Split({
  fill,
  label,
  value,
  total,
}: {
  fill: string
  label: string
  value: number
  total: number
}) {
  return (
    <div className="flex items-baseline gap-1.5">
      {/* The swatch repeats the bar's fill so the two can be matched; the word beside it means
          nobody has to. */}
      <span aria-hidden className={cn('size-2.5 shrink-0 rounded-[3px]', fill)} />
      <dt className="text-muted-foreground">{label}</dt>
      <dd className={cn('font-medium tabular-nums', value === 0 && 'text-dim-foreground')}>
        {formatCount(value)} · {formatPercent(total > 0 ? Math.round((value / total) * 100) : 0)}
      </dd>
    </div>
  )
}

/** The gate scored nothing — because nothing crossed a rule, or because nothing arrived at all. */
function NothingScored({ funnel }: { funnel: Funnel }) {
  const t = useT().telemetry.funnel

  return (
    <p className="text-muted-foreground text-sm">
      {funnel.logRecords > 0 ? t.nothingScored : t.nothingArrived}
    </p>
  )
}

function StagesCard({ funnel }: { funnel: Funnel }) {
  const t = useT().telemetry.funnel

  const stages: BarRow[] = [
    {
      key: 'logRecords',
      label: t.stageLogRecords,
      value: funnel.logRecords,
      display: formatCount(funnel.logRecords),
    },
    {
      key: 'signatures',
      label: t.stageSignatures,
      value: funnel.signatures,
      display: formatCount(funnel.signatures),
    },
    {
      key: 'signals',
      label: t.stageSignals,
      value: funnel.signals,
      display: formatCount(funnel.signals),
    },
  ]

  return (
    <Card className="h-full">
      <CardHeader>
        <CardTitle>{t.stagesTitle}</CardTitle>
      </CardHeader>

      <CardContent className="space-y-4">
        <BarRows
          rows={stages}
          label={t.stagesChartLabel(
            formatCount(funnel.logRecords),
            formatCount(funnel.signatures),
            formatCount(funnel.signals),
          )}
        />

        <Severities funnel={funnel} />
      </CardContent>
    </Card>
  )
}

function Severities({ funnel }: { funnel: Funnel }) {
  const { labels, telemetry } = useT()

  // The rollup comes back keyed and sparse, so the reading order is imposed here. Anything the
  // server sends that this file has not been taught still renders, at the end: a severity dropped
  // because it was unrecognised would make the counts above stop adding up.
  const known = logSeverities.filter((severity) => severity in funnel.logRecordsBySeverity)
  const unknown = Object.keys(funnel.logRecordsBySeverity).filter(
    (key) => !(logSeverities as string[]).includes(key),
  )

  const rows = [...known, ...unknown]

  return (
    <div className="space-y-1.5">
      <p className="text-muted-foreground text-[0.6875rem] font-medium tracking-wider uppercase">
        {telemetry.funnel.whatArrived}
      </p>

      {rows.length === 0 ? (
        <p className="text-muted-foreground text-sm">{telemetry.funnel.noLogRecord}</p>
      ) : (
        <dl className="flex flex-wrap items-baseline gap-x-5 gap-y-1 text-sm">
          {rows.map((key) => {
            const count = funnel.logRecordsBySeverity[key] ?? 0

            return (
              <div key={key} className="flex items-baseline gap-1.5">
                <dt className={cn('text-sm', severityClass[key as LogSeverity])}>
                  {labels.severity[key as LogSeverity] ?? key}
                </dt>
                <dd className={cn('font-medium tabular-nums', count === 0 && 'text-dim-foreground')}>
                  {formatCount(count)}
                </dd>
              </div>
            )
          })}
        </dl>
      )}
    </div>
  )
}

// Grouped by the one distinction the number above rests on, in that order. Promoted and
// Deduplicated are what "acted on" means; the other three are exactly what "not raised" counts.
// The headings are keyed rather than spelled: the grouping is the screen's whole argument, and a
// group that lost its words in one language would lose the argument with them.
const verdictGroups: { id: 'woken' | 'notWoken'; bands: SignalStatus[] }[] = [
  { id: 'woken', bands: ['Promoted', 'Deduplicated'] },
  { id: 'notWoken', bands: ['Weak', 'Recorded', 'Suppressed'] },
]

function VerdictsCard({ funnel }: { funnel: Funnel }) {
  const t = useT().telemetry.funnel

  const groupHeading = { woken: t.wokenHeading, notWoken: t.notWokenHeading }

  return (
    <Card className="h-full">
      <CardHeader>
        <CardTitle>{t.verdictsTitle}</CardTitle>
      </CardHeader>

      <CardContent className="space-y-4">
        {verdictGroups.map((group) => (
          <div key={group.id} className="space-y-2">
            <p className="text-[0.6875rem] font-medium tracking-wider uppercase">
              {groupHeading[group.id]}
            </p>

            <ul className="space-y-2.5">
              {group.bands.map((band) => (
                <Verdict
                  key={band}
                  band={band}
                  count={funnel.signalsByStatus[band] ?? 0}
                  total={funnel.signals}
                />
              ))}
            </ul>
          </div>
        ))}
      </CardContent>
    </Card>
  )
}

function Verdict({ band, count, total }: { band: SignalStatus; count: number; total: number }) {
  const { labels } = useT()

  const share = total > 0 ? Math.round((count / total) * 100) : 0

  return (
    <li className="space-y-1">
      <div className="flex items-baseline justify-between gap-3">
        <Badge variant="outline" className={cn('border', signalStatusClass[band])}>
          {labels.signalStatus[band]}
        </Badge>

        <span
          className={cn(
            'shrink-0 text-sm tabular-nums',
            count === 0 ? 'text-dim-foreground' : 'text-muted-foreground',
          )}
        >
          {formatCount(count)} · {formatPercent(share)}
        </span>
      </div>

      {/* One neutral fill for all five. The badge beside it already carries the verdict, and a bar
          that carried it too would put a quantity and a judgement on the same channel — the thing
          the signal map keeps apart on purpose. */}
      <div aria-hidden className="bg-muted h-1.5 w-full overflow-hidden rounded-full">
        <div className="bg-foreground/55 h-full rounded-full" style={{ width: `${share}%` }} />
      </div>

    </li>
  )
}
