import { useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'

import { telemetryApi } from '@/api/endpoints'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
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
  signalStatusClass,
  signalStatusLabel,
} from '@/lib/format'
import { cn } from '@/lib/utils'
import { defaultWindow, resolveWindow, windowLabel, windowPresets } from '@/lib/window'
import type { Signal } from '@/types/api'

import { buildHeatMap, errorKeyOf, otherColumn } from './heatmap'
import { SignalHeatMap } from './SignalHeatMap'

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

  // Built once and shared, so the list filters by exactly the columns the map drew.
  const map = buildHeatMap(signals)
  const named = new Set(map.columns.filter((column) => column !== otherColumn))

  const visible = selected
    ? signals.filter((signal) => {
        if (signal.service !== selected.service) return false

        const key = errorKeyOf(signal)

        // "other" is defined by exclusion — it holds everything that did not earn a column of
        // its own, so it cannot be matched by name.
        return selected.errorKey === otherColumn ? !named.has(key) : key === selected.errorKey
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
          <CardContent className="text-destructive py-6 text-sm">
            {query.error instanceof Error ? query.error.message : 'Could not load signals'}
          </CardContent>
        </Card>
      )}

      {query.isSuccess && (
        <>
          <SignalHeatMap map={map} selected={selected} onSelect={select} />

          <Card>
            <CardHeader className="flex-row items-center justify-between space-y-0">
              <CardTitle className="text-base">
                {selected ? `${selected.service} · ${selected.errorKey}` : 'All signals'}
              </CardTitle>

              {selected && (
                <button
                  type="button"
                  onClick={() => select(null)}
                  className="text-muted-foreground text-sm hover:underline"
                >
                  Clear filter
                </button>
              )}
            </CardHeader>

            <CardContent>
              {visible.length === 0 ? (
                <p className="text-muted-foreground py-6 text-center text-sm">
                  No signals here.
                </p>
              ) : (
                <ul className="divide-y">
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

function SignalRow({ signal }: { signal: Signal }) {
  // "total" is the sum the components add up to, not a component. Showing it alongside them
  // would make the arithmetic look wrong.
  const components = Object.entries(signal.scoreBreakdown)
    .filter(([key]) => key !== 'total')
    .sort((a, b) => b[1] - a[1])

  return (
    <li className="space-y-2 py-3">
      <div className="flex flex-wrap items-center gap-2">
        <Badge variant="outline" className={cn('border', signalStatusClass[signal.status])}>
          {signalStatusLabel[signal.status]}
        </Badge>

        <span className="font-medium">
          {signal.service ?? 'unknown service'}
          {signal.exceptionType && (
            <span className="text-muted-foreground"> · {signal.exceptionType}</span>
          )}
        </span>

        <span className="text-muted-foreground text-sm tabular-nums">
          {formatConfidence(signal.confidence)} · {signal.occurrenceCount} occurrence(s)
        </span>

        <span className="text-muted-foreground ml-auto text-sm tabular-nums">
          {formatRelative(signal.detectedAt)}
        </span>
      </div>

      {signal.normalizedMessage && (
        <p className="text-muted-foreground truncate text-sm">{signal.normalizedMessage}</p>
      )}

      {components.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {components.map(([name, value]) => (
            <span
              key={name}
              className={cn(
                'rounded border px-1.5 py-0.5 text-xs tabular-nums',
                value < 0 ? 'text-destructive border-destructive/30' : 'text-muted-foreground',
              )}
            >
              {name} {formatScore(value)}
            </span>
          ))}
        </div>
      )}

      {signal.reason && <p className="text-muted-foreground text-sm">{signal.reason}</p>}

      {signal.incidentId && (
        <Link
          to={`/incidents/${signal.incidentId}`}
          className="text-sm font-medium hover:underline"
        >
          View incident →
        </Link>
      )}
    </li>
  )
}
