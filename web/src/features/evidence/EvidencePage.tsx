import { useQuery } from '@tanstack/react-query'
import { XIcon } from 'lucide-react'
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
  formatDuration,
  formatTime,
  severityClass,
  signalStatusClass,
  signalStatusLabel,
} from '@/lib/format'
import { cn } from '@/lib/utils'
import { defaultWindow, resolveWindow, windowLabel, windowPresets } from '@/lib/window'
import type { ErrorSignature, LogRecord, Signal } from '@/types/api'

/**
 * The raw material, in the three shapes it passes through: log lines, the signatures they roll up
 * into, and the signals those produced. Reading down the screen is reading the pipeline forwards,
 * which is why the log column is the wide one — everything to its right is a summary of it.
 */
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
      <div className="flex flex-wrap items-end justify-between gap-x-6 gap-y-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Evidence</h1>
          <p className="text-muted-foreground text-sm">
            The raw material: what was logged, what it rolled up into, and what that produced.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <div className="relative">
            <Input
              // The field is uncontrolled so that typing does not rewrite the URL on every
              // keystroke, but `defaultValue` alone would ignore a service arriving from the
              // browser's back button or a pasted link. Keying on it remounts the field when the
              // URL genuinely changes, which keeps the URL the single source of truth without
              // making this a controlled input.
              key={service}
              defaultValue={service}
              placeholder="Filter by service"
              aria-label="Filter by service"
              className={cn('w-48', service && 'pr-8')}
              onBlur={(event) => setParam('service', event.target.value.trim())}
              onKeyDown={(event) => {
                if (event.key === 'Enter') setParam('service', event.currentTarget.value.trim())
              }}
            />

            {service && (
              <button
                type="button"
                onClick={() => setParam('service', null)}
                aria-label="Clear the service filter"
                className="text-muted-foreground hover:text-foreground focus-visible:ring-ring/50 absolute inset-y-0 right-0 grid w-8 place-items-center rounded-r-lg outline-none focus-visible:ring-[3px]"
              >
                <XIcon className="size-3.5" aria-hidden />
              </button>
            )}
          </div>

          <Select value={preset} onValueChange={(value) => setParam('window', value)}>
            <SelectTrigger className="w-44" aria-label="Time window">
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
          <CardContent className="text-alarm-ink py-6 text-sm">
            {query.error instanceof Error ? query.error.message : 'Could not load evidence'}
          </CardContent>
        </Card>
      )}

      {query.isSuccess && (
        <div className="grid gap-6 lg:grid-cols-3">
          {/* min-w-0 for the same reason as the incident screen: a grid item's default min-width
              is min-content, and a log message that never wraps would widen the document rather
              than truncate inside its cell. */}
          <Card className="min-w-0 lg:col-span-2">
            <CardHeader>
              <CardTitle>Log records</CardTitle>
              <CardDescription>
                {/* The endpoint caps at 200 but reports the true total, so a truncated view is
                    shown as truncated rather than quietly lying about the volume. */}
                {query.data.logRecords.length < query.data.totalLogRecords
                  ? `Showing the most recent ${query.data.logRecords.length} of ${query.data.totalLogRecords}`
                  : `${query.data.totalLogRecords} in this window`}
                {service && ` · ${service}`}
              </CardDescription>
            </CardHeader>

            <CardContent>
              {query.data.logRecords.length === 0 ? (
                <Empty
                  title="Nothing logged in this window."
                  detail={
                    service
                      ? `No record from "${service}" in the window. Either it was quiet, or nothing by that name is being read — the name has to match what the source reports.`
                      : 'Detection reads from your own log store on a schedule, so an empty window means either a quiet period or a source that is not being read.'
                  }
                />
              ) : (
                <ul className="divide-border/60 -my-1 divide-y text-sm">
                  {query.data.logRecords.map((record) => (
                    // With a service pinned, its column is the same word on all two hundred rows.
                    // Dropping it there gives the message the width instead, which is the only
                    // column whose content actually differs.
                    <LogLine key={record.id} record={record} showService={!service} />
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>

          <div className="min-w-0 space-y-6">
            <Card>
              <CardHeader>
                <CardTitle>Signatures</CardTitle>
                <CardDescription>
                  Distinct errors behind those lines, with their running counters. The counters span
                  all time, not this window.
                </CardDescription>
              </CardHeader>

              <CardContent>
                {query.data.signatures.length === 0 ? (
                  <Empty
                    title="No signatures here."
                    detail="A signature is created the first time an error is normalised, so an empty list means nothing in the window was an error."
                  />
                ) : (
                  <ul className="divide-border -my-2.5 divide-y">
                    {query.data.signatures.map((signature) => (
                      <SignatureRow key={signature.id} signature={signature} />
                    ))}
                  </ul>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Signals</CardTitle>
                <CardDescription>
                  What the gate made of those signatures in this window.
                </CardDescription>
              </CardHeader>

              <CardContent>
                {query.data.signals.length === 0 ? (
                  <Empty
                    title="No signals here."
                    detail="Errors were logged but no burst cleared a detection rule, so the gate had nothing to decide."
                  />
                ) : (
                  <ul className="divide-border -my-2.5 divide-y">
                    {query.data.signals.map((signal) => (
                      <SignalSummary key={signal.id} signal={signal} />
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

/** A blank panel reads as broken, so each one says what is absent and what that implies. */
function Empty({ title, detail }: { title: string; detail: string }) {
  return (
    <div className="py-6">
      <p className="text-sm font-medium">{title}</p>
      <p className="text-muted-foreground mt-1 text-sm leading-relaxed">{detail}</p>
    </div>
  )
}

/**
 * One log line, in fixed columns so the eye can run down any of them.
 *
 * The message keeps the native `title` rather than a real tooltip. Two hundred of these render at
 * once, each tooltip is a positioned popup with its own listeners, and the full text is already
 * reachable — this is a log line, not a control, and the operator who needs the whole message is
 * going to the incident for the stack trace anyway.
 */
function LogLine({ record, showService }: { record: LogRecord; showService: boolean }) {
  return (
    <li className="flex items-baseline gap-3 py-1.5">
      <span className="text-muted-foreground shrink-0 tabular-nums">
        {formatTime(record.timestamp)}
      </span>

      <span className={cn('w-[5.5rem] shrink-0 text-xs', severityClass[record.severity])}>
        {record.severity}
      </span>

      {showService && (
        <span className="text-muted-foreground hidden w-36 shrink-0 truncate sm:block">
          {record.service}
        </span>
      )}

      <span className="min-w-0 flex-1 truncate" title={record.message}>
        {record.message}
      </span>

      {record.hasClockSkew && (
        // Recorded rather than rejected upstream, so it should be visible here.
        <Badge variant="outline" className="shrink-0 text-xs">
          clock skew
        </Badge>
      )}

      {/* Ingestion lag: the source's clock to ours, per line. The same subtraction the incident
          screen calls detection latency, one stage earlier — and until now the only field on this
          record that nothing displayed. Its own column so it reads down the list. */}
      <span
        className="text-dim-foreground hidden w-16 shrink-0 text-right text-xs tabular-nums md:block"
        title="Ingestion lag — from the source's timestamp to ours"
      >
        +{formatDuration(record.timestamp, record.ingestedAt)}
      </span>
    </li>
  )
}

function SignatureRow({ signature }: { signature: ErrorSignature }) {
  return (
    <li className="space-y-1 py-2.5">
      <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
        <span className="min-w-0 flex-1 truncate text-sm font-medium">{signature.service}</span>

        {signature.isMuted && (
          // A muted signature is still scored and still counted; it is just heavily penalised.
          // Worth a marker here because it explains a low confidence downstream.
          <Badge variant="outline" className="shrink-0">
            muted
          </Badge>
        )}
      </div>

      <p className="text-muted-foreground truncate text-sm" title={signature.normalizedMessage}>
        {signature.exceptionType ?? signature.normalizedMessage}
      </p>

      {/* Truncated on purpose, head-first, with the whole value in reach: a fingerprint is 32
          hex characters, its prefix is what identifies it by eye, and this column is too narrow
          to carry it whole without pushing the counters onto a third line. The incident's own
          score panel prints it in full. */}
      <p
        className="text-dim-foreground truncate font-mono text-[11px]"
        title={signature.fingerprint}
      >
        {signature.fingerprint}
      </p>

      <p className="text-muted-foreground text-xs tabular-nums">
        {signature.occurrenceCount} total · promoted {signature.promotionCount}× ·{' '}
        {signature.confirmedRealCount} real, {signature.falsePositiveCount} false
      </p>
    </li>
  )
}

function SignalSummary({ signal }: { signal: Signal }) {
  return (
    <li className="space-y-1 py-2.5">
      <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
        <Badge variant="outline" className={cn('border', signalStatusClass[signal.status])}>
          {signalStatusLabel[signal.status]}
        </Badge>

        <span className="ml-auto text-sm font-medium tabular-nums">
          {formatConfidence(signal.confidence)}
        </span>
      </div>

      {/* Which error this signal was about. Without it the row was a verdict attached to nothing,
          which made the panel impossible to read against the signatures directly above it. */}
      <p className="text-muted-foreground truncate text-sm" title={signal.normalizedMessage ?? ''}>
        {signal.exceptionType ?? signal.normalizedMessage ?? 'unknown error'}
      </p>

      <p className="text-dim-foreground text-xs tabular-nums">
        {signal.occurrenceCount} occurrence(s) · {formatDateTime(signal.detectedAt)}
      </p>
    </li>
  )
}
