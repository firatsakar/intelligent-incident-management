import { useCanOperate } from '@/features/auth/AuthProvider'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { PlusIcon, TrashIcon } from 'lucide-react'
import { useState } from 'react'
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
import { useT } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import type { Integration, NotificationChannelType } from '@/types/api'

import { IntegrationDialog } from './IntegrationDialog'
import {
  connectable,
  describeFilters,
  filterParts,
  planned,
  type CatalogueEntry,
} from './integrationCatalogue'
import {
  Notice,
  PlannedRow,
  SectionHeading,
  TestReport,
  type TestOutcome,
} from './SettingsCatalogue'

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
//
// The pieces with no opinion about notifications — the section rule, the notice, the inert planned
// row, the test strip — moved to SettingsCatalogue when the telemetry screen became the second
// consumer of them.

export function NotificationsSection() {
  const queryClient = useQueryClient()
  const { settings } = useT()
  const t = settings.integrations

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
      toast.success(t.deleted)
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
        detail: result.isSuccess ? t.testOk : (result.error ?? t.testRejected),
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
      {query.isSuccess && <Silence total={integrations.length} enabled={enabledCount} />}

      {query.isError && (
        <Notice tone="bad">
          {t.loadError} {query.error.message}
        </Notice>
      )}

      <section className="space-y-3">
        <SectionHeading
          title={settings.shared.availableNow}
          detail={
            query.isSuccess ? settings.shared.counts(integrations.length, enabledCount) : undefined
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
        <SectionHeading title={settings.shared.comingSoon} />

        <p className="text-muted-foreground max-w-2xl text-sm">{t.comingSoonNote}</p>

        <div className="grid gap-3 sm:grid-cols-2 2xl:grid-cols-4">
          {planned.map((entry) => (
            <PlannedRow
              key={entry.id}
              entry={{ name: entry.name, mark: entry.mark, summary: t.planned[entry.id] }}
            />
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

/**
 * The operational fact, stated as one.
 *
 * Nothing connected and everything paused are different configurations with the same consequence:
 * an analysis finishes and no one is told. Caution rather than alarm — the gate has not reached a
 * verdict here, this is a gap in the setup — and it carries an icon and words so the colour is
 * never doing the work alone.
 */
function Silence({ total, enabled }: { total: number; enabled: number }) {
  const t = useT().settings.integrations

  if (enabled > 0) return null

  return (
    <Notice tone="warn">
      {total === 0 ? t.silenceNone : total === 1 ? t.silenceOne : t.silenceMany(total)}
    </Notice>
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
  const canOperate = useCanOperate()
  const { labels, settings } = useT()
  const t = settings.integrations
  const name = labels.channel[entry.channel]

  return (
    <Card className="flex flex-col">
      <CardHeader>
        <div className="flex items-start gap-3">
          <span className="bg-muted text-foreground grid size-10 shrink-0 place-items-center rounded-lg">
            <entry.mark className="size-5" />
          </span>

          <div className="min-w-0 flex-1">
            <CardTitle>{name}</CardTitle>
            {/* Two lines' worth whether it needs them or not, so the rule above the instance list
                falls at the same height across a row of tiles. Only once there is a row: in a
                single column it would just be a hole. */}
            <CardDescription className="mt-0.5 text-xs md:min-h-8">
              {t.summary[entry.channel]}
            </CardDescription>
          </div>

          {instances.length > 0 && (
            <span className="bg-secondary text-secondary-foreground shrink-0 rounded-full px-2 py-0.5 text-xs font-medium tabular-nums">
              {instances.length}
              <span className="sr-only">{settings.shared.connected}</span>
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
          <p className="text-muted-foreground px-4 text-sm">{settings.shared.notConnected}</p>
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
      {canOperate && (
        <CardFooter className="mt-auto">
          <Button
            variant={instances.length > 0 ? 'outline' : 'default'}
            className="w-full"
            onClick={onConnect}
          >
            <PlusIcon aria-hidden />
            {instances.length > 0 ? t.addAnother(name) : t.connectOne(name)}
          </Button>
        </CardFooter>
      )}
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
  const dictionary = useT()
  const { settings } = dictionary
  const canOperate = useCanOperate()
  const t = settings.integrations

  return (
    <li className="px-4 py-2.5">
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1.5">
        <Switch
          checked={integration.isEnabled}
          // Disabled rather than hidden for a Viewer: its position is how the state is read.
          disabled={toggling || !canOperate}
          aria-label={settings.shared.enabledSwitch(integration.name)}
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

        {canOperate && (
          <div className="flex shrink-0 items-center gap-1">
            <Button variant="outline" size="sm" disabled={testing} onClick={onTest}>
              {testing ? settings.shared.testing : settings.shared.test}
            </Button>
            <Button variant="outline" size="sm" onClick={onEdit}>
              {settings.shared.edit}
            </Button>
            <Button
              variant="ghost"
              size="icon-sm"
              aria-label={settings.shared.deleteAria(integration.name)}
              onClick={onDelete}
            >
              <TrashIcon aria-hidden />
            </Button>
          </div>
        )}
      </div>

      {/* Paused is a word, not just a switch position — the state has to survive being read by
          someone who cannot see the toggle, and it changes what the filter line means. */}
      <p className="text-muted-foreground mt-1 text-xs">
        {integration.isEnabled ? (
          describeFilters(dictionary, integration.minPriority, integration.categoryFilter)
        ) : (
          <>
            <span className="text-foreground font-medium">{settings.shared.paused}</span>{' '}
            {(() => {
              const parts = filterParts(
                dictionary,
                integration.minPriority,
                integration.categoryFilter,
              )

              return parts === null ? t.pausedNoteEverything : t.pausedNote(parts)
            })()}
          </>
        )}
      </p>

      {outcome && (
        <TestReport
          outcome={outcome}
          okLabel={t.testOkLabel}
          failLabel={t.testFailLabel}
          onDismiss={onDismiss}
        />
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
  const { labels, settings } = useT()
  const t = settings.integrations

  return (
    <Dialog open onOpenChange={(open) => !open && onCancel()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{settings.shared.deleteTitle(integration.name)}</DialogTitle>
          <DialogDescription>
            {t.deleteBody(labels.channel[integration.channel])}
          </DialogDescription>
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
