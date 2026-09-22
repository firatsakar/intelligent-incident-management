import { useMutation } from '@tanstack/react-query'
import { AlertTriangleIcon } from 'lucide-react'
import { useMemo, useState } from 'react'
import { toast } from 'sonner'

import { integrationsApi, type IntegrationInput } from '@/api/endpoints'
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  incidentPriorities,
  maskedValue,
  type IncidentPriority,
  type Integration,
  type NotificationChannelType,
} from '@/types/api'

import { buildConfig, ConfigFields, withStrayFields } from './ConfigFields'
import { integrationFields } from './configSchema'
import { connectable, describeFilters } from './integrationCatalogue'

const anyValue = 'any'

/**
 * Connect one destination, or edit one that exists.
 *
 * The channel is no longer a field. It arrives from the tile the operator pressed, which is the
 * catalogue's whole payoff: the dialog opens already knowing what it is asking for, and the one
 * control on the old form that could never be changed after creation is gone.
 */
export function IntegrationDialog({
  channel,
  integration,
  onClose,
  onSaved,
}: {
  channel: NotificationChannelType
  /** Null when connecting a new one. */
  integration: Integration | null
  onClose: () => void
  onSaved: () => void
}) {
  const [name, setName] = useState(integration?.name ?? '')
  const [minPriority, setMinPriority] = useState<string>(integration?.minPriority ?? anyValue)
  const [categoryFilter, setCategoryFilter] = useState(integration?.categoryFilter ?? '')

  // Secrets start blank rather than pre-filled with the mask, so the field reads as "not shown"
  // instead of as a value somebody might overwrite by accident.
  const [values, setValues] = useState<Record<string, string>>(() => {
    const initial: Record<string, string> = {}

    for (const [key, value] of Object.entries(integration?.config ?? {})) {
      if (value !== maskedValue) initial[key] = value
    }

    return initial
  })

  const entry = connectable.find((candidate) => candidate.channel === channel)
  const stored = integration?.config ?? null

  const fields = useMemo(
    () =>
      withStrayFields(
        integrationFields[channel],
        stored,
        'Stored on this integration; this form does not know its shape.',
      ),
    [channel, stored],
  )

  const save = useMutation({
    mutationFn: () => {
      const input: IntegrationInput = {
        name: name.trim(),
        config: buildConfig(fields, values, stored),
        minPriority: minPriority === anyValue ? null : (minPriority as IncidentPriority),
        categoryFilter: categoryFilter.trim() || null,
      }

      return integration
        ? integrationsApi.update(integration.id, input)
        : integrationsApi.create({ ...input, channel, isEnabled: true })
    },
    onSuccess: () => {
      toast.success(integration ? 'Integration updated' : `${channel} connected`)
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
              <DialogTitle>
                {integration ? `Edit ${channel} integration` : `Connect ${channel}`}
              </DialogTitle>
              <DialogDescription>
                {integration
                  ? 'A secret left blank keeps the value already stored.'
                  : entry?.summary}
              </DialogDescription>
            </div>
          </div>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="integration-name">Name</Label>
            <Input
              id="integration-name"
              value={name}
              placeholder={`${channel} — on-call`}
              onChange={(event) => setName(event.target.value)}
            />
            {/* One channel can hold several integrations, so the name is what tells them apart in
                every list on this screen. */}
            <p className="text-muted-foreground text-xs">
              How this destination is identified on the integrations page.
            </p>
          </div>

          <ConfigFields
            fields={fields}
            values={values}
            stored={stored}
            onChange={(key, value) => setValues((current) => ({ ...current, [key]: value }))}
          />

          <div className="space-y-2">
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label>Minimum priority</Label>
                <Select
                  value={minPriority}
                  onValueChange={(value) => setMinPriority(value ?? anyValue)}
                >
                  <SelectTrigger>
                    <SelectValue>
                      {minPriority === anyValue ? 'Any priority' : minPriority}
                    </SelectValue>
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={anyValue}>Any priority</SelectItem>
                    {incidentPriorities.map((option) => (
                      <SelectItem key={option} value={option}>
                        {option} and above
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="integration-category">Category filter</Label>
                <Input
                  id="integration-category"
                  value={categoryFilter}
                  placeholder="Any category"
                  onChange={(event) => setCategoryFilter(event.target.value)}
                />
              </div>
            </div>

            {/* Two empty controls say nothing; the sentence they add up to says the thing that
                matters, and it moves as the operator types. */}
            <p className="text-muted-foreground text-xs" aria-live="polite">
              {describeFilters(
                minPriority === anyValue ? null : (minPriority as IncidentPriority),
                categoryFilter.trim() || null,
              )}
              .
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
          <Button disabled={!name.trim() || save.isPending} onClick={() => save.mutate()}>
            {save.isPending ? 'Saving…' : integration ? 'Save changes' : 'Connect'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
