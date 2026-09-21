import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'

import { integrationsApi } from '@/api/endpoints'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { incidentStatusLabel, priorityClass } from '@/lib/format'
import { cn } from '@/lib/utils'
import { incidentStatuses, type IncidentStatus } from '@/types/api'

import { AiAnalysisPanel } from './AiAnalysisPanel'
import { DeliveryStrip } from './DeliveryStrip'
import { IncidentTimeline } from './IncidentTimeline'
import { useAssignTeam, useDeliveries, useIncident, useUpdateStatus } from './queries'

export function IncidentDetailPage() {
  const { id = '' } = useParams()

  const incident = useIncident(id)
  const deliveries = useDeliveries(id)

  // Delivery rows carry an integration id and nothing else, so the names come from here.
  const integrations = useQuery({
    queryKey: ['integrations'],
    queryFn: integrationsApi.list,
  })

  const updateStatus = useUpdateStatus(id)
  const assignTeam = useAssignTeam(id)

  const [team, setTeam] = useState('')

  if (incident.isPending) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-2/3" />
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }

  if (incident.isError) {
    return (
      <Alert variant="destructive">
        <AlertTitle>Could not load this incident</AlertTitle>
        <AlertDescription>
          {incident.error instanceof Error ? incident.error.message : 'Unknown error'}
        </AlertDescription>
      </Alert>
    )
  }

  const data = incident.data

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <Link to="/incidents" className="text-muted-foreground text-sm hover:underline">
          ← Incidents
        </Link>

        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="space-y-2">
            <h1 className="text-2xl font-semibold tracking-tight">{data.title}</h1>

            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="outline" className={cn('border', priorityClass[data.priority])}>
                {data.priority}
              </Badge>
              <Badge variant="secondary">{incidentStatusLabel[data.status]}</Badge>
              <Badge variant="secondary">{data.source}</Badge>
              {data.assignedTeam && (
                <span className="text-muted-foreground text-sm">· {data.assignedTeam}</span>
              )}
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <Select
              value={data.status}
              onValueChange={(value) => updateStatus.mutate(value as IncidentStatus)}
              disabled={updateStatus.isPending}
            >
              <SelectTrigger className="w-40">
                {/* Explicit for the same reason as the list filters: the trigger otherwise shows
                    the raw enum name. */}
                <SelectValue>{incidentStatusLabel[data.status]}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                {incidentStatuses.map((status) => (
                  <SelectItem key={status} value={status}>
                    {incidentStatusLabel[status]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Input
              value={team}
              onChange={(event) => setTeam(event.target.value)}
              placeholder={data.assignedTeam ?? 'Assign a team'}
              className="w-44"
            />
            <Button
              variant="outline"
              disabled={!team.trim() || assignTeam.isPending}
              onClick={() => {
                assignTeam.mutate(team.trim())
                setTeam('')
              }}
            >
              Assign
            </Button>
          </div>
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Description</CardTitle>
            </CardHeader>
            <CardContent>
              {/* Telemetry writes a structured evidence summary in here, so the whitespace is
                  meaningful and must not be collapsed. */}
              <pre className="text-sm leading-relaxed whitespace-pre-wrap font-sans">
                {data.description}
              </pre>
            </CardContent>
          </Card>

          <IncidentTimeline incident={data} deliveries={deliveries.data ?? []} />
        </div>

        <div className="space-y-6">
          <AiAnalysisPanel incident={data} />

          <DeliveryStrip
            deliveries={deliveries.data ?? []}
            integrations={integrations.data ?? []}
          />
        </div>
      </div>
    </div>
  )
}
