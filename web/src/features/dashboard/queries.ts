import { useQuery } from '@tanstack/react-query'

import { incidentsApi } from '@/api/endpoints'

import { dayRange, type DayWindow } from './window'

/**
 * The aggregate behind every card on this screen.
 *
 * Keyed by the window alone, not by the resolved instants. `dayRange` reads the clock, so keying on
 * its output would mint a new cache entry on every render and turn a dashboard into a request loop.
 * The instants go into the request; the window identifies it.
 *
 * Kept as a prefix the hubs can reach. `realtime.ts` writes the counters straight into every
 * cached window when an incident arrives or moves — a new incident is +1 in its priority and in
 * its own UTC day, which has one possible answer — and follows it with one coalesced invalidate
 * for the parts that do not: the detection percentiles, and the window's own edges. So the
 * dashboard moves at the instant of the push, and a storm costs one refetch rather than one per
 * incident.
 */
export const dashboardKeys = {
  stats: (days: DayWindow) => ['incident-stats', days] as const,
}

export function useIncidentStats(days: DayWindow) {
  const range = dayRange(days)

  return useQuery({
    queryKey: dashboardKeys.stats(days),
    queryFn: () => incidentsApi.stats(range),
  })
}
