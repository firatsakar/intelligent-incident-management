import { useQuery } from '@tanstack/react-query'

import { telemetryApi } from '@/api/endpoints'
import { resolveWindow, type WindowPreset } from '@/lib/window'

/**
 * The funnel and the per-service rollup arrive in one payload, so the two screens that draw them
 * share a query rather than asking for the same thing twice. Moving between /funnel and /services
 * at the same window is a cache hit and no request at all.
 *
 * The window vocabulary is `lib/window.ts` — the same presets the signal map and the evidence
 * explorer use, because these read the same log store over the same kind of span. The default is
 * 24 hours rather than that file's two, which is the default the endpoint itself applies: a funnel
 * asked over thirty minutes is mostly a report about how recently the seeder ran.
 */
export const defaultStatsWindow: WindowPreset = '24h'

/**
 * Keyed by the preset, not by the instants it resolves to. `resolveWindow` reads the clock, so a
 * key built from its output would be a new key on every render and turn a dashboard into a request
 * loop. The instants go into the request; the preset identifies it.
 *
 * Kept as a reachable prefix: nothing invalidates `['telemetry-stats']` yet, and the signal hub's
 * handler that should is Parça 6's.
 */
export const telemetryStatsKeys = {
  all: ['telemetry-stats'] as const,
  window: (preset: string) => ['telemetry-stats', preset] as const,
}

export function useTelemetryStats(preset: string) {
  const range = resolveWindow(preset)

  return useQuery({
    queryKey: telemetryStatsKeys.window(preset),
    queryFn: () => telemetryApi.stats(range),
  })
}
