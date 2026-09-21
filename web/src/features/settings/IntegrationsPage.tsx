import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  AlertTriangleIcon,
  CircleCheckIcon,
  PlusIcon,
  TrashIcon,
  XIcon,
} from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { toast } from 'sonner'

import { integrationsApi } from '@/api/endpoints'
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
import { Skeleton } from '@/components/ui/skeleton'
import { Switch } from '@/components/ui/switch'
import { formatTime } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { Integration, NotificationChannelType } from '@/types/api'

import { IntegrationDialog } from './IntegrationDialog'
import {
  connectable,
  describeFilters,
  planned,
  type CatalogueEntry,
} from './integrationCatalogue'

// A directory rather than a table behind an "Add" button.
//
// The old screen asked the operator to know the channel vocabulary before it would show them
// anything. A catalogue inverts that: the destinations are the page, each one says what it is, and
// connecting is a tile press rather than a dropdown inside a modal.
//
// A tile is a *type*, not an instance. One channel legitimately holds several integrations — two
// Email entries with different priority filters is the intended way to route Critical somewhere
// louder — so the instances live inside their own tile, where the count, the filters and the
// controls for each one sit next to the thing they belong to. Nothing needs a second screen.

/** The last test the operator ran, kept on the row. A toast is gone before a long SMTP error
    can be read, and this button is the only feedback loop a customer has. */
interface TestOutcome {
  ok: boolean
  detail: string
  at: string
}

