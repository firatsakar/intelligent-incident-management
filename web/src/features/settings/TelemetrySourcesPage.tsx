import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { PlusIcon, TrashIcon } from 'lucide-react'
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
import { cn } from '@/lib/utils'
import type { TelemetrySource, TelemetrySourceKind } from '@/types/api'

import {
  Notice,
  PlannedRow,
  SectionHeading,
  TestReport,
  type TestOutcome,
} from './SettingsCatalogue'
import { TelemetrySourceDialog } from './TelemetrySourceDialog'
import { connectable, describeSchedule, planned, type SourceCatalogueEntry } from './telemetryCatalogue'

// The same catalogue the integrations screen uses, pointed the other way: that page is where
// findings go, this one is where they come from. A tile is a connector *type* holding its own
// instances, connecting is a tile press, and what is not built yet is listed as inert rather than
// hidden — so the two settings screens teach one shape rather than two.
//
// What is not echoed is the three-column grid. There is exactly one TelemetrySourceKind, and a
// grid holding one tile is a grid with a hole in it; the tile takes the column instead, which is
// also what gives a source room to state its URL and its schedule on the row.

export function TelemetrySourcesPage() {
  const queryClient = useQueryClient()

  const [editing, setEditing] = useState<{
    kind: TelemetrySourceKind
    source: TelemetrySource | null
  } | null>(null)
  const [deleting, setDeleting] = useState<TelemetrySource | null>(null)
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
      toast.success('Source deleted')
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
    mutationFn: (id: string) => telemetrySourcesApi.test(id),
    onSuccess: (result, id) =>
      record(id, {
        ok: result.isSuccess,
        detail: result.isSuccess ? describeProbe(result.matchedEvents) : (result.error ?? 'The source rejected the probe.'),
        at: new Date().toISOString(),
      }),
    // The 502 body carries the real reason — the customer's instance or its API key, not a bad
    // request to us — and ApiError has already pulled it out of the problem document.
    onError: (error: Error, id) =>
      record(id, { ok: false, detail: error.message, at: new Date().toISOString() }),
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
      <div>
        <h2 className="text-lg font-medium tracking-tight">Telemetry</h2>
        <p className="text-muted-foreground mt-1 text-sm">
          Where detection reads from. The platform pulls from your own log store on a schedule — it
          never watches itself, and nothing reaches it that you have not connected here.
        </p>
      </div>

      {query.isSuccess && <Blindness total={sources.length} enabled={enabledCount} />}

      {query.isError && <Notice tone="bad">Could not load sources. {query.error.message}</Notice>}

      <section className="space-y-3">
        <SectionHeading
          title="Available now"
          detail={
            query.isSuccess ? `${sources.length} connected · ${enabledCount} active` : undefined
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
              testingId={test.isPending ? test.variables : undefined}
              togglingId={setEnabled.isPending ? setEnabled.variables?.id : undefined}
              onConnect={() => setEditing({ kind: entry.kind, source: null })}
              onEdit={(source) => setEditing({ kind: entry.kind, source })}
              onTest={(source) => test.mutate(source.id)}
              onToggle={(source, isEnabled) => setEnabled.mutate({ id: source.id, isEnabled })}
              onDelete={setDeleting}
              onDismiss={dismiss}
            />
          ))}
        </div>
      </section>

      <section className="space-y-3">
        <SectionHeading title="Coming soon" />

        <p className="text-muted-foreground text-sm">
          Both of these are the general answer to “my logs are not in Seq”, and neither is built
          yet — there is nothing here to configure. The intent is deliberately not a connector per
          vendor: one standard wire format, and the long tail handled by the shipper you already
          run.
        </p>

        <div className="grid gap-3 sm:grid-cols-2">
          {planned.map((entry) => (
            <PlannedRow key={entry.name} entry={entry} />
          ))}
        </div>
      </section>

      {editing && (
        <TelemetrySourceDialog
          kind={editing.kind}
          source={editing.source}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null)
            void invalidate()
          }}
        />
      )}

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
function describeProbe(matchedEvents: number | null | undefined): string {
  if (matchedEvents === null || matchedEvents === undefined) return 'The source answered the probe.'

  if (matchedEvents === 0) {
    return 'The source answered, but nothing matched the filter in the window probed. Either the window is quiet or the filter is too narrow.'
  }

  return `The source answered with ${matchedEvents} matching event(s) visible.`
}

