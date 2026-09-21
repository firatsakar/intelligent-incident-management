import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { toast } from 'sonner'

import { telemetrySourcesApi, type TelemetrySourceInput } from '@/api/endpoints'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
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
import { Switch } from '@/components/ui/switch'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import type { TelemetrySource } from '@/types/api'

import { buildConfig, ConfigFields } from './ConfigFields'
import { telemetrySourceFields } from './configSchema'

export function TelemetrySourcesPage() {
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<TelemetrySource | 'new' | null>(null)

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
    onSuccess: () => {
      void invalidate()
      toast.success('Source deleted')
    },
    onError: (error: Error) => toast.error(error.message),
  })

  const test = useMutation({
    mutationFn: (id: string) => telemetrySourcesApi.test(id),
    onSuccess: (result) =>
      toast.success(
        // A source that connects but matches nothing is a different problem from one that
        // cannot connect, and only the count tells them apart.
        result.matchedEvents === undefined
          ? 'Connected'
          : `Connected — ${result.matchedEvents} event(s) visible`,
      ),
    onError: (error: Error) => toast.error(error.message),
  })

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Telemetry sources</h1>
          <p className="text-muted-foreground text-sm">
            Where detection reads from. The platform pulls from the customer's own log store —
            it never watches itself.
          </p>
        </div>

        <Button onClick={() => setEditing('new')}>Add source</Button>
      </div>

      <Card className="overflow-hidden py-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead className="w-24">Kind</TableHead>
              <TableHead className="w-64">URL</TableHead>
              <TableHead className="w-28">Poll</TableHead>
              <TableHead className="w-24">Enabled</TableHead>
              <TableHead className="w-56 text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>

          <TableBody>
            {query.isPending && (
              <TableRow>
                <TableCell colSpan={6}>
                  <Skeleton className="h-6 w-full" />
                </TableCell>
              </TableRow>
            )}

            {query.isSuccess && query.data.length === 0 && (
              <TableRow>
                <TableCell colSpan={6} className="text-muted-foreground py-10 text-center">
                  No sources configured. Nothing is being watched, so nothing will be detected.
                </TableCell>
              </TableRow>
            )}

            {query.data?.map((source) => (
              <TableRow key={source.id}>
                <TableCell className="font-medium">{source.name}</TableCell>

                <TableCell>
                  <Badge variant="secondary">{source.kind}</Badge>
                </TableCell>

                <TableCell className="text-muted-foreground truncate text-sm">
                  {source.config.Url ?? '—'}
                </TableCell>

                <TableCell className="text-muted-foreground text-sm tabular-nums">
                  every {source.pollIntervalSeconds}s
                </TableCell>

                <TableCell>
                  <Switch
                    checked={source.isEnabled}
                    onCheckedChange={(isEnabled) =>
                      setEnabled.mutate({ id: source.id, isEnabled: Boolean(isEnabled) })
                    }
                  />
                </TableCell>

                <TableCell className="text-right">
                  <div className="flex justify-end gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={test.isPending}
                      onClick={() => test.mutate(source.id)}
                    >
                      Test
                    </Button>
                    <Button variant="outline" size="sm" onClick={() => setEditing(source)}>
                      Edit
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        if (confirm(`Delete "${source.name}"?`)) remove.mutate(source.id)
                      }}
                    >
                      Delete
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>

      {editing && (
        <SourceDialog
          source={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null)
            void invalidate()
          }}
        />
      )}
    </div>
  )
}

function SourceDialog({
  source,
  onClose,
  onSaved,
}: {
  source: TelemetrySource | null
  onClose: () => void
  onSaved: () => void
}) {
  const [name, setName] = useState(source?.name ?? '')
  const [pollInterval, setPollInterval] = useState(String(source?.pollIntervalSeconds ?? 15))

  const [values, setValues] = useState<Record<string, string>>(() => {
    const initial: Record<string, string> = {}

    for (const [key, value] of Object.entries(source?.config ?? {})) {
      if (value !== '***') initial[key] = value
    }

    return initial
  })

  // Only Seq exists today; the kind is fixed at creation either way.
  const fields = telemetrySourceFields.Seq

  const save = useMutation({
    mutationFn: () => {
      const parsed = Number(pollInterval)

      const input: TelemetrySourceInput = {
        name: name.trim(),
        config: buildConfig(fields, values, source?.config ?? null),
        pollIntervalSeconds: Number.isFinite(parsed) && parsed > 0 ? parsed : undefined,
      }

      return source
        ? telemetrySourcesApi.update(source.id, input)
        : telemetrySourcesApi.create({ ...input, kind: 'Seq', isEnabled: true })
    },
    onSuccess: () => {
      toast.success(source ? 'Source updated' : 'Source created')
      onSaved()
    },
    onError: (error: Error) => toast.error(error.message),
  })

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{source ? 'Edit source' : 'New telemetry source'}</DialogTitle>
          <DialogDescription>
            {source
              ? 'Leave the API key blank to keep the stored one.'
              : 'Seq is the only connector today. OTLP ingest is Adım 13.5.'}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="source-name">Name</Label>
            <Input
              id="source-name"
              value={name}
              onChange={(event) => setName(event.target.value)}
            />
          </div>

          <ConfigFields
            fields={fields}
            values={values}
            stored={source?.config ?? null}
            onChange={(key, value) => setValues((current) => ({ ...current, [key]: value }))}
          />

          <div className="space-y-1.5">
            <Label htmlFor="poll">Poll interval (seconds)</Label>
            <Input
              id="poll"
              value={pollInterval}
              onChange={(event) => setPollInterval(event.target.value)}
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button disabled={!name.trim() || save.isPending} onClick={() => save.mutate()}>
            {save.isPending ? 'Saving…' : 'Save'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
