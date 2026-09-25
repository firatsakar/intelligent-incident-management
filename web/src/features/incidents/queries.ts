import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'

import { incidentsApi, notificationsApi, type IncidentListParams } from '@/api/endpoints'
import { useT } from '@/lib/i18n'
import type { Incident, IncidentStatus, IncidentVerdict } from '@/types/api'

// Query keys mirror the URL shape, so an invalidation reads like the screen it affects.
export const incidentKeys = {
  all: ['incidents'] as const,
  list: (params: IncidentListParams) => ['incidents', params] as const,
  detail: (id: string) => ['incident', id] as const,
  deliveries: (id: string) => ['deliveries', id] as const,
}

export function useIncidents(params: IncidentListParams) {
  return useQuery({
    queryKey: incidentKeys.list(params),
    queryFn: () => incidentsApi.list(params),
  })
}

export function useIncident(id: string) {
  return useQuery({
    queryKey: incidentKeys.detail(id),
    queryFn: () => incidentsApi.byId(id),
  })
}

export function useDeliveries(incidentId: string) {
  return useQuery({
    queryKey: incidentKeys.deliveries(incidentId),
    queryFn: () => notificationsApi.byIncident(incidentId),
  })
}

/**
 * The PATCH endpoints return 204, so there is no updated incident to write into the cache. The
 * hub will push one moments later — but waiting for a round trip to see your own click feels
 * broken, so the cache is patched locally and the pushed version overwrites it.
 */
function patchCached(
  queryClient: ReturnType<typeof useQueryClient>,
  id: string,
  change: Partial<Incident>,
) {
  queryClient.setQueryData<Incident>(incidentKeys.detail(id), (current) =>
    current ? { ...current, ...change } : current,
  )
}

export function useUpdateStatus(id: string) {
  const queryClient = useQueryClient()
  const t = useT().incidents.detail

  return useMutation({
    mutationFn: ({ status, verdict }: { status: IncidentStatus; verdict?: IncidentVerdict }) =>
      incidentsApi.updateStatus(id, status, verdict),
    onSuccess: (_, { status }) => {
      patchCached(queryClient, id, { status })

      // The list is filtered and paginated on the server, and a status change can move a row off
      // the current page. Only the server knows, so the list is re-asked rather than guessed.
      // The detail too: closing and reopening set and clear the verdict and the time, and those
      // are the server's to state rather than this screen's to guess.
      void queryClient.invalidateQueries({ queryKey: incidentKeys.all })
      void queryClient.invalidateQueries({ queryKey: incidentKeys.detail(id) })

      toast.success(t.statusUpdated)
    },
    onError: (error: Error) => toast.error(error.message),
  })
}

export function useAssignTeam(id: string) {
  const queryClient = useQueryClient()
  const t = useT().incidents.detail

  return useMutation({
    mutationFn: (team: string) => incidentsApi.assignTeam(id, team),
    onSuccess: (_, team) => {
      patchCached(queryClient, id, { assignedTeam: team })
      void queryClient.invalidateQueries({ queryKey: incidentKeys.all })

      toast.success(t.assignedTo(team))
    },
    onError: (error: Error) => toast.error(error.message),
  })
}
