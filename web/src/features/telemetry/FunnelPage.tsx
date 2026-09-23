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
import { formatCount, severityClass, signalStatusClass } from '@/lib/format'
import { cn } from '@/lib/utils'
import { useT } from '@/lib/i18n'
import { resolveWindowPreset } from '@/lib/window'
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

  const preset = params.get('window') ?? defaultStatsWindow
  const query = useTelemetryStats(preset)

  const scope = useT().window.scope[resolveWindowPreset(preset)]

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-x-6 gap-y-3">
        <div className="max-w-2xl">
          <h1 className="text-2xl font-semibold tracking-tight">Signal funnel</h1>
          <p className="text-muted-foreground text-sm">
            Everything the platform read, what it folded together, and how much of it it decided
            was not worth waking anybody for.
          </p>
        </div>

        <WindowSelect value={preset} />
      </div>

      {query.isError && (
        <Card>
          <CardContent className="text-alarm-ink py-6 text-sm">
            {query.error instanceof Error ? query.error.message : 'Could not load the funnel'}
          </CardContent>
        </Card>
      )}

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-12">
        <div className="lg:col-span-12">
          {query.data ? (
            <RestraintCard funnel={query.data.funnel} scope={scope} />
          ) : (
            <Skeleton className="h-44 w-full" />
          )}
        </div>

        <div className="lg:col-span-7">
          {query.data ? (
            <StagesCard funnel={query.data.funnel} scope={scope} />
          ) : (
            <Skeleton className="h-80 w-full" />
          )}
        </div>

        <div className="lg:col-span-5">
          {query.data ? (
            <VerdictsCard funnel={query.data.funnel} scope={scope} />
          ) : (
            <Skeleton className="h-80 w-full" />
          )}
        </div>
      </div>
    </div>
  )
}

const plural = (count: number, word: string) => `${word}${count === 1 ? '' : 's'}`

function RestraintCard({ funnel, scope }: { funnel: Funnel; scope: string }) {
  const deduplicated = funnel.signalsByStatus.Deduplicated ?? 0
  const actedOn = funnel.signals - funnel.notRaised

  return (
    <Card>
      <CardHeader>
        <CardTitle>Not raised</CardTitle>
        <CardDescription>
          Signals the gate scored and deliberately left alone — {scope}.
        </CardDescription>
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
                of {formatCount(funnel.signals)} {plural(funnel.signals, 'signal')} the gate scored
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
                label="held back"
                value={funnel.notRaised}
                total={funnel.signals}
              />
              <Split
                fill="bg-foreground/25"
                label="acted on"
                value={actedOn}
                total={funnel.signals}
              />
            </dl>

            <p className="text-muted-foreground max-w-3xl text-sm">
              {funnel.notRaised === 0 ? (
                <>
                  Everything the gate scored in this window, it acted on — all{' '}
                  {formatCount(funnel.signals)} crossed the threshold, so there was nothing left to
                  hold back.{' '}
                  <strong className="text-foreground font-medium">
                    A zero here means the gate refused nothing, not that it looked at nothing.
                  </strong>{' '}
                  What it looked at is the number beside it, and the stages below.
                </>
              ) : (
                <>
                  {formatCount(funnel.notRaised)} of {formatCount(funnel.signals)} were scored and
                  left where they were: no incident, no page, no email. That is the thing an
                  alerting rule cannot do — decide, on arithmetic you can read back, that this one
                  was not worth a human.
                </>
              )}
            </p>

            {deduplicated > 0 && (
              // The exclusion is deliberate on the server and is worth showing rather than
              // hiding: it is the difference between restraint and a claim of restraint.
              <p className="text-muted-foreground max-w-3xl text-sm">
                {formatCount(deduplicated)} deduplicated {plural(deduplicated, 'signal')} count as
                acted on, not as held back. Each was folded into an incident that was already open,
                so somebody was woken — just earlier.
              </p>
            )}
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
        {formatCount(value)} · {total > 0 ? Math.round((value / total) * 100) : 0}%
      </dd>
    </div>
  )
}

/**
 * The gate scored nothing. Two different facts land here and only one of them is about the gate,
 * so they get different sentences rather than a shared "no data".
 */
function NothingScored({ funnel }: { funnel: Funnel }) {
  return (
    <>
      <p className="text-muted-foreground text-sm">Nothing was scored in this window.</p>

      <p className="max-w-3xl text-sm">
        {funnel.logRecords > 0 ? (
          <>
            {formatCount(funnel.logRecords)} log {plural(funnel.logRecords, 'record')} arrived and
            none of them crossed a detection rule, so no burst ever reached the score. The
            filtering here happened a stage earlier than this number measures — the stages below
            are where to read it.
          </>
        ) : (
          <>
            No telemetry arrived in this window at all, so the gate had nothing to look at. A quiet
            window and a source that is not being read look the same from here — Settings ›
            Telemetry says which.
          </>
        )}
      </p>
    </>
  )
}

function StagesCard({ funnel, scope }: { funnel: Funnel; scope: string }) {
  const stages: BarRow[] = [
    {
      key: 'logRecords',
      label: 'Log records',
      value: funnel.logRecords,
      display: formatCount(funnel.logRecords),
    },
    {
      key: 'signatures',
      label: 'Signatures',
      value: funnel.signatures,
      display: formatCount(funnel.signatures),
    },
    { key: 'signals', label: 'Signals', value: funnel.signals, display: formatCount(funnel.signals) },
  ]

  return (
    <Card className="h-full">
      <CardHeader>
        <CardTitle>From log records to signals</CardTitle>
        <CardDescription>
          The three stages, on one scale — {scope}. They count different things: records are lines
          of log, signatures are distinct fingerprints cut from them, and signals are bursts the
          gate was asked to score.
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-4">
        <BarRows
          rows={stages}
          label={`Pipeline stages. ${formatCount(funnel.logRecords)} log records, ${formatCount(funnel.signatures)} signatures, ${formatCount(funnel.signals)} signals.`}
        />

        <ul className="space-y-2">
          <Transition
            from={funnel.logRecords}
            to={funnel.signatures}
            text={describeFolding(funnel)}
          />
          <Transition from={funnel.signatures} to={funnel.signals} text={describeBursting(funnel)} />
        </ul>

        <Severities funnel={funnel} />
      </CardContent>
    </Card>
  )
}

