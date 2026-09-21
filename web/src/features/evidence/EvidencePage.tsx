import { useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'

import { telemetryApi } from '@/api/endpoints'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
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
  formatDateTime,
  formatTime,
  severityClass,
  signalStatusClass,
  signalStatusLabel,
} from '@/lib/format'
import { cn } from '@/lib/utils'
import { defaultWindow, resolveWindow, windowLabel, windowPresets } from '@/lib/window'

export function EvidencePage() {
  const [params, setParams] = useSearchParams()

  const preset = params.get('window') ?? defaultWindow
  const service = params.get('service') ?? ''

  const range = resolveWindow(preset)

  const query = useQuery({
    queryKey: ['evidence', { window: preset, service }],
    queryFn: () =>
      telemetryApi.evidence({
        from: range.from,
        to: range.to,
        service: service || undefined,
      }),
  })

  function setParam(key: string, value: string | null | undefined) {
    const next = new URLSearchParams(params)

    if (!value) next.delete(key)
    else next.set(key, value)

    setParams(next)
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Evidence</h1>
          <p className="text-muted-foreground text-sm">
            The raw material: what was logged, what it rolled up into, and what that produced.
          </p>
        </div>

        <div className="flex gap-2">
          <Input
            defaultValue={service}
            placeholder="Filter by service"
            className="w-48"
            onBlur={(event) => setParam('service', event.target.value.trim())}
            onKeyDown={(event) => {
              if (event.key === 'Enter') setParam('service', event.currentTarget.value.trim())
            }}
          />

          <Select value={preset} onValueChange={(value) => setParam('window', value)}>
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
      </div>

      {query.isPending && <Skeleton className="h-96 w-full" />}

      {query.isError && (
        <Card>
          <CardContent className="text-destructive py-6 text-sm">
            {query.error instanceof Error ? query.error.message : 'Could not load evidence'}
          </CardContent>
        </Card>
      )}

      {query.isSuccess && (
        <div className="grid gap-6 lg:grid-cols-3">
          <Card className="lg:col-span-2">
            <CardHeader>
              <CardTitle className="text-base">Log records</CardTitle>
              <CardDescription>
                {/* The endpoint caps at 200 but reports the true total, so a truncated view is
                    shown as truncated rather than quietly lying about the volume. */}
                {query.data.logRecords.length < query.data.totalLogRecords
                  ? `Showing the most recent ${query.data.logRecords.length} of ${query.data.totalLogRecords}`
                  : `${query.data.totalLogRecords} in this window`}
              </CardDescription>
            </CardHeader>

            <CardContent>
              {query.data.logRecords.length === 0 ? (
                <p className="text-muted-foreground py-6 text-center text-sm">
                  Nothing logged in this window.
                </p>
              ) : (
                <ul className="divide-y text-sm">
                  {query.data.logRecords.map((record) => (
                    <li key={record.id} className="flex gap-3 py-2">
                      <span className="text-muted-foreground shrink-0 tabular-nums">
                        {formatTime(record.timestamp)}
                      </span>
                      <span className={cn('w-20 shrink-0', severityClass[record.severity])}>
                        {record.severity}
                      </span>
                      <span className="text-muted-foreground w-36 shrink-0 truncate">
                        {record.service}
                      </span>
                      <span className="min-w-0 flex-1 truncate" title={record.message}>
                        {record.message}
                      </span>
                      {record.hasClockSkew && (
                        // Recorded rather than rejected upstream, so it should be visible here.
                        <Badge variant="outline" className="shrink-0 text-xs">
                          clock skew
                        </Badge>
                      )}
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>

          <div className="space-y-6">
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Signatures</CardTitle>
                <CardDescription>
                  Distinct errors behind those lines, with their running counters.
                </CardDescription>
              </CardHeader>

              <CardContent>
                {query.data.signatures.length === 0 ? (
                  <p className="text-muted-foreground text-sm">None in this window.</p>
                ) : (
                  <ul className="space-y-3 text-sm">
                    {query.data.signatures.map((signature) => (
                      <li key={signature.id} className="space-y-1">
                        <div className="flex items-center gap-2">
                          <span className="font-medium">{signature.service}</span>
                          {signature.isMuted && <Badge variant="outline">muted</Badge>}
                        </div>
                        <p className="text-muted-foreground truncate">
                          {signature.exceptionType ?? signature.normalizedMessage}
                        </p>
                        <p className="text-muted-foreground text-xs tabular-nums">
                          {signature.occurrenceCount} total · promoted{' '}
                          {signature.promotionCount}× · {signature.confirmedRealCount} real,{' '}
                          {signature.falsePositiveCount} false
                        </p>
                      </li>
                    ))}
                  </ul>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="text-base">Signals</CardTitle>
              </CardHeader>

              <CardContent>
                {query.data.signals.length === 0 ? (
                  <p className="text-muted-foreground text-sm">None in this window.</p>
                ) : (
                  <ul className="space-y-3 text-sm">
                    {query.data.signals.map((signal) => (
                      <li key={signal.id} className="space-y-1">
                        <div className="flex items-center gap-2">
                          <Badge
                            variant="outline"
                            className={cn('border', signalStatusClass[signal.status])}
                          >
                            {signalStatusLabel[signal.status]}
                          </Badge>
                          <span className="tabular-nums">
                            {formatConfidence(signal.confidence)}
                          </span>
                        </div>
                        <p className="text-muted-foreground text-xs tabular-nums">
                          {signal.occurrenceCount} occurrence(s) ·{' '}
                          {formatDateTime(signal.detectedAt)}
                        </p>
                      </li>
                    ))}
                  </ul>
                )}
              </CardContent>
            </Card>
          </div>
        </div>
      )}
    </div>
  )
}
