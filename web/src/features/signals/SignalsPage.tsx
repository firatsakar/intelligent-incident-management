import { useQuery } from '@tanstack/react-query'
import { ArrowRightIcon } from 'lucide-react'
import { Link, useSearchParams } from 'react-router-dom'

import { telemetryApi } from '@/api/endpoints'
import { Badge } from '@/components/ui/badge'
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import {
  formatConfidence,
  formatRelative,
  formatScore,
  scoreTerm,
  signalStatusClass,
  signalStatusLabel,
} from '@/lib/format'
import { cn } from '@/lib/utils'
import { defaultWindow, resolveWindow, windowLabel, windowPresets } from '@/lib/window'
import type { Signal } from '@/types/api'

import { buildHeatMap, errorKeyOf, otherKey } from './heatmap'
import { SignalHeatMap } from './SignalHeatMap'

/**
 * The same bucket name the map's breadcrumb uses. `other` is the literal in the URL and in the
 * cell key, and printing that literal as a heading was a bug the map had already fixed on its own
 * side — so the wording lives in one expression rather than being spelt out twice.
 */
const describeErrorKey = (key: string) => (key === otherKey ? 'other signatures' : key)

export function SignalsPage() {
  const [params, setParams] = useSearchParams()

  const preset = params.get('window') ?? defaultWindow
  const service = params.get('service')
  const errorKey = params.get('error')

  // Resolved once per render from the preset. The query key carries the resolved instants so a
  // window change is a different key, and a pushed signal writes into the current one.
  const range = resolveWindow(preset)

  const query = useQuery({
    queryKey: ['signals', { window: preset }],
    queryFn: () => telemetryApi.signals({ from: range.from, to: range.to }),
  })

  const signals = query.data ?? []

  const selected = service && errorKey ? { service, errorKey } : null

  function select(cell: { service: string; errorKey: string } | null) {
    const next = new URLSearchParams(params)

    if (cell) {
      next.set('service', cell.service)
      next.set('error', cell.errorKey)
    } else {
      next.delete('service')
      next.delete('error')
    }

    setParams(next)
  }

  // Built once and shared, so the list filters by exactly the tiles the map drew.
  const map = buildHeatMap(signals)
  const named = new Set(map.signatures.filter((signature) => signature !== otherKey))

  const visible = selected
    ? signals.filter((signal) => {
        if (signal.service !== selected.service) return false

        const key = errorKeyOf(signal)

        // "other" is defined by exclusion — it holds everything that did not earn a tile of
        // its own, so it cannot be matched by name.
        return selected.errorKey === otherKey ? !named.has(key) : key === selected.errorKey
      })
    : signals

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Signals</h1>
          <p className="text-muted-foreground text-sm">
            Everything the detection gate looked at — including what it decided not to wake
            anyone for.
          </p>
        </div>

        <Select
          value={preset}
          onValueChange={(value) => {
            const next = new URLSearchParams(params)
            next.set('window', value ?? defaultWindow)
            setParams(next)
          }}
        >
          <SelectTrigger className="w-44">
            <SelectValue>{windowLabel(preset)}</SelectValue>
          </SelectTrigger>
          <SelectContent>
            {windowPresets.map((option) => (
              <SelectItem key={option.value} value={option.value}>
                {option.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {query.isPending && <Skeleton className="h-64 w-full" />}

      {query.isError && (
        <Card>
          <CardContent className="text-alarm-ink py-6 text-sm">
            {query.error instanceof Error ? query.error.message : 'Could not load signals'}
          </CardContent>
        </Card>
      )}

      {query.isSuccess && (
        <>
          <SignalHeatMap map={map} selected={selected} onSelect={select} />

          <Card>
            <CardHeader>
              <CardTitle className="min-w-0 truncate">
                {selected
                  ? `${selected.service} · ${describeErrorKey(selected.errorKey)}`
                  : 'All signals'}
              </CardTitle>

              {/* Stated rather than hidden behind a tooltip on each chip: the component names are
                  the gate's own arithmetic and the operator needs to know that before the numbers
                  mean anything. The per-term glossary is on the incident, where the breakdown is
                  the headline rather than a summary. */}
              <CardDescription className="tabular-nums">
                {visible.length} of {signals.length} in this window · the chips are the gate's
                score components, and the confidence is what they add up to
              </CardDescription>

              {selected && (
                <CardAction>
                  <button
                    type="button"
                    onClick={() => select(null)}
                    className="text-muted-foreground hover:text-foreground focus-visible:ring-ring/50 -mx-1 inline-flex min-h-6 items-center rounded-sm px-1 text-sm outline-none hover:underline focus-visible:ring-[3px]"
                  >
                    Clear filter
                  </button>
                </CardAction>
              )}
            </CardHeader>

            <CardContent>
              {visible.length === 0 ? (
                <div className="py-6 text-center">
                  <p className="text-sm font-medium">
                    {selected ? 'No signals for this tile.' : 'No signals in this window.'}
                  </p>
                  <p className="text-muted-foreground mt-1 text-sm">
                    {selected
                      ? // Two causes, and from here they are indistinguishable: the link may
                        // carry a tile from another window, or the signatures may have been
                        // re-ranked since, which moves the tail in and out of "other".
                        'Nothing in the current window matches this tile. The link may have been made against a different window, or the signatures may have been re-ranked since.'
                      : 'Nothing has crossed a detection rule yet. A quiet window and a source that is not being read look the same here — Settings › Telemetry says which.'}
                  </p>
                </div>
              ) : (
                <ul className="divide-border -my-2 divide-y">
                  {visible
                    .slice()
                    .sort((a, b) => (a.detectedAt < b.detectedAt ? 1 : -1))
                    .map((signal) => (
                      <SignalRow key={signal.id} signal={signal} />
                    ))}
                </ul>
              )}
            </CardContent>
          </Card>
        </>
      )}
    </div>
  )
}

/** `LogBurst` and `RateAnomaly` are fine in JSON and read as identifiers on screen. */
const kindLabel: Record<Signal['kind'], string> = {
  LogBurst: 'burst',
  RateAnomaly: 'rate anomaly',
}

/**
 * One signal, at full size.
 *
 * The map above answers "where is the noise"; this answers "what exactly did the gate decide, and
 * on what arithmetic". So the identity leads — the exception type is what an operator recognises,
 * not the service, which the map has already grouped by — and the score components stay visible
 * rather than collapsing into the single confidence figure. A row that showed only the verdict
 * would be an alerting rule's output, which is the one thing this screen exists not to be.
 */
function SignalRow({ signal }: { signal: Signal }) {
  // "total" is the sum the components add up to, not a component. Showing it alongside them
  // would make the arithmetic look wrong.
  const components = Object.entries(signal.scoreBreakdown)
    .filter(([key]) => key !== 'total')
    .sort((a, b) => b[1] - a[1])

  return (
    <li className="space-y-1.5 py-3">
      <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
        <Badge variant="outline" className={cn('border', signalStatusClass[signal.status])}>
          {signalStatusLabel[signal.status]}
        </Badge>

        <span className="min-w-0 text-sm font-medium break-words">
          {signal.exceptionType ?? signal.normalizedMessage ?? 'unknown error'}
        </span>

        <span className="text-muted-foreground min-w-0 truncate text-xs">
          {signal.service ?? 'unknown service'} · {kindLabel[signal.kind]}
        </span>

        <span className="ml-auto flex shrink-0 items-baseline gap-2 text-xs tabular-nums">
          <span className="text-foreground font-medium">
            {formatConfidence(signal.confidence)}
          </span>
          <span className="text-muted-foreground">{signal.occurrenceCount} occurrence(s)</span>
          <span className="text-dim-foreground">{formatRelative(signal.detectedAt)}</span>
        </span>
      </div>

      {/* Only when it is not already serving as the identity above. */}
      {signal.normalizedMessage && signal.exceptionType && (
        <p className="text-muted-foreground truncate text-sm" title={signal.normalizedMessage}>
          {signal.normalizedMessage}
        </p>
      )}

      {components.length > 0 && (
        <div className="flex flex-wrap items-center gap-1.5">
          {components.map(([name, value]) => (
            <span
              key={name}
              className={cn(
                'rounded border px-1.5 py-0.5 text-xs tabular-nums',
                value < 0
                  ? // A penalty, not a contribution. The tier ink rather than `destructive`,
                    // which in this kit means a destructive action.
                    'border-alarm-border/40 text-alarm-ink'
                  : 'border-border text-muted-foreground',
              )}
            >
              {scoreTerm(name)} {formatScore(value)}
            </span>
          ))}
        </div>
      )}

      {(signal.reason || signal.incidentId) && (
        <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
          {signal.reason && (
            <p className="text-muted-foreground min-w-0 text-xs break-words">{signal.reason}</p>
          )}

          {signal.incidentId && (
            <Link
              to={`/incidents/${signal.incidentId}`}
              className="text-primary focus-visible:ring-ring/50 -mx-1 ml-auto inline-flex min-h-6 shrink-0 items-center gap-1 rounded-sm px-1 text-xs font-medium outline-none hover:underline focus-visible:ring-[3px]"
            >
              View incident
              <ArrowRightIcon className="size-3" aria-hidden />
            </Link>
          )}
        </div>
      )}
    </li>
  )
}
