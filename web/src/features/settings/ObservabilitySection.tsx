import { useCanOperate } from '@/features/auth/AuthProvider'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { KeyRoundIcon, PlusIcon, TrashIcon } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'

import { telemetrySourcesApi } from '@/api/endpoints'
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
import { formatRelative } from '@/lib/format'
import { useT, type Dictionary } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { isPushed, type TelemetrySource, type TelemetrySourceKind } from '@/types/api'

import { IngestKeyDialog, otlpEndpoint } from './IngestKeyDialog'

import {
  Notice,
  PlannedRow,
  SectionHeading,
  TestReport,
  type TestOutcome,
} from './SettingsCatalogue'
import { TelemetrySourceDialog } from './TelemetrySourceDialog'
import {
  connectable,
  describeSchedule,
  planned,
  scheduleTarget,
  type SourceCatalogueEntry,
} from './telemetryCatalogue'

// The same catalogue the integrations screen uses, pointed the other way: that page is where
// findings go, this one is where they come from. A tile is a connector *type* holding its own
// instances, connecting is a tile press, and what is not built yet is listed as inert rather than
// hidden — so the two settings screens teach one shape rather than two.
//
// What is not echoed is the three-column grid. There is exactly one TelemetrySourceKind, and a
// grid holding one tile is a grid with a hole in it; the tile takes the column instead, which is
// also what gives a source room to state its URL and its schedule on the row.

export function ObservabilitySection() {
  const queryClient = useQueryClient()
  const { settings } = useT()
  const t = settings.telemetry

  const [editing, setEditing] = useState<{
    kind: TelemetrySourceKind
    source: TelemetrySource | null
  } | null>(null)
  const [deleting, setDeleting] = useState<TelemetrySource | null>(null)
  // A source whose key was just issued. Its only appearance: close this and the key is gone.
  const [issued, setIssued] = useState<TelemetrySource | null>(null)
  const [outcomes, setOutcomes] = useState<Record<string, TestOutcome>>({})

  const query = useQuery({ queryKey: ['telemetry-sources'], queryFn: telemetrySourcesApi.list })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['telemetry-sources'] })

  const setEnabled = useMutation({
    mutationFn: ({ id, isEnabled }: { id: string; isEnabled: boolean }) =>
      telemetrySourcesApi.setEnabled(id, isEnabled),
    onSuccess: invalidate,
    onError: (error: Error) => toast.error(error.message),
  })

  const remove = useMutation({
    mutationFn: (id: string) => telemetrySourcesApi.remove(id),
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
    mutationFn: (source: TelemetrySource) => telemetrySourcesApi.test(source.id),
    onSuccess: (result, source) =>
      record(source.id, {
        ok: result.isSuccess,
        detail: isPushed(source.kind)
          ? t.lastReceived(formatRelative(result.lastReceivedAt))
          : result.isSuccess
            ? describeProbe(t, result.matchedEvents)
            : (result.error ?? t.probeRejected),
        at: new Date().toISOString(),
      }),
    // The 502 body carries the real reason — the customer's instance or its API key, not a bad
    // request to us — and ApiError has already pulled it out of the problem document. A pushed
    // source's only failure is that nothing has arrived, said here in the reader's language.
    onError: (error: Error, source) =>
      record(source.id, {
        ok: false,
        detail: isPushed(source.kind) ? t.nothingReceived : error.message,
        at: new Date().toISOString(),
      }),
  })

  const rotate = useMutation({
    mutationFn: (id: string) => telemetrySourcesApi.rotateKey(id),
    onSuccess: (source) => {
      setIssued(source)
      void invalidate()
      toast.success(t.rotated)
    },
    onError: (error: Error) => toast.error(error.message),
  })

  const sources = query.data ?? []
  const enabledCount = sources.filter((source) => source.isEnabled).length

  const byKind = new Map<TelemetrySourceKind, TelemetrySource[]>()

  for (const source of sources) {
    const bucket = byKind.get(source.kind)

    if (bucket) bucket.push(source)
    else byKind.set(source.kind, [source])
  }

  return (
    <div className="max-w-4xl space-y-6">
      {query.isSuccess && <Blindness total={sources.length} enabled={enabledCount} />}

      {query.isError && (
        <Notice tone="bad">
          {t.loadError} {query.error.message}
        </Notice>
      )}

      <section className="space-y-3">
        <SectionHeading
          title={settings.shared.availableNow}
          detail={
            query.isSuccess ? settings.shared.counts(sources.length, enabledCount) : undefined
          }
        />

        <div className="space-y-4">
          {connectable.map((entry) => (
            <KindTile
              key={entry.kind}
              entry={entry}
              instances={byKind.get(entry.kind) ?? []}
              loading={query.isPending}
              outcomes={outcomes}
              testingId={test.isPending ? test.variables.id : undefined}
              togglingId={setEnabled.isPending ? setEnabled.variables?.id : undefined}
              rotatingId={rotate.isPending ? rotate.variables : undefined}
              onConnect={() => setEditing({ kind: entry.kind, source: null })}
              onEdit={(source) => setEditing({ kind: entry.kind, source })}
              onTest={(source) => test.mutate(source)}
              onRotate={(source) => rotate.mutate(source.id)}
              onToggle={(source, isEnabled) => setEnabled.mutate({ id: source.id, isEnabled })}
              onDelete={setDeleting}
              onDismiss={dismiss}
            />
          ))}
        </div>
      </section>

      <section className="space-y-3">
        <SectionHeading title={settings.shared.comingSoon} />

        <p className="text-muted-foreground text-sm">{t.comingSoonNote}</p>

        <div className="grid gap-3 sm:grid-cols-2">
          {planned.map((entry) => (
            <PlannedRow
              key={entry.id}
              entry={{
                name: t.planned[entry.id].name,
                mark: entry.mark,
                summary: t.planned[entry.id].summary,
              }}
            />
          ))}
        </div>
      </section>

      {editing && (
        <TelemetrySourceDialog
          kind={editing.kind}
          source={editing.source}
          onClose={() => setEditing(null)}
          onSaved={(saved) => {
            setEditing(null)
            void invalidate()

            if (saved.ingestKey) setIssued(saved)
          }}
        />
      )}

      {issued && <IngestKeyDialog source={issued} onClose={() => setIssued(null)} />}

      {deleting && (
        <DeleteDialog
          source={deleting}
          pending={remove.isPending}
          onCancel={() => setDeleting(null)}
          onConfirm={() => remove.mutate(deleting.id)}
        />
      )}
    </div>
  )
}

