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
import { cn } from '@/lib/utils'
import { incidentVerdicts, type IncidentStatus, type IncidentVerdict } from '@/types/api'

/**
 * The one question asked when an open incident is closed: was it real?
 *
 * Asked here, once, because this is the moment somebody knows — and because the answer is what
 * the detector learns from (Adım 24). Nothing is preselected: a default would be the answer most
 * people click through to, and a detector trained on click-through learns nothing.
 *
 * Each option says what it does to the detector, in the same breath as what it means, so the
 * choice is made knowing its effect rather than discovering it on the evidence screen later.
 */
export function VerdictDialog({
  status,
  pending,
  onConfirm,
  onCancel,
}: {
  status: IncidentStatus
  pending: boolean
  onConfirm: (verdict: IncidentVerdict) => void
  onCancel: () => void
}) {
  const { incidents, labels } = useT()
  const t = incidents.detail.verdictDialog
  const statusLabel = labels.incidentStatus[status]

  const [verdict, setVerdict] = useState<IncidentVerdict | null>(null)

  return (
    <Dialog open onOpenChange={(open) => !open && onCancel()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t.title}</DialogTitle>
          <DialogDescription>{t.description(statusLabel)}</DialogDescription>
        </DialogHeader>

        <fieldset className="grid gap-2">
          <legend className="sr-only">{t.title}</legend>

          {incidentVerdicts.map((option) => {
            const selected = verdict === option

            return (
              <label
                key={option}
                className={cn(
                  'has-focus-visible:ring-ring/50 flex cursor-pointer flex-col gap-0.5 rounded-lg border px-3 py-2.5 transition-colors has-focus-visible:ring-[3px]',
                  selected ? 'border-primary bg-primary/5' : 'border-border hover:bg-muted/60',
                )}
              >
                <input
                  type="radio"
                  name="incident-verdict"
                  value={option}
                  checked={selected}
                  onChange={() => setVerdict(option)}
                  className="sr-only"
                />
                <span className="text-sm font-medium">{labels.verdict[option]}</span>
                <span className="text-muted-foreground text-xs leading-relaxed">
                  {t.effect[option]}
                </span>
              </label>
            )
          })}
        </fieldset>

        <DialogFooter>
          <Button variant="outline" onClick={onCancel}>
            {t.cancel}
          </Button>
          <Button disabled={!verdict || pending} onClick={() => verdict && onConfirm(verdict)}>
            {t.confirm(statusLabel)}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
