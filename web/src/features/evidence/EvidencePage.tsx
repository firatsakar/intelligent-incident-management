import { skipToken, useQuery } from '@tanstack/react-query'
import { XIcon } from 'lucide-react'
import { useSearchParams } from 'react-router-dom'

import { telemetryApi } from '@/api/endpoints'
import { ingestionKey } from '@/app/realtime'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
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
} from '@/lib/format'
import { useT } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import {
  defaultWindow,
  resolveWindow,
  resolveWindowPreset,
  windowPresets,
} from '@/lib/window'
import type { ErrorSignature, IngestionTick, LogRecord, Signal } from '@/types/api'

/**
 * The raw material, in the three shapes it passes through: log lines, the signatures they roll up
 * into, and the signals those produced. Reading down the screen is reading the pipeline forwards,
 * which is why the log column is the wide one — everything to its right is a summary of it.
 */
export function EvidencePage() {
  const { window: windowText, telemetry } = useT()
  const t = telemetry.evidence
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

  /**
   * What ingestion has written since this window was read.
   *
   * `skipToken` rather than a query function: nothing fetches this key. The signal hub writes
   * into it when a poll finishes, and this is only a subscription to that — no request on mount,
   * none on a window change, none ever.
   */
  const ticks =
    useQuery<IngestionTick[]>({ queryKey: ingestionKey, queryFn: skipToken }).data ?? []

  // Ticks older than this screen's own data are already in it. `dataUpdatedAt` moves on every
  // refetch, so the count clears itself when the operator re-reads and needs no state of its own.
  const windowStart = Date.parse(range.from)

  const arrived = query.isSuccess
    ? ticks.filter(
        (tick) =>
          Date.parse(tick.completedAt) > query.dataUpdatedAt &&
          (tick.latestEventAt === null || Date.parse(tick.latestEventAt) >= windowStart),
      )
    : []

  const arrivedRecords = arrived.reduce((sum, tick) => sum + tick.newRecords, 0)

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
          <h1 className="text-2xl font-semibold tracking-tight">{t.title}</h1>
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
              placeholder={t.filterService}
              aria-label={t.filterService}
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
                aria-label={t.clearService}
                className="text-muted-foreground hover:text-foreground focus-visible:ring-ring/50 absolute inset-y-0 right-0 grid w-8 place-items-center rounded-r-lg outline-none focus-visible:ring-[3px]"
              >
                <XIcon className="size-3.5" aria-hidden />
              </button>
            )}
          </div>

          <Select value={preset} onValueChange={(value) => setParam('window', value)}>
            <SelectTrigger className="w-44" aria-label={windowText.label}>
              <SelectValue>{windowText.option[resolveWindowPreset(preset)]}</SelectValue>
            </SelectTrigger>
            <SelectContent>
              {windowPresets.map((option) => (
                <SelectItem key={option.value} value={option.value}>
                  {windowText.option[option.value]}
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
            {query.error instanceof Error ? query.error.message : t.loadError}
          </CardContent>
        </Card>
      )}

      {arrivedRecords > 0 && (
        <Arrivals
          records={arrivedRecords}
          polls={arrived.length}
          service={service}
          refreshing={query.isFetching}
          onRefresh={() => void query.refetch()}
        />
      )}

      {query.isSuccess && (
        <div className="grid gap-6 lg:grid-cols-3">
          {/* min-w-0 for the same reason as the incident screen: a grid item's default min-width
              is min-content, and a log message that never wraps would widen the document rather
              than truncate inside its cell. */}
          <Card className="min-w-0 lg:col-span-2">
            <CardHeader>
              <CardTitle>{t.logRecords}</CardTitle>
              <CardDescription>
                {/* The endpoint caps at 200 but reports the true total, so a truncated view is
                    shown as truncated rather than quietly lying about the volume. */}
                {query.data.logRecords.length < query.data.totalLogRecords
                  ? t.showingRecent(query.data.logRecords.length, query.data.totalLogRecords)
                  : t.inWindow(query.data.totalLogRecords)}
                {service && ` · ${service}`}
              </CardDescription>
            </CardHeader>

            <CardContent>
              {query.data.logRecords.length === 0 ? (
                <Empty
                  title={t.emptyLogTitle}
                  detail={service ? t.emptyLogForService(service) : t.emptyLog}
                />
              ) : (
                // Two hundred fixed-height rows is about three screens of document, and the two
                // panels beside this one end up floating next to whitespace. Its own scroll
                // container keeps the three collections readable against each other, which is the
                // comparison this screen is for.
                //
                // Focusable itself because, unlike the signal list, nothing inside a log line is:
                // without this the box could be read by a mouse and by nothing else.
                <ul
                  tabIndex={0}
                  role="group"
                  aria-label={t.logListLabel}
                  className="divide-border/60 focus-visible:ring-ring/50 -my-1 max-h-[36rem] divide-y overflow-y-auto rounded-sm text-sm outline-none focus-visible:ring-[3px]"
                >
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
                <CardTitle>{t.signatures}</CardTitle>
                {/* The one collection here whose count is not a cap being hit. It is derived from
                    the signals panel below — the distinct signatures those signals point at — so
                    when the signals are truncated this list is truncated with them, and saying
                    "12 in this window" would be claiming a completeness the response never had. */}
                <CardDescription>
                  {t.signaturesCount(query.data.totalSignatures)}
                  {query.data.signals.length < query.data.totalSignals && t.signaturesTruncated}
                  {t.signaturesAllTime}
                </CardDescription>
              </CardHeader>

              <CardContent>
                {query.data.signatures.length === 0 ? (
                  <Empty title={t.emptySignaturesTitle} detail={t.emptySignatures} />
                ) : (
                  <ul
                    tabIndex={0}
                    role="group"
                    aria-label={t.signaturesListLabel}
                    className="divide-border focus-visible:ring-ring/50 -my-2.5 max-h-[28rem] divide-y overflow-y-auto rounded-sm outline-none focus-visible:ring-[3px]"
                  >
                    {query.data.signatures.map((signature) => (
                      <SignatureRow key={signature.id} signature={signature} />
                    ))}
                  </ul>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>{t.signals}</CardTitle>
                {/* Signals were the collection here that never had a cap, so this panel used to
                    be the only one that could not lie about its own size. Now that it has one, it
                    gets the same sentence the log records have had all along. */}
                <CardDescription>
                  {t.signalsDescription} ·{' '}
                  {query.data.signals.length < query.data.totalSignals
                    ? t.signalsRecent(query.data.signals.length, query.data.totalSignals)
                    : t.inWindow(query.data.totalSignals)}
                </CardDescription>
              </CardHeader>

              <CardContent>
                {query.data.signals.length === 0 ? (
                  <Empty title={t.emptySignalsTitle} detail={t.emptySignals} />
                ) : (
                  <ul
                    tabIndex={0}
                    role="group"
                    aria-label={t.signalsListLabel}
                    className="divide-border focus-visible:ring-ring/50 -my-2.5 max-h-[28rem] divide-y overflow-y-auto rounded-sm outline-none focus-visible:ring-[3px]"
                  >
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

/**
 * What has landed since this window was read, and nothing more.
 *
 * The hub sends one summary per poll cycle rather than one message per row, and this screen
 * deliberately stops at showing it. Refetching on arrival would put back exactly the traffic that
 * design removed — a poll writes up to two hundred records every few seconds, per source, and
 * every open client would ask for the window again each time. So the count sits here and the
 * operator decides, which is also the only behaviour that does not move a log line out from under
 * somebody in the middle of reading it.
 *
 * Not a toast: a toast is gone in four seconds and this is a standing fact about the screen.
 */
function Arrivals({
  records,
  polls,
  service,
  refreshing,
  onRefresh,
}: {
  records: number
  polls: number
  service: string
  refreshing: boolean
  onRefresh: () => void
}) {
  const t = useT().telemetry.evidence

  return (
    <div
      // Assertive would interrupt; this is news that can wait for a pause.
      aria-live="polite"
      className="bg-info border-info-border text-info-foreground flex flex-wrap items-center gap-x-3 gap-y-2 rounded-lg border px-3 py-2 text-sm"
    >
      <span className="min-w-0 tabular-nums">
        {polls > 1 ? t.arrivedAcrossPolls(records, polls) : t.arrived(records)}
        {/* The tick counts records, not records matching a filter: the summary is per source, and
            the service a record belongs to is not in it. Saying so beats a number that silently
            means something else than the panel below it. */}
        {service && t.allServicesNote}
      </span>

      <Button
        variant="outline"
        size="sm"
        disabled={refreshing}
        onClick={onRefresh}
        className="ml-auto"
      >
        {refreshing ? t.rereading : t.reread}
      </Button>
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
  const { labels, telemetry } = useT()

  return (
    <li className="flex items-baseline gap-3 py-1.5">
      <span className="text-muted-foreground shrink-0 tabular-nums">
        {formatTime(record.timestamp)}
      </span>

      <span className={cn('w-[5.5rem] shrink-0 text-xs', severityClass[record.severity])}>
        {labels.severity[record.severity]}
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
          {telemetry.evidence.clockSkew}
        </Badge>
      )}

      {/* Ingestion lag: the source's clock to ours, per line. The same subtraction the incident
          screen calls detection latency, one stage earlier — and until now the only field on this
          record that nothing displayed. Its own column so it reads down the list. */}
      <span
        className="text-dim-foreground hidden w-16 shrink-0 text-right text-xs tabular-nums md:block"
        title={telemetry.evidence.ingestionLag}
      >
        {telemetry.evidence.lag(formatDuration(record.timestamp, record.ingestedAt))}
      </span>
    </li>
  )
}

function SignatureRow({ signature }: { signature: ErrorSignature }) {
  const t = useT().telemetry.evidence

  return (
    <li className="space-y-1 py-2.5">
      <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
        <span className="min-w-0 flex-1 truncate text-sm font-medium">{signature.service}</span>

        {signature.isMuted && (
          // A muted signature is still scored and still counted; it is just heavily penalised.
          // Worth a marker here because it explains a low confidence downstream.
          <Badge variant="outline" className="shrink-0">
            {t.muted}
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
        {t.signatureCounts(
          signature.occurrenceCount,
          signature.promotionCount,
          signature.confirmedRealCount,
          signature.falsePositiveCount,
        )}
      </p>
    </li>
  )
}

function SignalSummary({ signal }: { signal: Signal }) {
  const { labels, telemetry } = useT()

  return (
    <li className="space-y-1 py-2.5">
      <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
        <Badge variant="outline" className={cn('border', signalStatusClass[signal.status])}>
          {labels.signalStatus[signal.status]}
        </Badge>

        <span className="ml-auto text-sm font-medium tabular-nums">
          {formatConfidence(signal.confidence)}
        </span>
      </div>

      {/* Which error this signal was about. Without it the row was a verdict attached to nothing,
          which made the panel impossible to read against the signatures directly above it. */}
      <p className="text-muted-foreground truncate text-sm" title={signal.normalizedMessage ?? ''}>
        {signal.exceptionType ?? signal.normalizedMessage ?? telemetry.unknownError}
      </p>

      <p className="text-dim-foreground text-xs tabular-nums">
        {telemetry.evidence.signalCounts(
          signal.occurrenceCount,
          formatDateTime(signal.detectedAt),
        )}
      </p>
    </li>
  )
}