/**
 * Connecting and matching are different successes.
 *
 * A source that answers but shows nothing is a filter problem or a quiet window, and telling the
 * operator only "Connected" leaves them to discover that later, from an empty incident list.
 */
function describeProbe(
  t: Dictionary['settings']['telemetry'],
  matchedEvents: number | null | undefined,
): string {
  if (matchedEvents === null || matchedEvents === undefined) return t.probeAnswered
  if (matchedEvents === 0) return t.probeNoMatch

  return t.probeMatched(matchedEvents)
}

/**
 * The operational fact, stated as one.
 *
 * Nothing connected and everything paused are different configurations with the same consequence,
 * and it is the most consequential one in the product: with no source being read, detection has no
 * input, so the incident list stays empty and reads as "quiet" rather than as "deaf".
 */
function Blindness({ total, enabled }: { total: number; enabled: number }) {
  const t = useT().settings.telemetry

  if (enabled > 0) return null

  return (
    <Notice tone="warn">
      {total === 0 ? t.blindnessNone : total === 1 ? t.blindnessOne : t.blindnessMany(total)}
    </Notice>
  )
}

function KindTile({
  entry,
  instances,
  loading,
  outcomes,
  testingId,
  togglingId,
  rotatingId,
  onConnect,
  onEdit,
  onTest,
  onRotate,
  onToggle,
  onDelete,
  onDismiss,
}: {
  entry: SourceCatalogueEntry
  instances: TelemetrySource[]
  loading: boolean
  outcomes: Record<string, TestOutcome>
  testingId: string | undefined
  togglingId: string | undefined
  rotatingId: string | undefined
  onConnect: () => void
  onEdit: (source: TelemetrySource) => void
  onTest: (source: TelemetrySource) => void
  onRotate: (source: TelemetrySource) => void
  onToggle: (source: TelemetrySource, isEnabled: boolean) => void
  onDelete: (source: TelemetrySource) => void
  onDismiss: (id: string) => void
}) {
  const canOperate = useCanOperate()
  const { labels, settings } = useT()
  const t = settings.telemetry
  const name = labels.telemetryKind[entry.kind]

  return (
    <Card className="flex flex-col">
      <CardHeader>
        <div className="flex items-start gap-3">
          <span className="bg-muted text-foreground grid size-10 shrink-0 place-items-center rounded-lg">
            <entry.mark className="size-5" />
          </span>

          <div className="min-w-0 flex-1">
            <CardTitle>{name}</CardTitle>
            <CardDescription className="mt-0.5 text-xs">{t.summary[entry.kind]}</CardDescription>
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
          <ul className="divide-border border-border divide-y border-t">
            {instances.map((source) => (
              <SourceRow
                key={source.id}
                source={source}
                outcome={outcomes[source.id]}
                testing={testingId === source.id}
                toggling={togglingId === source.id}
                rotating={rotatingId === source.id}
                onEdit={() => onEdit(source)}
                onTest={() => onTest(source)}
                onRotate={() => onRotate(source)}
                onToggle={(isEnabled) => onToggle(source, isEnabled)}
                onDelete={() => onDelete(source)}
                onDismiss={() => onDismiss(source.id)}
              />
            ))}
          </ul>
        )}
      </CardContent>

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

