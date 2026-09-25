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
import { useT } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { isPushed, maskedValue, type TelemetrySource, type TelemetrySourceKind } from '@/types/api'

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
 *
 * A pushed kind has no schedule, so the interval and the sentence it drives are not shown; what it
 * has instead is a key, which the server issues on creation and the page shows once.
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
  /** The saved source — for a new pushed one, with its key, which the page shows next. */
  onSaved: (saved: TelemetrySource) => void
}) {
  const dictionary = useT()
  const { labels, settings } = dictionary
  const t = settings.telemetry
  const kindName = labels.telemetryKind[kind]
  const pushed = isPushed(kind)

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
        settings.config.straySource,
      ),
    [kind, stored, settings.config.straySource],
  )

  const parsedPoll = Number(pollInterval.trim())
  const pollIsValid =
    pushed ||
    (Number.isInteger(parsedPoll) && parsedPoll >= minimumPollSeconds && pollInterval.trim() !== '')

  const save = useMutation({
    mutationFn: () => {
      const input: TelemetrySourceInput = {
        name: name.trim(),
        config: buildConfig(fields, values, stored),
        // Nothing polls a pushed source; the server keeps its default.
        pollIntervalSeconds: pushed ? undefined : parsedPoll,
      }

      return source
        ? telemetrySourcesApi.update(source.id, input)
        : telemetrySourcesApi.create({ ...input, kind, isEnabled: true })
    },
    onSuccess: (saved) => {
      toast.success(source ? t.updated : t.connected(kindName))
      onSaved(saved)
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
              <DialogTitle>
                {source ? t.editTitle(kindName) : t.connectTitle(kindName)}
              </DialogTitle>
              <DialogDescription>
                {source ? settings.shared.secretKept : entry && t.summary[entry.kind]}
              </DialogDescription>
            </div>
          </div>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="source-name">{settings.shared.name}</Label>
            <Input
              id="source-name"
              value={name}
              placeholder={t.namePlaceholder(kindName)}
              onChange={(event) => setName(event.target.value)}
            />
            <p className="text-muted-foreground text-xs">{t.nameHint}</p>
          </div>

          <ConfigFields
            fields={fields}
            values={values}
            stored={stored}
            onChange={(key, value) => setValues((current) => ({ ...current, [key]: value }))}
          />

          {!pushed && (
          <div className="space-y-2">
            <div className="space-y-1.5">
              <Label htmlFor="poll">{t.pollLabel}</Label>
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
                ? describeDraftSchedule(dictionary, parsedPoll, values.Filter ?? '')
                : t.pollInvalid(minimumPollSeconds)}
            </p>
          </div>
          )}

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
            {settings.shared.cancel}
          </Button>
          <Button
            disabled={!name.trim() || !pollIsValid || save.isPending}
            onClick={() => save.mutate()}
          >
            {save.isPending
              ? settings.shared.saving
              : source
                ? settings.shared.saveChanges
                : settings.shared.connect}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
