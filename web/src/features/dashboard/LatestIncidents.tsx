import { ArrowRightIcon } from 'lucide-react'
import { Link } from 'react-router-dom'

import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { formatRelative, priorityClass } from '@/lib/format'
import { useT } from '@/lib/i18n'
import { cn } from '@/lib/utils'
// The one import that crosses a feature boundary, and the reason is the cache rather than
// convenience. `incidentCreated` invalidates `['incidents']` and `incidentChanged` patches every
// cached page that holds the row; both find this list because it is keyed by the incidents
// feature's own query. A private key here would be a list the hub cannot reach, which is a strip
// that says "live" and is not.
import { useIncidents } from '@/features/incidents/queries'

/** Enough to show a shift's worth of arrivals without turning the card into a second list page. */
const rows = 6

export function LatestIncidents() {
  const { labels } = useT()
  const query = useIncidents({ pageNumber: 1, pageSize: rows })

  return (
    <Card className="h-full">
      <CardHeader>
        <CardTitle>Latest incidents</CardTitle>
        <CardDescription>
          The {rows} most recent, whenever they were opened. Arrives over the socket — no refresh,
          and no window.
        </CardDescription>
      </CardHeader>

      <CardContent className="flex-1">
        {query.isPending && (
          <div className="space-y-3">
            {Array.from({ length: 4 }).map((_, index) => (
              <Skeleton key={index} className="h-9 w-full" />
            ))}
          </div>
        )}

        {query.isError && (
          <p className="text-alarm-ink text-sm">
            {query.error instanceof Error ? query.error.message : 'Could not load incidents'}
          </p>
        )}

        {query.isSuccess &&
          (query.data.items.length === 0 ? (
            <div className="py-6">
              <p className="text-sm font-medium">No incidents on record.</p>
              <p className="text-muted-foreground mt-1 text-sm">
                Nothing has been opened by hand, and nothing has crossed a detection rule yet.
              </p>
            </div>
          ) : (
            <ul className="divide-border -my-2 divide-y">
              {query.data.items.map((incident) => (
                <li key={incident.id} className="group flex items-baseline gap-2 py-2">
                  <Badge
                    variant="outline"
                    className={cn('border', priorityClass[incident.priority])}
                  >
                    {incident.priority}
                  </Badge>

                  <span className="min-w-0 flex-1">
                    <Link
                      to={`/incidents/${incident.id}`}
                      className="focus-visible:ring-ring/50 -mx-1 block rounded-sm px-1 text-sm font-medium outline-none group-hover:underline focus-visible:ring-[3px]"
                    >
                      <span className="block truncate">{incident.title}</span>
                    </Link>
                    <span className="text-muted-foreground block truncate text-xs">
                      {labels.incidentStatus[incident.status]} · {incident.source}
                    </span>
                  </span>

                  {/* detectedAt where there is one: when the platform noticed is a different fact
                      from when the record was filed, and this card is about arrivals. */}
                  <span className="text-dim-foreground shrink-0 text-xs tabular-nums">
                    {formatRelative(incident.detectedAt ?? incident.createdAt)}
                  </span>
                </li>
              ))}
            </ul>
          ))}
      </CardContent>

      {query.isSuccess && query.data.items.length > 0 && (
        <CardContent>
          <Link
            to="/incidents"
            className="text-primary focus-visible:ring-ring/50 -mx-1 inline-flex min-h-6 items-center gap-1 rounded-sm px-1 text-sm font-medium outline-none hover:underline focus-visible:ring-[3px]"
          >
            All {query.data.totalCount} incidents
            <ArrowRightIcon className="size-3.5" aria-hidden />
          </Link>
        </CardContent>
      )}
    </Card>
  )
}
