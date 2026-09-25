import { KeyRoundIcon } from 'lucide-react'

import { Copyable } from '@/components/Copyable'
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
