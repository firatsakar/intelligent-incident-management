import { useQuery } from '@tanstack/react-query'

import { notificationsApi } from '@/api/endpoints'
import { resolveWindow, type WindowPreset } from '@/lib/window'

/**
 * Delivery health across a window, which until now existed nowhere.
 *
 * The per-incident delivery strip answers "did anyone hear about this one". It cannot answer "is
 * this channel working", and a channel failing quietly for a day was therefore invisible: every
 * incident's own page would show one failed row, and nobody reads every incident's page.
 *
 * Seven days by default — the endpoint's own default, and the span over which a channel that has
 * stopped is still distinguishable from a quiet afternoon. The presets themselves are the shared
 * `lib/window.ts` vocabulary, so the three screens in this section all take the same window.
 */
export const defaultDeliveryWindow: WindowPreset = '7d'

/**
 * Keyed by the preset, not by the instants it resolves to — `resolveWindow` reads the clock, and a
 * key built from its output would be a new key on every render.
 *
 * `['notification-stats']` is deliberately a reachable prefix. The notification hub invalidates it
 * — coalesced — when a delivery is recorded, and writes a renamed or deleted integration's name
 * straight into the rows. A `refetchInterval` here instead would put back exactly the traffic the
 * sockets exist to remove.
 */
export const notificationStatsKeys = {
  all: ['notification-stats'] as const,
  window: (preset: string) => ['notification-stats', preset] as const,
}

export function useNotificationStats(preset: string) {
  const range = resolveWindow(preset)

  return useQuery({
    queryKey: notificationStatsKeys.window(preset),
    queryFn: () => notificationsApi.stats(range),
  })
}
