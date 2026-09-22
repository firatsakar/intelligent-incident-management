import { FilterXIcon } from 'lucide-react'
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
  TableCaption,
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
  type Incident,
  type IncidentPriority,
  type IncidentStatus,
} from '@/types/api'

import { useIncidents } from './queries'

const pageSize = 20
const anyValue = 'any'

/** Kept in one place so the skeleton and the empty rows cannot drift from the header. */
const columnCount = 7

export function IncidentListPage() {
  // Filters live in the URL, not in component state: a filtered view of an ops tool should be
  // shareable, and it should survive a refresh.
  const [params, setParams] = useSearchParams()

  const status = (params.get('status') as IncidentStatus | null) ?? undefined
  const priority = (params.get('priority') as IncidentPriority | null) ?? undefined
  const pageNumber = Number(params.get('page') ?? '1')

  const query = useIncidents({ status, priority, pageNumber, pageSize })

  const filtered = Boolean(status || priority)

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

  function clearFilters() {
    const next = new URLSearchParams(params)

    next.delete('status')
    next.delete('priority')
    next.delete('page')

    setParams(next)
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-x-6 gap-y-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Incidents</h1>
          <p className="text-muted-foreground text-sm">
            {query.data
              ? // Says which count this is. "8 on record" next to an active filter is a claim
                // about the whole table that the table is not showing.
                filtered
                ? `${query.data.totalCount} match these filters`
                : `${query.data.totalCount} on record`
              : 'Everything the platform has opened, by hand or on its own.'}
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <Select value={status ?? anyValue} onValueChange={(v) => setParam('status', v)}>
            <SelectTrigger className="w-36" aria-label="Filter by status">
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
            <SelectTrigger className="w-36" aria-label="Filter by priority">
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

          {/* Only once there is something to clear. A permanently visible reset invites the
              press that does nothing, and the two selects already say "Any" when they are off. */}
          {filtered && (
            <Button variant="ghost" onClick={clearFilters}>
              <FilterXIcon aria-hidden />
              Clear
            </Button>
          )}
        </div>
      </div>

      <Card className="overflow-hidden py-0">
        {/* table-fixed, so the column widths below are obeyed and the title truncates against the
            width it actually got. Under `auto` the browser widened the table to fit the longest
            title instead, which on a phone pushed the Detected column off the side of the screen
            behind a horizontal scrollbar. */}
        <Table className="table-fixed">
          <TableCaption className="sr-only">
            Incidents, newest first. Each row links to the incident.
          </TableCaption>

          <TableHeader>
            <TableRow>
              <TableHead className="w-[4.5rem] pl-4 md:w-24">Priority</TableHead>
              <TableHead>Incident</TableHead>
              <TableHead className="hidden w-24 md:table-cell">Source</TableHead>
              <TableHead className="hidden w-28 md:table-cell">Status</TableHead>
              <TableHead className="hidden w-44 md:table-cell">Analysis</TableHead>
              <TableHead className="w-20 pr-4 text-right md:w-28 md:pr-2">Detected</TableHead>
              {/* Its own column rather than a sub-line, so the latencies line up and can be read
                  down the column. That comparison is the point of showing them at all. */}
              <TableHead className="hidden w-24 pr-4 text-right md:table-cell">
                Latency
              </TableHead>
            </TableRow>
          </TableHeader>

          <TableBody>
            {query.isPending &&
              Array.from({ length: 5 }).map((_, index) => (
                <TableRow key={index}>
                  <TableCell colSpan={columnCount} className="px-4">
                    <Skeleton className="h-6 w-full" />
                  </TableCell>
                </TableRow>
              ))}

            {query.isError && (
              <TableRow>
                <TableCell
                  colSpan={columnCount}
                  className="text-alarm-ink px-4 py-8 text-center"
                >
                  {query.error instanceof Error ? query.error.message : 'Could not load incidents'}
                </TableCell>
              </TableRow>
            )}

            {query.isSuccess && query.data.items.length === 0 && (
              <TableRow>
                <TableCell colSpan={columnCount} className="px-4 py-10 text-center">
                  {/* A blank grid reads as broken. These are two different situations and only
                      one of them is fixable by touching the filters. */}
                  {filtered ? (
                    <>
                      <p className="text-sm font-medium">Nothing matches these filters.</p>
                      <p className="text-muted-foreground mt-1 text-sm">
                        There are incidents on record; none of them is both{' '}
                        {status ? incidentStatusLabel[status] : 'any status'} and{' '}
                        {priority ?? 'any priority'}.
                      </p>
                      <Button variant="outline" className="mt-3" onClick={clearFilters}>
                        <FilterXIcon aria-hidden />
                        Clear filters
                      </Button>
                    </>
                  ) : (
                    <>
                      <p className="text-sm font-medium">No incidents on record.</p>
                      <p className="text-muted-foreground mt-1 text-sm">
                        Nothing has been opened by hand, and nothing has crossed a detection rule
                        yet.
                      </p>
                    </>
                  )}
                </TableCell>
              </TableRow>
            )}

            {query.data?.items.map((incident) => (
              <IncidentRow key={incident.id} incident={incident} />
            ))}
          </TableBody>
        </Table>
      </Card>

      {query.data && query.data.totalPages > 1 && (
        <div className="flex items-center justify-between gap-4 text-sm">
          <span className="text-muted-foreground tabular-nums">
            Page {query.data.pageNumber} of {query.data.totalPages}
          </span>

          <div className="flex gap-2">
            <Button
              variant="outline"
              disabled={!query.data.hasPreviousPage}
              onClick={() => setParam('page', String(pageNumber - 1))}
            >
              Previous
            </Button>
            <Button
              variant="outline"
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

/**
 * One row, one tab stop.
 *
 * The title is the only interactive thing in the row, which keeps a twenty-row page at twenty tab
 * stops rather than sixty. It is a block filling its cell so the target is the width of the column
 * rather than the width of the text, and the row's hover tint is a reading aid for the line the
 * pointer is on — not a claim that the whole row is clickable.
 *
 * At narrow widths the four middle columns fold into a second line here instead of pushing the
 * table into a horizontal scroll. Nothing is dropped; the same fields arrive in one place.
 */
function IncidentRow({ incident }: { incident: Incident }) {
  const analysis = incident.isAiAnalyzed
    ? `${incident.aiSuggestedCategory ?? 'analysed'} · ${formatConfidence(incident.aiConfidence)}`
    : 'awaiting analysis'

  return (
    <TableRow className="group">
      <TableCell className="pl-4 align-top">
        <Badge variant="outline" className={cn('border', priorityClass[incident.priority])}>
          {incident.priority}
        </Badge>
      </TableCell>

      <TableCell className="align-top">
        <Link
          to={`/incidents/${incident.id}`}
          className="focus-visible:ring-ring/50 -mx-1 block rounded-sm px-1 font-medium outline-none group-hover:underline focus-visible:ring-[3px]"
        >
          <span className="block truncate">{incident.title}</span>
        </Link>

        {incident.assignedTeam && (
          <span className="text-muted-foreground block truncate text-xs">
            {incident.assignedTeam}
          </span>
        )}

        {/* whitespace-normal because the cell's default is nowrap and this line is four fields
            long; on a phone it has to be allowed to wrap rather than truncate away the analysis. */}
        <span className="text-muted-foreground mt-0.5 block text-xs whitespace-normal md:hidden">
          {incidentStatusLabel[incident.status]} · {incident.source} · {analysis}
          {incident.detectedAt &&
            ` · +${formatDuration(incident.detectedAt, incident.createdAt)} to open`}
        </span>
      </TableCell>

      <TableCell className="text-muted-foreground hidden align-top text-sm md:table-cell">
        {incident.source}
      </TableCell>

      <TableCell className="text-muted-foreground hidden align-top text-sm md:table-cell">
        {incidentStatusLabel[incident.status]}
      </TableCell>

      <TableCell className="hidden max-w-44 align-top text-sm md:table-cell">
        {incident.isAiAnalyzed ? (
          <>
            <span className="block truncate">{incident.aiSuggestedCategory ?? 'analysed'}</span>
            {/* formatConfidence renders a null as "not given". It must never become 0% — the
                analysis declining to quantify is not the analysis being certain of nothing. */}
            <span className="text-muted-foreground block text-xs tabular-nums">
              {formatConfidence(incident.aiConfidence)}
            </span>
          </>
        ) : (
          <span className="text-muted-foreground">awaiting analysis</span>
        )}
      </TableCell>

      {/* The latency has its own column from md up; below that it joins the folded line in the
          incident cell, because a phone cannot spare a second column for it. */}
      <TableCell className="pr-4 align-top text-right text-sm tabular-nums md:pr-2">
        {formatRelative(incident.detectedAt ?? incident.createdAt)}
      </TableCell>

      <TableCell className="hidden pr-4 align-top text-right text-sm tabular-nums md:table-cell">
        {incident.detectedAt ? (
          formatDuration(incident.detectedAt, incident.createdAt)
        ) : (
          // Opened by hand: there is no detection to have been slow. An em dash says that; a
          // zero would claim the platform found it instantly.
          <span className="text-dim-foreground" title="Opened by hand — nothing detected it">
            —
          </span>
        )}
      </TableCell>
    </TableRow>
  )
}