/**
 * The operational fact, stated as one.
 *
 * Nothing connected and everything paused are different configurations with the same consequence,
 * and it is the most consequential one in the product: with no source being read, detection has no
 * input, so the incident list stays empty and reads as "quiet" rather than as "deaf".
 */
function Blindness({ total, enabled }: { total: number; enabled: number }) {
  if (enabled > 0) return null

  return (
    <Notice tone="warn">
      {total === 0
        ? 'No source is connected. Nothing is being read, so nothing will ever be detected.'
        : total === 1
          ? 'The only source is paused. Nothing is being read, so nothing will be detected.'
          : `All ${total} sources are paused. Nothing is being read, so nothing will be detected.`}
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
  onConnect,
  onEdit,
  onTest,
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
  onConnect: () => void
  onEdit: (source: TelemetrySource) => void
  onTest: (source: TelemetrySource) => void
  onToggle: (source: TelemetrySource, isEnabled: boolean) => void
  onDelete: (source: TelemetrySource) => void
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
            <CardDescription className="mt-0.5 text-xs">{entry.summary}</CardDescription>
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
          <ul className="divide-border border-border divide-y border-t">
            {instances.map((source) => (
              <SourceRow
                key={source.id}
                source={source}
                outcome={outcomes[source.id]}
                testing={testingId === source.id}
                toggling={togglingId === source.id}
                onEdit={() => onEdit(source)}
                onTest={() => onTest(source)}
                onToggle={(isEnabled) => onToggle(source, isEnabled)}
                onDelete={() => onDelete(source)}
                onDismiss={() => onDismiss(source.id)}
              />
            ))}
          </ul>
        )}
      </CardContent>

      <CardFooter className="mt-auto">
        <Button
          variant={instances.length > 0 ? 'outline' : 'default'}
          className="w-full"
          onClick={onConnect}
        >
          <PlusIcon aria-hidden />
          {instances.length > 0 ? `Add another ${entry.name} source` : `Connect ${entry.name}`}
        </Button>
      </CardFooter>
    </Card>
  )
}

function SourceRow({
  source,
  outcome,
  testing,
  toggling,
  onEdit,
  onTest,
  onToggle,
  onDelete,
  onDismiss,
}: {
  source: TelemetrySource
  outcome: TestOutcome | undefined
  testing: boolean
  toggling: boolean
  onEdit: () => void
  onTest: () => void
  onToggle: (isEnabled: boolean) => void
  onDelete: () => void
  onDismiss: () => void
}) {
  const url = source.config.Url

  return (
    <li className="px-4 py-2.5">
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1.5">
        <Switch
          checked={source.isEnabled}
          disabled={toggling}
          aria-label={`${source.name} enabled`}
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
            aria-label={`Delete ${source.name}`}
            onClick={onDelete}
          >
            <TrashIcon aria-hidden />
          </Button>
        </div>
      </div>

      {/* The endpoint is the one value that identifies which instance this actually is, and it is
          long enough to need a truncation decision: the row shows the head and the title carries
          the whole thing, because a URL's tail is the part that repeats. */}
      {url && (
        <p className="text-muted-foreground mt-1 truncate font-mono text-xs" title={url}>
          {url}
        </p>
      )}

      {/* Paused is a word, not just a switch position — the state has to survive being read by
          someone who cannot see the toggle, and it changes what the schedule line means. */}
      <p className="text-muted-foreground mt-1 text-xs">
        {source.isEnabled ? (
          `${describeSchedule(source)}.`
        ) : (
          <>
            <span className="text-foreground font-medium">Paused</span> — nothing is read from
            here. {describeSchedule(source)} when resumed.
          </>
        )}
      </p>

      {outcome && (
        <TestReport
          outcome={outcome}
          okLabel="Probe succeeded"
          failLabel="Probe failed"
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
  return (
    <Dialog open onOpenChange={(open) => !open && onCancel()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Delete “{source.name}”?</DialogTitle>
          <DialogDescription>
            This {source.kind} source and the credentials stored with it are removed, and detection
            stops reading from it immediately. Logs and signatures already ingested are kept — they
            are the evidence behind incidents already opened. A source connected here again starts
            from its initial lookback window rather than from where this one stopped.
          </DialogDescription>
        </DialogHeader>

        <DialogFooter>
          {/* Cancel first, so the destructive control is never what focus lands on. */}
          <Button variant="outline" onClick={onCancel}>
            Cancel
          </Button>
          <Button variant="destructive" disabled={pending} onClick={onConfirm}>
            {pending ? 'Deleting…' : 'Delete source'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