export function IntegrationsPage() {
  const queryClient = useQueryClient()

  const [editing, setEditing] = useState<{
    channel: NotificationChannelType
    integration: Integration | null
  } | null>(null)
  const [deleting, setDeleting] = useState<Integration | null>(null)
  const [outcomes, setOutcomes] = useState<Record<string, TestOutcome>>({})

  const query = useQuery({ queryKey: ['integrations'], queryFn: integrationsApi.list })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['integrations'] })

  const setEnabled = useMutation({
    mutationFn: ({ id, isEnabled }: { id: string; isEnabled: boolean }) =>
      integrationsApi.setEnabled(id, isEnabled),
    onSuccess: invalidate,
    onError: (error: Error) => toast.error(error.message),
  })

  const remove = useMutation({
    mutationFn: (id: string) => integrationsApi.remove(id),
    onSuccess: (_result, id) => {
      setDeleting(null)
      dismiss(id)
      void invalidate()
      toast.success('Integration deleted')
    },
    onError: (error: Error) => toast.error(error.message),
  })

  const record = (id: string, outcome: TestOutcome) =>
    setOutcomes((current) => ({ ...current, [id]: outcome }))

  function dismiss(id: string) {
    setOutcomes((current) => {
      const next = { ...current }

      delete next[id]

      return next
    })
  }

  const test = useMutation({
    mutationFn: (id: string) => integrationsApi.test(id),
    onSuccess: (result, id) =>
      record(id, {
        ok: result.isSuccess,
        detail: result.isSuccess
          ? 'The channel accepted a test notification.'
          : (result.error ?? 'The channel rejected the test.'),
        at: new Date().toISOString(),
      }),
    // The 502 body carries the real reason — the customer's endpoint or credentials, not a bad
    // request to us — and ApiError has already pulled it out of the problem document.
    onError: (error: Error, id) =>
      record(id, { ok: false, detail: error.message, at: new Date().toISOString() }),
  })

  const integrations = query.data ?? []
  const enabledCount = integrations.filter((integration) => integration.isEnabled).length

  const byChannel = new Map<NotificationChannelType, Integration[]>()

  for (const integration of integrations) {
    const bucket = byChannel.get(integration.channel)

    if (bucket) bucket.push(integration)
    else byChannel.set(integration.channel, [integration])
  }

  return (
    <div className="space-y-6">
      <div className="max-w-2xl">
        <h1 className="text-2xl font-semibold tracking-tight">Integrations</h1>
        <p className="text-muted-foreground mt-1 text-sm">
          Where a notification goes when an analysis completes. One destination type can hold
          several integrations — two Email entries with different filters is a normal setup.
        </p>
      </div>

      {query.isSuccess && <Silence total={integrations.length} enabled={enabledCount} />}

      {query.isError && (
        <Notice tone="bad">
          Could not load integrations. {query.error.message}
        </Notice>
      )}

      <section className="space-y-3">
        <SectionHeading
          title="Available now"
          detail={
            query.isSuccess
              ? `${integrations.length} connected · ${enabledCount} active`
              : undefined
          }
        />

        <div className="grid items-stretch gap-4 md:grid-cols-2 xl:grid-cols-3">
          {connectable.map((entry) => (
            <ChannelTile
              key={entry.channel}
              entry={entry}
              instances={byChannel.get(entry.channel) ?? []}
              loading={query.isPending}
              outcomes={outcomes}
              testingId={test.isPending ? test.variables : undefined}
              togglingId={setEnabled.isPending ? setEnabled.variables?.id : undefined}
              onConnect={() => setEditing({ channel: entry.channel, integration: null })}
              onEdit={(integration) => setEditing({ channel: entry.channel, integration })}
              onTest={(integration) => test.mutate(integration.id)}
              onToggle={(integration, isEnabled) =>
                setEnabled.mutate({ id: integration.id, isEnabled })
              }
              onDelete={setDeleting}
              onDismiss={dismiss}
            />
          ))}
        </div>
      </section>

      <section className="space-y-3">
        <SectionHeading title="Coming soon" />

        <p className="text-muted-foreground max-w-2xl text-sm">
          Planned destinations with nothing behind them yet — there is nothing here to configure.
          Until they land, a custom endpoint is reachable through Webhook, which posts this
          platform's own JSON rather than any vendor's payload format.
        </p>

        <div className="grid gap-3 sm:grid-cols-2 2xl:grid-cols-4">
          {planned.map((entry) => (
            <div
              key={entry.name}
              className="border-inert-border flex items-center gap-3 rounded-xl border border-dashed px-3 py-2.5"
            >
              <span className="bg-muted/50 text-dim-foreground grid size-9 shrink-0 place-items-center rounded-lg">
                <entry.mark className="size-5" />
              </span>

              <div className="min-w-0">
                <p className="text-muted-foreground truncate text-sm font-medium">{entry.name}</p>
                <p className="text-dim-foreground truncate text-xs">{entry.summary}</p>
              </div>

              {/* Not a disabled button. A control that cannot ever be pressed is still a control,
                  and it invites the press that does nothing. This is a label. */}
              <span className="border-inert-border text-dim-foreground ml-auto shrink-0 rounded-full border px-2 py-0.5 text-[11px] whitespace-nowrap">
                Coming soon
              </span>
            </div>
          ))}
        </div>
      </section>

      {editing && (
        <IntegrationDialog
          channel={editing.channel}
          integration={editing.integration}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null)
            void invalidate()
          }}
        />
      )}

      {deleting && (
        <DeleteDialog
          integration={deleting}
          pending={remove.isPending}
          onCancel={() => setDeleting(null)}
          onConfirm={() => remove.mutate(deleting.id)}
        />
      )}
    </div>
  )
}

function SectionHeading({ title, detail }: { title: string; detail?: string }) {
  return (
    <div className="flex items-baseline gap-3">
      <h2 className="text-muted-foreground text-xs font-medium tracking-wider uppercase">
        {title}
      </h2>
      <span className="bg-border h-px flex-1" aria-hidden />
      {detail && <span className="text-muted-foreground text-xs tabular-nums">{detail}</span>}
    </div>
  )
}

/**
 * The operational fact, stated as one.
 *
 * Nothing connected and everything paused are different configurations with the same consequence:
 * an analysis finishes and no one is told. Caution rather than alarm — the gate has not reached a
 * verdict here, this is a gap in the setup — and it carries an icon and words so the colour is
 * never doing the work alone.
 */
function Silence({ total, enabled }: { total: number; enabled: number }) {
  if (enabled > 0) return null

  return (
    <Notice tone="warn">
      {total === 0
        ? 'Nothing is connected. When an analysis completes, no one is told.'
        : total === 1
          ? 'The only integration is paused. When an analysis completes, no one is told.'
          : `All ${total} integrations are paused. When an analysis completes, no one is told.`}
    </Notice>
  )
}

