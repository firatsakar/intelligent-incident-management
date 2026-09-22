import { useMutation } from '@tanstack/react-query'
import { AlertTriangleIcon } from 'lucide-react'
import { useMemo, useState } from 'react'
import { toast } from 'sonner'

import { telemetrySourcesApi, type TelemetrySourceInput } from '@/api/endpoints'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { cn } from '@/lib/utils'
import { maskedValue, type TelemetrySource, type TelemetrySourceKind } from '@/types/api'

import { buildConfig, ConfigFields, withStrayFields } from './ConfigFields'
import { telemetrySourceFields } from './configSchema'
import { connectable, describeDraftSchedule } from './telemetryCatalogue'

/** The server rejects anything faster, and says why. Refusing it here saves the round trip. */
const minimumPollSeconds = 5

/**
 * Connect one log store, or edit one that exists.
 *
 * The kind is not a field, for the same reason the channel is not one on the integrations dialog:
 * it arrives from the tile that was pressed, and it was never editable after creation anyway.
 */
export function TelemetrySourceDialog({
  kind,
  source,
  onClose,
  onSaved,
}: {
  kind: TelemetrySourceKind
  /** Null when connecting a new one. */
  source: TelemetrySource | null
  onClose: () => void
  onSaved: () => void
}) {
  const [name, setName] = useState(source?.name ?? '')
  const [pollInterval, setPollInterval] = useState(String(source?.pollIntervalSeconds ?? 15))

  // Secrets start blank rather than pre-filled with the mask, so the field reads as "not shown"
  // instead of as a value somebody might overwrite by accident.
  const [values, setValues] = useState<Record<string, string>>(() => {
    const initial: Record<string, string> = {}

    for (const [key, value] of Object.entries(source?.config ?? {})) {
      if (value !== maskedValue) initial[key] = value
    }

    return initial
  })

  const entry = connectable.find((candidate) => candidate.kind === kind)
  const stored = source?.config ?? null

  const fields = useMemo(
    () =>
      withStrayFields(
        telemetrySourceFields[kind],
        stored,
        'Stored on this source; this form does not know its shape.',
      ),
    [kind, stored],
  )

  const parsedPoll = Number(pollInterval.trim())
  const pollIsValid =
    Number.isInteger(parsedPoll) && parsedPoll >= minimumPollSeconds && pollInterval.trim() !== ''

  const save = useMutation({
    mutationFn: () => {
      const input: TelemetrySourceInput = {
        name: name.trim(),
        config: buildConfig(fields, values, stored),
        pollIntervalSeconds: parsedPoll,
      }

      return source
        ? telemetrySourcesApi.update(source.id, input)
        : telemetrySourcesApi.create({ ...input, kind, isEnabled: true })
    },
    onSuccess: () => {
      toast.success(source ? 'Source updated' : `${kind} connected`)
      onSaved()
    },
  })

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <div className="flex items-center gap-3">
            <span className="bg-muted text-foreground grid size-9 shrink-0 place-items-center rounded-lg">
              {entry && <entry.mark className="size-5" />}
            </span>

            <div className="min-w-0">
              <DialogTitle>{source ? `Edit ${kind} source` : `Connect ${kind}`}</DialogTitle>
              <DialogDescription>
                {source ? 'A secret left blank keeps the value already stored.' : entry?.summary}
              </DialogDescription>
            </div>
          </div>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="source-name">Name</Label>
            <Input
              id="source-name"
              value={name}
              placeholder={`${kind} — production`}
              onChange={(event) => setName(event.target.value)}
            />
            <p className="text-muted-foreground text-xs">
              How this source is identified on the telemetry page and in detection logs.
            </p>
          </div>

          <ConfigFields
            fields={fields}
            values={values}
            stored={stored}
            onChange={(key, value) => setValues((current) => ({ ...current, [key]: value }))}
          />

          <div className="space-y-2">
            <div className="space-y-1.5">
              <Label htmlFor="poll">Poll interval (seconds)</Label>
              <Input
                id="poll"
                inputMode="numeric"
                value={pollInterval}
                aria-invalid={pollIsValid ? undefined : true}
                aria-describedby="poll-schedule"
                onChange={(event) => setPollInterval(event.target.value)}
              />
            </div>

            {/* The cadence and what it selects, as one sentence that moves while the operator
                types. Two numbers in two boxes do not say what the source will actually do; the
                sentence they add up to does. An invalid interval says so here rather than coming
                back as a rejected save, and rather than silently becoming the server's default. */}
            <p
              id="poll-schedule"
              aria-live="polite"
              className={cn('text-xs', pollIsValid ? 'text-muted-foreground' : 'text-alarm-ink')}
            >
              {pollIsValid
                ? `${describeDraftSchedule(parsedPoll, values.Filter ?? '')}.`
                : `Enter a whole number of seconds, ${minimumPollSeconds} or more. Polling faster than that hammers the source for no benefit.`}
            </p>
          </div>

          {/* Inline rather than a toast: a rejected save is usually a missing setting, and the
              operator needs to read it while looking at the field it names. */}
          {save.isError && (
            <p
              role="alert"
              className="border-alarm-border/70 bg-alarm/10 text-alarm-ink flex items-start gap-2 rounded-md border px-2.5 py-2 text-xs"
            >
              <AlertTriangleIcon className="mt-px size-3.5 shrink-0" aria-hidden />
              <span className="min-w-0 break-words">{save.error.message}</span>
            </p>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button
            disabled={!name.trim() || !pollIsValid || save.isPending}
            onClick={() => save.mutate()}
          >
            {save.isPending ? 'Saving…' : source ? 'Save changes' : 'Connect'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
