import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { toast } from 'sonner'

import { integrationsApi, type IntegrationInput } from '@/api/endpoints'
import { ApiError } from '@/api/client'
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
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
import {
  incidentPriorities,
  notificationChannels,
  type IncidentPriority,
  type Integration,
  type NotificationChannelType,
} from '@/types/api'

import { buildConfig, ConfigFields } from './ConfigFields'
import { integrationFields } from './configSchema'

const anyValue = 'any'

export function IntegrationsPage() {
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<Integration | 'new' | null>(null)

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
    onSuccess: () => {
      void invalidate()
      toast.success('Integration deleted')
    },
    onError: (error: Error) => toast.error(error.message),
  })

  const test = useMutation({
    mutationFn: (id: string) => integrationsApi.test(id),
    onSuccess: () => toast.success('Test notification sent'),
    onError: (error: Error) => {
      // A 502 here is the customer's endpoint or credentials, not a bad request to us, and the
      // body carries the reason — which is the only useful thing on this screen.
      const detail = error instanceof ApiError ? error.message : 'The channel rejected the test'
      toast.error(detail)
    },
  })

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Integrations</h1>
          <p className="text-muted-foreground text-sm">
            Where notifications go when an analysis completes. Filters are optional; an
            integration with none matches every incident.
          </p>
        </div>

        <Button onClick={() => setEditing('new')}>Add integration</Button>
      </div>

      <Card className="overflow-hidden py-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead className="w-28">Channel</TableHead>
              <TableHead className="w-48">Filters</TableHead>
              <TableHead className="w-24">Enabled</TableHead>
              <TableHead className="w-56 text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>

          <TableBody>
            {query.isPending && (
              <TableRow>
                <TableCell colSpan={5}>
                  <Skeleton className="h-6 w-full" />
                </TableCell>
              </TableRow>
            )}

            {query.isSuccess && query.data.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} className="text-muted-foreground py-10 text-center">
                  No integrations yet. Nothing will be sent when an incident is analysed.
                </TableCell>
              </TableRow>
            )}

            {query.data?.map((integration) => (
              <TableRow key={integration.id}>
                <TableCell className="font-medium">{integration.name}</TableCell>

                <TableCell>
                  <Badge variant="secondary">{integration.channel}</Badge>
                </TableCell>

                <TableCell className="text-muted-foreground text-sm">
                  {integration.minPriority || integration.categoryFilter ? (
                    <>
                      {integration.minPriority && `${integration.minPriority} and above`}
                      {integration.minPriority && integration.categoryFilter && ' · '}
                      {integration.categoryFilter}
                    </>
                  ) : (
                    'everything'
                  )}
                </TableCell>

                <TableCell>
                  <Switch
                    checked={integration.isEnabled}
                    onCheckedChange={(isEnabled) =>
                      setEnabled.mutate({ id: integration.id, isEnabled: Boolean(isEnabled) })
                    }
                  />
                </TableCell>

                <TableCell className="text-right">
                  <div className="flex justify-end gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={test.isPending}
                      onClick={() => test.mutate(integration.id)}
                    >
                      Test
                    </Button>
                    <Button variant="outline" size="sm" onClick={() => setEditing(integration)}>
                      Edit
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        if (confirm(`Delete "${integration.name}"?`)) remove.mutate(integration.id)
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
        <IntegrationDialog
          integration={editing === 'new' ? null : editing}
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

function IntegrationDialog({
  integration,
  onClose,
  onSaved,
}: {
  integration: Integration | null
  onClose: () => void
  onSaved: () => void
}) {
  const [name, setName] = useState(integration?.name ?? '')
  const [channel, setChannel] = useState<NotificationChannelType>(integration?.channel ?? 'Email')
  const [minPriority, setMinPriority] = useState<string>(integration?.minPriority ?? anyValue)
  const [categoryFilter, setCategoryFilter] = useState(integration?.categoryFilter ?? '')

  // Secrets start blank rather than pre-filled with the mask, so the field reads as "not shown"
  // instead of as a value somebody might overwrite by accident.
  const [values, setValues] = useState<Record<string, string>>(() => {
    const initial: Record<string, string> = {}

    for (const [key, value] of Object.entries(integration?.config ?? {})) {
      if (value !== '***') initial[key] = value
    }

    return initial
  })

  const fields = integrationFields[channel]

  const save = useMutation({
    mutationFn: () => {
      const input: IntegrationInput = {
        name: name.trim(),
        config: buildConfig(fields, values, integration?.config ?? null),
        minPriority: minPriority === anyValue ? null : (minPriority as IncidentPriority),
        categoryFilter: categoryFilter.trim() || null,
      }

      return integration
        ? integrationsApi.update(integration.id, input)
        : integrationsApi.create({ ...input, channel, isEnabled: true })
    },
    onSuccess: () => {
      toast.success(integration ? 'Integration updated' : 'Integration created')
      onSaved()
    },
    onError: (error: Error) => toast.error(error.message),
  })

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{integration ? 'Edit integration' : 'New integration'}</DialogTitle>
          <DialogDescription>
            {integration
              ? 'The channel is fixed at creation. Leave a secret blank to keep the stored value.'
              : 'Pick a channel and fill in what it needs.'}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="name">Name</Label>
            <Input id="name" value={name} onChange={(event) => setName(event.target.value)} />
          </div>

          {!integration && (
            <div className="space-y-1.5">
              <Label>Channel</Label>
              <Select
                value={channel}
                onValueChange={(value) => setChannel(value as NotificationChannelType)}
              >
                <SelectTrigger>
                  <SelectValue>{channel}</SelectValue>
                </SelectTrigger>
                <SelectContent>
                  {notificationChannels.map((option) => (
                    <SelectItem key={option} value={option}>
                      {option}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}

          <ConfigFields
            fields={fields}
            values={values}
            stored={integration?.config ?? null}
            onChange={(key, value) => setValues((current) => ({ ...current, [key]: value }))}
          />

          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label>Minimum priority</Label>
              <Select value={minPriority} onValueChange={(value) => setMinPriority(value ?? anyValue)}>
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
              <Label htmlFor="category">Category filter</Label>
              <Input
                id="category"
                value={categoryFilter}
                placeholder="Any category"
                onChange={(event) => setCategoryFilter(event.target.value)}
              />
            </div>
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
