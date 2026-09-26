import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { KeyRoundIcon, PlusIcon, TrashIcon } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { toast } from 'sonner'

import { ApiError } from '@/api/client'
import { incidentApiKeysApi } from '@/api/endpoints'
import { Copyable } from '@/components/Copyable'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
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
import { Skeleton } from '@/components/ui/skeleton'
import { formatDateTime, formatRelative } from '@/lib/format'
import { useT } from '@/lib/i18n'
import type { IncidentApiKey, IssuedIncidentApiKey } from '@/types/api'

import { Notice, SectionHeading } from './SettingsCatalogue'

/** The header the intake endpoint reads the key from — IncidentIntakeController.ApiKeyHeader. */
const incidentApiKeyHeader = 'X-IIM-Api-Key'

/** The console and the gateway share an origin, so this is the address a sender should be given. */
const intakeEndpoint = () => `${window.location.origin}/api/incidents/intake`

/**
 * Where incidents come from when nothing detected them (Adım 27): an organisation's own scripts,
 * pipelines and alerting, each with a named key.
 *
 * It sits under Observability because it is the same question as the telemetry sources above it —
 * how problems reach the platform — answered for systems that already know they have one. There is
 * deliberately no form here to open an incident by hand.
 */
export function IncidentApiSection() {
  const { settings } = useT()
  const t = settings.incidentApi
  const queryClient = useQueryClient()

  const [creating, setCreating] = useState(false)
  // A key just made. Its only appearance: close this and the value is gone.
  const [issued, setIssued] = useState<IssuedIncidentApiKey | null>(null)
  const [deleting, setDeleting] = useState<IncidentApiKey | null>(null)

  const query = useQuery({ queryKey: ['incident-api-keys'], queryFn: incidentApiKeysApi.list })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['incident-api-keys'] })

  const remove = useMutation({
    mutationFn: (id: string) => incidentApiKeysApi.remove(id),
    onSuccess: () => {
      setDeleting(null)
      void invalidate()
      toast.success(t.deleted)
    },
    onError: (error: Error) => toast.error(error.message),
  })

  const keys = query.data ?? []

  return (
    <section className="space-y-3">
      <SectionHeading
        title={t.heading}
        detail={query.isSuccess ? t.count(keys.length) : undefined}
      />

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <KeyRoundIcon className="text-muted-foreground size-4" aria-hidden />
            {t.title}
          </CardTitle>
          <CardDescription>{t.description}</CardDescription>
        </CardHeader>

        <CardContent className="min-w-0 space-y-4">
          <Copyable
            label={t.endpointLabel}
            value={intakeEndpoint()}
            hint={t.endpointHint(incidentApiKeyHeader)}
          />

          {query.isError && (
            <Notice tone="bad">
              {t.loadError} {query.error.message}
            </Notice>
          )}

          {query.isPending ? (
            <Skeleton className="h-14 w-full" />
          ) : keys.length === 0 ? (
            <p className="text-muted-foreground text-sm">{t.empty}</p>
          ) : (
            <ul className="divide-border divide-y rounded-lg border">
              {keys.map((key) => (
                <KeyRow key={key.id} apiKey={key} onDelete={() => setDeleting(key)} />
              ))}
            </ul>
          )}

          <p className="text-muted-foreground text-xs leading-relaxed">{t.rotate}</p>
        </CardContent>

        <CardFooter>
          <Button variant="outline" onClick={() => setCreating(true)}>
            <PlusIcon data-icon="inline-start" aria-hidden />
            {t.create}
          </Button>
        </CardFooter>
      </Card>

      {creating && (
        <CreateDialog
          onClose={() => setCreating(false)}
          onCreated={(key) => {
            setCreating(false)
            void invalidate()
            setIssued(key)
          }}
        />
      )}

      {issued && <IssuedDialog apiKey={issued} onClose={() => setIssued(null)} />}

      {deleting && (
        <DeleteDialog
          apiKey={deleting}
          pending={remove.isPending}
          onCancel={() => setDeleting(null)}
          onConfirm={() => remove.mutate(deleting.id)}
        />
      )}
    </section>
  )
}

function KeyRow({ apiKey, onDelete }: { apiKey: IncidentApiKey; onDelete: () => void }) {
  const t = useT().settings.incidentApi

  return (
    <li className="flex items-center gap-3 px-3 py-2.5 text-sm">
      <div className="min-w-0 flex-1">
        <p className="truncate font-medium">{apiKey.name}</p>
        <p className="text-muted-foreground truncate text-xs">
          <span className="font-mono">{apiKey.keyPrefix}…</span>
          {' · '}
          <span title={formatDateTime(apiKey.createdAt)}>
            {t.createdBy(apiKey.createdBy, formatRelative(apiKey.createdAt))}
          </span>
        </p>
      </div>

      <span
        className="text-muted-foreground hidden text-xs tabular-nums sm:inline"
        title={apiKey.lastUsedAt ? formatDateTime(apiKey.lastUsedAt) : undefined}
      >
        {apiKey.lastUsedAt ? t.lastUsed(formatRelative(apiKey.lastUsedAt)) : t.neverUsed}
      </span>

      <Button variant="ghost" size="icon" aria-label={t.deleteAria(apiKey.name)} onClick={onDelete}>
        <TrashIcon aria-hidden />
      </Button>
    </li>
  )
}

