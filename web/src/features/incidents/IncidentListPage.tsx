import { Link, useSearchParams } from 'react-router-dom'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import {
  formatConfidence,
  formatDuration,
  formatRelative,
  incidentStatusLabel,
  priorityClass,
} from '@/lib/format'
import { cn } from '@/lib/utils'
import {
  incidentPriorities,
  incidentStatuses,
  type IncidentPriority,
  type IncidentStatus,
} from '@/types/api'

import { useIncidents } from './queries'

const pageSize = 20
const anyValue = 'any'

export function IncidentListPage() {
  // Filters live in the URL, not in component state: a filtered view of an ops tool should be
  // shareable, and it should survive a refresh.
  const [params, setParams] = useSearchParams()

  const status = (params.get('status') as IncidentStatus | null) ?? undefined
  const priority = (params.get('priority') as IncidentPriority | null) ?? undefined
  const pageNumber = Number(params.get('page') ?? '1')

  const query = useIncidents({ status, priority, pageNumber, pageSize })

  // The Select can hand back null when a value is cleared, so this takes the wider type.
  function setParam(key: string, value: string | null | undefined) {
    const next = new URLSearchParams(params)

    if (value === undefined || value === null || value === anyValue) next.delete(key)
    else next.set(key, value)

    // Any filter change invalidates the page number; staying on page 4 of a different result set
    // shows an empty table for no reason.
    if (key !== 'page') next.delete('page')

    setParams(next)
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Incidents</h1>
          <p className="text-muted-foreground text-sm">
            {query.data
              ? `${query.data.totalCount} on record`
              : 'Everything the platform has opened, by hand or on its own.'}
          </p>
        </div>

        <div className="flex gap-2">
          <Select value={status ?? anyValue} onValueChange={(v) => setParam('status', v)}>
            <SelectTrigger className="w-40">
              {/* The label is passed explicitly: left to itself the trigger renders the raw
                  value, so the filter reads "InProgress" and "any" rather than English. */}
              <SelectValue>{status ? incidentStatusLabel[status] : 'Any status'}</SelectValue>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={anyValue}>Any status</SelectItem>
              {incidentStatuses.map((value) => (
                <SelectItem key={value} value={value}>
                  {incidentStatusLabel[value]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select value={priority ?? anyValue} onValueChange={(v) => setParam('priority', v)}>
            <SelectTrigger className="w-40">
              <SelectValue>{priority ?? 'Any priority'}</SelectValue>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={anyValue}>Any priority</SelectItem>
              {incidentPriorities.map((value) => (
                <SelectItem key={value} value={value}>
                  {value}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      <Card className="overflow-hidden py-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-24">Priority</TableHead>
              <TableHead>Incident</TableHead>
              <TableHead className="w-28">Source</TableHead>
              <TableHead className="w-32">Status</TableHead>
              <TableHead className="w-40">Analysis</TableHead>
              <TableHead className="w-36 text-right">Detected</TableHead>
            </TableRow>
          </TableHeader>

          <TableBody>
            {query.isPending &&
              Array.from({ length: 5 }).map((_, index) => (
                <TableRow key={index}>
                  <TableCell colSpan={6}>
                    <Skeleton className="h-6 w-full" />
                  </TableCell>
                </TableRow>
              ))}

            {query.isError && (
              <TableRow>
                <TableCell colSpan={6} className="text-destructive py-8 text-center">
                  {query.error instanceof Error ? query.error.message : 'Could not load incidents'}
                </TableCell>
              </TableRow>
            )}

            {query.isSuccess && query.data.items.length === 0 && (
              <TableRow>
                <TableCell colSpan={6} className="text-muted-foreground py-10 text-center">
                  Nothing matches these filters.
                </TableCell>
              </TableRow>
            )}

            {query.data?.items.map((incident) => (
              <TableRow key={incident.id} className="group">
                <TableCell>
                  <Badge variant="outline" className={cn('border', priorityClass[incident.priority])}>
                    {incident.priority}
                  </Badge>
                </TableCell>

                <TableCell>
                  <Link
                    to={`/incidents/${incident.id}`}
                    className="font-medium group-hover:underline"
                  >
                    {incident.title}
                  </Link>
                  {incident.assignedTeam && (
                    <span className="text-muted-foreground ml-2 text-xs">
                      · {incident.assignedTeam}
                    </span>
                  )}
                </TableCell>

                <TableCell>
                  <Badge variant="secondary" className="font-normal">
                    {incident.source}
                  </Badge>
                </TableCell>

                <TableCell className="text-muted-foreground text-sm">
                  {incidentStatusLabel[incident.status]}
                </TableCell>

                <TableCell className="text-sm">
                  {incident.isAiAnalyzed ? (
                    <span>
                      {incident.aiSuggestedCategory ?? 'analysed'}
                      <span className="text-muted-foreground ml-1 tabular-nums">
                        {formatConfidence(incident.aiConfidence)}
                      </span>
                    </span>
                  ) : (
                    <span className="text-muted-foreground">awaiting analysis</span>
                  )}
                </TableCell>

                <TableCell className="text-right text-sm tabular-nums">
                  {formatRelative(incident.detectedAt ?? incident.createdAt)}
                  {incident.detectedAt && (
                    // The gap between when it started and when the record was filed. On a
                    // telemetry incident this is detection latency.
                    <span className="text-muted-foreground block text-xs">
                      +{formatDuration(incident.detectedAt, incident.createdAt)} to open
                    </span>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>

      {query.data && query.data.totalPages > 1 && (
        <div className="flex items-center justify-between text-sm">
          <span className="text-muted-foreground">
            Page {query.data.pageNumber} of {query.data.totalPages}
          </span>

          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={!query.data.hasPreviousPage}
              onClick={() => setParam('page', String(pageNumber - 1))}
            >
              Previous
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={!query.data.hasNextPage}
              onClick={() => setParam('page', String(pageNumber + 1))}
            >
              Next
            </Button>
          </div>
        </div>
      )}
    </div>
  )
}