function SourceRow({
  source,
  outcome,
  testing,
  toggling,
  rotating,
  onEdit,
  onTest,
  onRotate,
  onToggle,
  onDelete,
  onDismiss,
}: {
  source: TelemetrySource
  outcome: TestOutcome | undefined
  testing: boolean
  toggling: boolean
  rotating: boolean
  onEdit: () => void
  onTest: () => void
  onRotate: () => void
  onToggle: (isEnabled: boolean) => void
  onDelete: () => void
  onDismiss: () => void
}) {
  const dictionary = useT()
  const { settings } = dictionary
  const canOperate = useCanOperate()
  const t = settings.telemetry

  const pushed = isPushed(source.kind)
  const url = source.config.Url

  return (
    <li className="px-4 py-2.5">
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1.5">
        <Switch
          checked={source.isEnabled}
          // Disabled rather than hidden for a Viewer: its position is how the state is read.
          disabled={toggling || !canOperate}
          aria-label={settings.shared.enabledSwitch(source.name)}
          onCheckedChange={(isEnabled) => onToggle(Boolean(isEnabled))}
        />

        <span
          className={cn(
            'min-w-0 flex-1 truncate text-sm font-medium',
            !source.isEnabled && 'text-muted-foreground',
          )}
          title={source.name}
        >
          {source.name}
        </span>

        {canOperate && (
          <div className="flex shrink-0 items-center gap-1">
            <Button variant="outline" size="sm" disabled={testing} onClick={onTest}>
              {testing ? settings.shared.testing : settings.shared.test}
            </Button>
            {pushed && (
              <Button variant="outline" size="sm" disabled={rotating} onClick={onRotate}>
                <KeyRoundIcon aria-hidden />
                {rotating ? t.rotatingKey : t.rotateKey}
              </Button>
            )}
            <Button variant="outline" size="sm" onClick={onEdit}>
              {settings.shared.edit}
            </Button>
            <Button
              variant="ghost"
              size="icon-sm"
              aria-label={settings.shared.deleteAria(source.name)}
              onClick={onDelete}
            >
              <TrashIcon aria-hidden />
            </Button>
          </div>
        )}
      </div>

      {/* The endpoint is the one value that identifies which instance this actually is, and it is
          long enough to need a truncation decision: the row shows the head and the title carries
          the whole thing, because a URL's tail is the part that repeats. */}
      {url && (
        <p className="text-muted-foreground mt-1 truncate font-mono text-xs" title={url}>
          {url}
        </p>
      )}

      {/* A pushed source's identity is the other way round: where it receives, and which key a
          sender holds for it. The prefix is all there is to show — the key itself was shown once. */}
      {pushed && (
        <p className="text-muted-foreground mt-1 flex flex-wrap gap-x-3 font-mono text-xs">
          <span className="truncate" title={otlpEndpoint()}>
            <span className="font-sans">{t.endpointLabel}:</span> {otlpEndpoint()}
          </span>
          {source.ingestKeyPrefix && (
            <span>
              <span className="font-sans">{t.keyLabel}:</span> {source.ingestKeyPrefix}…
            </span>
          )}
        </p>
      )}

      {/* Paused is a word, not just a switch position — the state has to survive being read by
          someone who cannot see the toggle, and it changes what the schedule line means. */}
      <p className="text-muted-foreground mt-1 text-xs">
        {source.isEnabled ? (
          pushed ? (
            t.pushedSchedule(source.config.MinimumSeverity?.trim() || 'Error')
          ) : (
            describeSchedule(dictionary, source)
          )
        ) : (
          <>
            <span className="text-foreground font-medium">{settings.shared.paused}</span>{' '}
            {pushed
              ? t.pushedPausedNote
              : t.pausedNote(
                  source.pollIntervalSeconds,
                  scheduleTarget(dictionary, source.config.Filter ?? ''),
                )}
          </>
        )}
      </p>

      {outcome && (
        <TestReport
          outcome={outcome}
          okLabel={pushed ? t.receivedOkLabel : t.testOkLabel}
          failLabel={pushed ? t.receivedFailLabel : t.testFailLabel}
          onDismiss={onDismiss}
        />
      )}
    </li>
  )
}

/**
 * Replaces the browser's confirm(). That dialog cannot say what is actually lost, and here two
 * different things are: the API key, which nobody can retype because nobody is allowed to read it
 * back, and the read position, which is what stops the next source re-reading the same window.
 */
function DeleteDialog({
  source,
  pending,
  onCancel,
  onConfirm,
}: {
  source: TelemetrySource
  pending: boolean
  onCancel: () => void
  onConfirm: () => void
}) {
  const { labels, settings } = useT()
  const t = settings.telemetry

  return (
    <Dialog open onOpenChange={(open) => !open && onCancel()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{settings.shared.deleteTitle(source.name)}</DialogTitle>
          <DialogDescription>
            {isPushed(source.kind)
              ? t.deleteBodyPushed(labels.telemetryKind[source.kind])
              : t.deleteBody(labels.telemetryKind[source.kind])}
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