function Notice({ tone, children }: { tone: 'warn' | 'bad'; children: ReactNode }) {
  return (
    <p
      role={tone === 'bad' ? 'alert' : 'status'}
      className={cn(
        'flex items-start gap-2 rounded-lg border px-3 py-2 text-sm',
        tone === 'warn'
          ? 'bg-caution text-caution-foreground border-caution-border'
          : 'border-alarm-border/70 bg-alarm/10 text-alarm-ink',
      )}
    >
      <AlertTriangleIcon className="mt-0.5 size-4 shrink-0" aria-hidden />
      <span className="min-w-0">{children}</span>
    </p>
  )
}

function ChannelTile({
  entry,
  instances,
  loading,
  outcomes,
  testingId,
  togglingId,
  onConnect,
  onEdit,
  onTest,
  onToggle,
  onDelete,
  onDismiss,
}: {
  entry: CatalogueEntry
  instances: Integration[]
  loading: boolean
  outcomes: Record<string, TestOutcome>
  testingId: string | undefined
  togglingId: string | undefined
  onConnect: () => void
  onEdit: (integration: Integration) => void
  onTest: (integration: Integration) => void
  onToggle: (integration: Integration, isEnabled: boolean) => void
  onDelete: (integration: Integration) => void
  onDismiss: (id: string) => void
}) {
  return (
    <Card className="flex flex-col">
      <CardHeader>
        <div className="flex items-start gap-3">
          <span className="bg-muted text-foreground grid size-10 shrink-0 place-items-center rounded-lg">
            <entry.mark className="size-5" />
          </span>

          <div className="min-w-0 flex-1">
            <CardTitle>{entry.name}</CardTitle>
            {/* Two lines' worth whether it needs them or not, so the rule above the instance list
                falls at the same height across a row of tiles. Only once there is a row: in a
                single column it would just be a hole. */}
            <CardDescription className="mt-0.5 text-xs md:min-h-8">{entry.summary}</CardDescription>
          </div>

          {instances.length > 0 && (
            <span className="bg-secondary text-secondary-foreground shrink-0 rounded-full px-2 py-0.5 text-xs font-medium tabular-nums">
              {instances.length}
              <span className="sr-only"> connected</span>
            </span>
          )}
        </div>
      </CardHeader>

      <CardContent className="px-0">
        {loading ? (
          <div className="px-4">
            <Skeleton className="h-9 w-full" />
          </div>
        ) : instances.length === 0 ? (
          <p className="text-muted-foreground px-4 text-sm">Not connected.</p>
        ) : (
          // Ruled above only. A bottom rule would close a box that the footer then re-opens, and
          // in a tile that is shorter than its neighbours the gap between the two reads as an
          // empty row rather than as slack.
          <ul className="divide-border border-border divide-y border-t">
            {instances.map((integration) => (
              <InstanceRow
                key={integration.id}
                integration={integration}
                outcome={outcomes[integration.id]}
                testing={testingId === integration.id}
                toggling={togglingId === integration.id}
                onEdit={() => onEdit(integration)}
                onTest={() => onTest(integration)}
                onToggle={(isEnabled) => onToggle(integration, isEnabled)}
                onDelete={() => onDelete(integration)}
                onDismiss={() => onDismiss(integration.id)}
              />
            ))}
          </ul>
        )}
      </CardContent>

      {/* mt-auto so the action lines up across a row of tiles whose bodies are different heights. */}
      <CardFooter className="mt-auto">
        <Button
          variant={instances.length > 0 ? 'outline' : 'default'}
          className="w-full"
          onClick={onConnect}
        >
          <PlusIcon aria-hidden />
          {instances.length > 0 ? `Add another ${entry.name}` : `Connect ${entry.name}`}
        </Button>
      </CardFooter>
    </Card>
  )
}

