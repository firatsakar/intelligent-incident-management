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
 * Kept as a prefix the hubs can reach: `realtime.ts` invalidates `['incident-stats']` when an
 * incident is opened or changes, which is how these numbers stay current without a timer.
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