function Transition({ from, to, text }: { from: number; to: number; text: string }) {
  return (
    <li className="flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
      <span className="shrink-0 text-xs font-medium tabular-nums">
        {formatCount(from)} → {formatCount(to)}
      </span>
      <span className="text-muted-foreground min-w-0 text-sm">{text}</span>
    </li>
  )
}

/** The drop fingerprinting bought, stated as the ratio rather than left to be inferred from a bar
 *  that is two pixels wide. */
function describeFolding(funnel: Funnel): string {
  if (funnel.logRecords === 0) return 'Nothing arrived, so there was nothing to fingerprint.'

  if (funnel.signatures === 0)
    return 'Nothing in this window was fingerprinted, which should not happen — the records arrived without one.'

  const perSignature = Math.round(funnel.logRecords / funnel.signatures)

  return `About ${formatCount(perSignature)} records per signature. That fold is what fingerprinting bought: the gate reasons about ${formatCount(funnel.signatures)} ${plural(funnel.signatures, 'thing')}, not ${formatCount(funnel.logRecords)}.`
}

/** The one stage that can widen, which a funnel drawn without saying so would quietly misreport. */
function describeBursting(funnel: Funnel): string {
  if (funnel.signals === 0)
    return 'No burst crossed a detection rule, so the gate was never asked to score anything.'

  const base = `${formatCount(funnel.signatures)} ${plural(funnel.signatures, 'signature')} produced ${formatCount(funnel.signals)} ${plural(funnel.signals, 'burst')} for the gate to score.`

  return funnel.signals > funnel.signatures
    ? `${base} A signature can fire more than once, which is why this stage is wider than the one above it rather than narrower.`
    : base
}

function Severities({ funnel }: { funnel: Funnel }) {
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
        What arrived
      </p>

      {rows.length === 0 ? (
        <p className="text-muted-foreground text-sm">No log record arrived in this window.</p>
      ) : (
        <dl className="flex flex-wrap items-baseline gap-x-5 gap-y-1 text-sm">
          {rows.map((key) => {
            const count = funnel.logRecordsBySeverity[key] ?? 0

            return (
              <div key={key} className="flex items-baseline gap-1.5">
                <dt className={cn('text-sm', severityClass[key as LogSeverity])}>{key}</dt>
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

/**
 * What each verdict means, in the operator's language rather than the enum's.
 *
 * Says what the gate *did*, not how it scored: the weights live in SignalScoring and a number
 * copied into the frontend is a number that goes stale without anyone noticing.
 */
const verdictMeaning: Record<SignalStatus, string> = {
  Promoted: 'Cleared the line, and the gate opened an incident for it.',
  Deduplicated: 'Folded into an incident that was already open. Somebody was woken — earlier.',
  Weak: 'Scored, and scored under the line. Kept where you can see it; nobody was called.',
  Recorded: 'Kept for the record and nothing more.',
  Suppressed: 'The signature is muted, so the gate scored it and then silenced it on purpose.',
}

// Grouped by the one distinction the number above rests on, in that order. Promoted and
// Deduplicated are what "acted on" means; the other three are exactly what "not raised" counts.
const verdictGroups: { heading: string; note: string; bands: SignalStatus[] }[] = [
  {
    heading: 'Somebody was woken',
    note: 'Not counted as held back.',
    bands: ['Promoted', 'Deduplicated'],
  },
  {
    heading: 'Nobody was woken',
    note: 'These three are what "not raised" counts.',
    bands: ['Weak', 'Recorded', 'Suppressed'],
  },
]

function VerdictsCard({ funnel, scope }: { funnel: Funnel; scope: string }) {
  return (
    <Card className="h-full">
      <CardHeader>
        <CardTitle>How the gate ruled</CardTitle>
        <CardDescription>
          Every verdict the gate can reach, and how many landed on each — {scope}.
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-4">
        {funnel.signals === 0 && (
          // The five stay on screen with their zeros rather than being replaced by one blank
          // sentence: an operator reading this at 3am still learns what the gate can decide, and
          // a list of zeros says "none of these happened" far more precisely than an absence does.
          <p className="text-muted-foreground text-sm">
            The gate scored nothing in this window, so it reached none of these.
          </p>
        )}

        {verdictGroups.map((group) => (
          <div key={group.heading} className="space-y-2">
            <p className="flex flex-wrap items-baseline gap-x-2">
              <span className="text-[0.6875rem] font-medium tracking-wider uppercase">
                {group.heading}
              </span>
              <span className="text-muted-foreground text-xs">{group.note}</span>
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
          {formatCount(count)} · {share}%
        </span>
      </div>

      {/* One neutral fill for all five. The badge beside it already carries the verdict, and a bar
          that carried it too would put a quantity and a judgement on the same channel — the thing
          the signal map keeps apart on purpose. */}
      <div aria-hidden className="bg-muted h-1.5 w-full overflow-hidden rounded-full">
        <div className="bg-foreground/55 h-full rounded-full" style={{ width: `${share}%` }} />
      </div>

      <p className="text-muted-foreground text-xs">{verdictMeaning[band]}</p>
    </li>
  )
}
