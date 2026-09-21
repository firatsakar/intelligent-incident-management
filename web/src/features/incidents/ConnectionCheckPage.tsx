import { useQuery } from '@tanstack/react-query'

import { incidentsApi } from '@/api/endpoints'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'

// Temporary, and deliberately not a fake: it makes a real call through the proxy to a real
// service. If the proxy table, the query wiring or a service is wrong, this says so plainly
// rather than rendering an empty shell that looks fine.
export function ConnectionCheckPage() {
  const incidents = useQuery({
    queryKey: ['incidents', { pageNumber: 1, pageSize: 1 }],
    queryFn: () => incidentsApi.list({ pageNumber: 1, pageSize: 1 }),
  })

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Connected</h1>
        <p className="text-muted-foreground text-sm">
          The screens arrive with their chunks. This page proves the path to the services.
        </p>
      </div>

      {incidents.isPending && <Skeleton className="h-28 w-full max-w-md" />}

      {incidents.isError && (
        <Alert variant="destructive" className="max-w-md">
          <AlertTitle>Could not reach IncidentService</AlertTitle>
          <AlertDescription>
            {incidents.error instanceof Error ? incidents.error.message : 'Unknown error'}
            <span className="mt-2 block text-xs">
              Check that the service is running on 5203 and that the Vite proxy is in front of it.
            </span>
          </AlertDescription>
        </Alert>
      )}

      {incidents.isSuccess && (
        <Card className="max-w-md">
          <CardHeader>
            <CardTitle>IncidentService</CardTitle>
            <CardDescription>Reached through the dev proxy, no CORS involved.</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-3xl font-semibold tabular-nums">{incidents.data.totalCount}</p>
            <p className="text-muted-foreground text-sm">incidents on record</p>
          </CardContent>
        </Card>
      )}
    </div>
  )
}