function CreateDialog({
  onClose,
  onCreated,
}: {
  onClose: () => void
  onCreated: (key: IssuedIncidentApiKey) => void
}) {
  const { settings } = useT()
  const t = settings.incidentApi

  const [name, setName] = useState('')
  const [problem, setProblem] = useState<string | null>(null)

  const create = useMutation({
    mutationFn: () => incidentApiKeysApi.create(name.trim()),
    onSuccess: onCreated,
    // The name is checked here before it is sent, so the one 400 left is a name already taken.
    onError: (error: Error) =>
      setProblem(error instanceof ApiError && error.status === 400 ? t.nameTaken : error.message),
  })

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!name.trim()) {
      setProblem(t.nameRequired)
      return
    }

    setProblem(null)
    create.mutate()
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-md">
        <form onSubmit={onSubmit} noValidate className="space-y-4">
          <DialogHeader>
            <DialogTitle>{t.createTitle}</DialogTitle>
            <DialogDescription>{t.createBody}</DialogDescription>
          </DialogHeader>

          <div className="space-y-1.5">
            <Label htmlFor="incident-api-key-name">{settings.shared.name}</Label>
            <Input
              id="incident-api-key-name"
              value={name}
              autoFocus
              maxLength={64}
              placeholder={t.namePlaceholder}
              aria-invalid={problem ? true : undefined}
              onChange={(event) => setName(event.target.value)}
            />
            {problem ? (
              <p role="alert" className="text-alarm-ink text-xs">
                {problem}
              </p>
            ) : (
              <p className="text-muted-foreground text-xs">{t.nameHint}</p>
            )}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              {settings.shared.cancel}
            </Button>
            <Button type="submit" disabled={create.isPending}>
              {create.isPending ? t.creating : t.createConfirm}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

/**
 * The one moment the key exists in the open — so everything a person needs to point a sender at
 * it is here beside it: the address, the header, and a request that works as pasted.
 */
function IssuedDialog({ apiKey, onClose }: { apiKey: IssuedIncidentApiKey; onClose: () => void }) {
  const t = useT().settings.incidentApi
  const endpoint = intakeEndpoint()

  const curl = `curl -X POST ${endpoint} \\
  -H "Content-Type: application/json" \\
  -H "${incidentApiKeyHeader}: ${apiKey.key}" \\
  -d '{
    "title": "Checkout error rate above 5%",
    "description": "Raised by our alerting at 14:20 UTC.",
    "priority": "High",
    "externalId": "checkout-error-rate"
  }'`

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <div className="flex items-center gap-3">
            <span className="bg-muted text-foreground grid size-9 shrink-0 place-items-center rounded-lg">
              <KeyRoundIcon className="size-5" aria-hidden />
            </span>

            <div className="min-w-0">
              <DialogTitle>{t.issuedTitle(apiKey.name)}</DialogTitle>
              <DialogDescription>{t.once}</DialogDescription>
            </div>
          </div>
        </DialogHeader>

        <div className="min-w-0 space-y-4">
          <Copyable label={t.keyLabel} value={apiKey.key} hint={t.endpointHint(incidentApiKeyHeader)} />
          <Copyable label={t.curlLabel} value={curl} hint={t.curlHint} block />
        </div>

        <DialogFooter>
          <Button onClick={onClose}>{t.done}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function DeleteDialog({
  apiKey,
  pending,
  onCancel,
  onConfirm,
}: {
  apiKey: IncidentApiKey
  pending: boolean
  onCancel: () => void
  onConfirm: () => void
}) {
  const { settings } = useT()
  const t = settings.incidentApi

  return (
    <Dialog open onOpenChange={(open) => !open && onCancel()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{settings.shared.deleteTitle(apiKey.name)}</DialogTitle>
          <DialogDescription>{t.deleteBody}</DialogDescription>
        </DialogHeader>

        <DialogFooter>
          {/* Cancel first, so the destructive control is never what focus lands on. */}
          <Button variant="outline" onClick={onCancel}>
            {settings.shared.cancel}
          </Button>
          <Button variant="destructive" disabled={pending} onClick={onConfirm}>
            {pending ? settings.shared.deleting : t.deleteConfirm}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
