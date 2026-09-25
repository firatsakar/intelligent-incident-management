import { CheckIcon, CopyIcon, KeyRoundIcon } from 'lucide-react'
import { useState } from 'react'

import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { useT } from '@/lib/i18n'
import type { TelemetrySource } from '@/types/api'

/** The header the OTLP endpoint reads the key from — OtlpController.IngestKeyHeader. */
export const ingestKeyHeader = 'X-IIM-Ingest-Key'

/**
 * Where a pushed source receives. The console and the gateway share an origin — in development
 * Vite forwards /otlp as it does /api — so this is the address a collector should be given.
 * A base, not the full path: OTLP exporters append /v1/logs themselves.
 */
export const otlpEndpoint = () => `${window.location.origin}/otlp`

/**
 * The one moment the ingest key exists in the open.
 *
 * Only its hash is stored, so this screen cannot be reopened later; everything a person needs to
 * point a sender at the source is therefore here, on one screen, next to the key itself — the
 * endpoint, the header it goes in, and a working collector config with both filled in. A key shown
 * alone would send them to documentation with the one copy of it on their clipboard.
 */
export function IngestKeyDialog({
  source,
  onClose,
}: {
  source: TelemetrySource
  onClose: () => void
}) {
  const t = useT().settings.telemetry.keyPanel
  const key = source.ingestKey ?? ''
  const endpoint = otlpEndpoint()

  // The filter processor drops what matches: everything below ERROR, before it leaves the
  // customer's network.
  const collector = `processors:
  batch: {}
  filter/errors:
    logs:
      log_record:
        - severity_number < SEVERITY_NUMBER_ERROR

exporters:
  otlphttp/iim:
    endpoint: ${endpoint}
    headers:
      ${ingestKeyHeader}: ${key}

service:
  pipelines:
    logs:
      receivers: [otlp]
      processors: [filter/errors, batch]
      exporters: [otlphttp/iim]`

  const sdk = `OTEL_EXPORTER_OTLP_LOGS_ENDPOINT=${endpoint}/v1/logs
OTEL_EXPORTER_OTLP_LOGS_PROTOCOL=http/protobuf
OTEL_EXPORTER_OTLP_LOGS_HEADERS=${ingestKeyHeader}=${key}`

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <div className="flex items-center gap-3">
            <span className="bg-muted text-foreground grid size-9 shrink-0 place-items-center rounded-lg">
              <KeyRoundIcon className="size-5" aria-hidden />
            </span>

            <div className="min-w-0">
              <DialogTitle>{t.title(source.name)}</DialogTitle>
              <DialogDescription>{t.once}</DialogDescription>
            </div>
          </div>
        </DialogHeader>

        {/* min-w-0: the dialog is a grid, and a grid item is as wide as its widest line unless told
            otherwise — the collector config would otherwise widen the whole dialog past a phone. */}
        <div className="min-w-0 space-y-4">
          <Copyable label={t.keyLabel} value={key} hint={t.headerHint(ingestKeyHeader)} />
          <Copyable label={t.endpointLabel} value={endpoint} hint={t.endpointHint} />
          <Copyable label={t.collectorLabel} value={collector} hint={t.collectorHint} block />
          <Copyable label={t.sdkLabel} value={sdk} hint={t.sdkHint} block />
        </div>

        <DialogFooter>
          <Button onClick={onClose}>{t.done}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function Copyable({
  label,
  value,
  hint,
  block = false,
}: {
  label: string
  value: string
  hint: string
  block?: boolean
}) {
  const t = useT().settings.telemetry.keyPanel
  const [copied, setCopied] = useState(false)

  const copy = async () => {
    await navigator.clipboard.writeText(value)
    setCopied(true)
    window.setTimeout(() => setCopied(false), 1500)
  }

  return (
    <div className="min-w-0 space-y-1.5">
      <div className="flex items-center justify-between gap-2">
        <span className="text-sm font-medium">{label}</span>
        <Button variant="ghost" size="sm" onClick={() => void copy()} aria-label={`${t.copy}: ${label}`}>
          {copied ? <CheckIcon aria-hidden /> : <CopyIcon aria-hidden />}
          <span aria-live="polite">{copied ? t.copied : t.copy}</span>
        </Button>
      </div>

      {block ? (
        <pre className="bg-muted overflow-x-auto rounded-md px-3 py-2 font-mono text-xs leading-relaxed">
          {value}
        </pre>
      ) : (
        <p className="bg-muted rounded-md px-3 py-2 font-mono text-xs break-all">{value}</p>
      )}

      <p className="text-muted-foreground text-xs">{hint}</p>
    </div>
  )
}