function InstanceRow({
  integration,
  outcome,
  testing,
  toggling,
  onEdit,
  onTest,
  onToggle,
  onDelete,
  onDismiss,
}: {
  integration: Integration
  outcome: TestOutcome | undefined
  testing: boolean
  toggling: boolean
  onEdit: () => void
  onTest: () => void
  onToggle: (isEnabled: boolean) => void
  onDelete: () => void
  onDismiss: () => void
}) {
  return (
    <li className="px-4 py-2.5">
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1.5">
        <Switch
          checked={integration.isEnabled}
          disabled={toggling}
          aria-label={`${integration.name} enabled`}
          onCheckedChange={(isEnabled) => onToggle(Boolean(isEnabled))}
        />

        <span
          className={cn(
            'min-w-0 flex-1 truncate text-sm font-medium',
            !integration.isEnabled && 'text-muted-foreground',
          )}
          title={integration.name}
        >
          {integration.name}
        </span>

        <div className="flex shrink-0 items-center gap-1">
          <Button variant="outline" size="sm" disabled={testing} onClick={onTest}>
            {testing ? 'Testing…' : 'Test'}
          </Button>
          <Button variant="outline" size="sm" onClick={onEdit}>
            Edit
          </Button>
          <Button
            variant="ghost"
            size="icon-sm"
            aria-label={`Delete ${integration.name}`}
            onClick={onDelete}
          >
            <TrashIcon aria-hidden />
          </Button>
        </div>
      </div>

      {/* Paused is a word, not just a switch position — the state has to survive being read by
          someone who cannot see the toggle, and it changes what the filter line means. */}
      <p className="text-muted-foreground mt-1 text-xs">
        {integration.isEnabled ? (
          `${describeFilters(integration.minPriority, integration.categoryFilter)}.`
        ) : (
          <>
            <span className="text-foreground font-medium">Paused</span> — nothing is sent here.{' '}
            {describeFilters(integration.minPriority, integration.categoryFilter)} when resumed.
          </>
        )}
      </p>

      {outcome && (
        <div
          role="status"
          className={cn(
            'mt-2 flex items-start gap-2 rounded-md border px-2.5 py-1.5 text-xs',
            outcome.ok
              ? 'bg-nominal text-nominal-foreground border-nominal-border'
              : // Not the solid alarm fill. That is reserved for a verdict the system reached on
                // its own; this is the output of a probe the operator just ran, and the reason can
                // run to several lines of somebody else's exception text.
                'border-alarm-border/70 bg-alarm/10 text-alarm-ink',
          )}
        >
          {outcome.ok ? (
            <CircleCheckIcon className="mt-px size-3.5 shrink-0" aria-hidden />
          ) : (
            <AlertTriangleIcon className="mt-px size-3.5 shrink-0" aria-hidden />
          )}

          <span className="min-w-0 flex-1 break-words">
            <span className="font-medium">{outcome.ok ? 'Test sent' : 'Test failed'}</span> at{' '}
            {formatTime(outcome.at)} — {outcome.detail}
          </span>

          <button
            type="button"
            onClick={onDismiss}
            aria-label="Dismiss test result"
            className="focus-visible:ring-ring/50 -my-0.5 -mr-1 grid size-6 shrink-0 place-items-center rounded-sm outline-none hover:opacity-70 focus-visible:ring-3"
          >
            <XIcon className="size-3.5" aria-hidden />
          </button>
        </div>
      )}
    </li>
  )
}

/**
 * Replaces the browser's confirm(). That dialog cannot say what is actually lost, and what is
 * actually lost is the credential — the one value nobody can retype, because nobody is allowed to
 * read it back.
 */
function DeleteDialog({
  integration,
  pending,
  onCancel,
  onConfirm,
}: {
  integration: Integration
  pending: boolean
  onCancel: () => void
  onConfirm: () => void
}) {
  return (
    <Dialog open onOpenChange={(open) => !open && onCancel()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Delete “{integration.name}”?</DialogTitle>
          <DialogDescription>
            This {integration.channel} destination and the credentials stored with it are removed.
            Delivery history for incidents already sent is kept.
          </DialogDescription>
        </DialogHeader>

        <DialogFooter>
          {/* Cancel first, so the destructive control is never what focus lands on. */}
          <Button variant="outline" onClick={onCancel}>
            Cancel
          </Button>
          <Button variant="destructive" disabled={pending} onClick={onConfirm}>
            {pending ? 'Deleting…' : 'Delete integration'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
