import { useQuery } from '@tanstack/react-query'

import { telemetryApi } from '@/api/endpoints'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { formatScore } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { Incident } from '@/types/api'

/** Minutes either side of detectedAt. Generous, because the match is by id, not by time. */
const windowMinutes = 5

/**
 * Why this incident was opened, in the arithmetic the gate actually used.
 *
 * No new endpoint was needed for this. Signal.DetectedAt is carried unchanged through the
 * promotion chain into Incident.DetectedAt, and the evidence query filters signals on DetectedAt
 * — so a narrow window around the incident's own timestamp is guaranteed to contain its signal,
 * and the match is then made on incidentId rather than on time.
 */
export function ScoreBreakdownPanel({ incident }: { incident: Incident }) {
  const detectedAt = incident.detectedAt

  const query = useQuery({
    queryKey: ['incident-signal', incident.id],
    enabled: Boolean(detectedAt),
    queryFn: async () => {
      const centre = new Date(detectedAt!).getTime()

      const evidence = await telemetryApi.evidence({
        from: new Date(centre - windowMinutes * 60_000).toISOString(),
        to: new Date(centre + windowMinutes * 60_000).toISOString(),
      })

      return {
        signal: evidence.signals.find((candidate) => candidate.incidentId === incident.id) ?? null,
        signatures: evidence.signatures,
      }
    },
  })

  // A manually opened incident has no signal behind it, and saying nothing is the right amount
  // to say about that.
  if (!detectedAt || incident.source !== 'Telemetry') return null

  const signal = query.data?.signal

  if (!signal) return null

  const components = Object.entries(signal.scoreBreakdown)
    .filter(([key]) => key !== 'total')
    .sort((a, b) => b[1] - a[1])

  const total = signal.scoreBreakdown.total ?? signal.confidence

  const signature = query.data?.signatures.find(
    (candidate) => candidate.id === signal.errorSignatureId,
  )

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Why this was raised</CardTitle>
        <CardDescription>
          A deterministic score, not a judgement call. Every term is recorded so the decision can
          be argued with afterwards.
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-4">
        <ul className="space-y-1.5">
          {components.map(([name, value]) => (
            <li key={name} className="flex items-center gap-3 text-sm">
              <span className="w-40 shrink-0">{name}</span>

              <span className="bg-secondary h-2 flex-1 overflow-hidden rounded-full">
                <span
                  className={cn(
                    'block h-full rounded-full',
                    value < 0 ? 'bg-destructive' : 'bg-primary',
                  )}
                  style={{ width: `${Math.min(100, Math.abs(value) * 100)}%` }}
                />
              </span>

              <span
                className={cn(
                  'w-14 shrink-0 text-right tabular-nums',
                  value < 0 && 'text-destructive',
                )}
              >
                {formatScore(value)}
              </span>
            </li>
          ))}
        </ul>

        <div className="flex items-center justify-between border-t pt-3 text-sm font-medium">
          <span>Total</span>
          <span className="tabular-nums">{total.toFixed(2)}</span>
        </div>

        <p className="text-muted-foreground text-sm">
          {/* The threshold is per-rule and not exposed, so this states what the outcome was
              rather than inventing the number it was compared against. */}
          {signal.reason ?? 'It met the promotion threshold for its detection rule.'}
        </p>

        {signature && (
          <div className="text-muted-foreground space-y-1 border-t pt-3 text-sm">
            <p className="text-foreground font-medium">Signature</p>
            <p className="break-all font-mono text-xs">{signature.fingerprint}</p>
            <p>{signature.normalizedMessage}</p>
            <p className="tabular-nums">
              {signature.occurrenceCount} occurrence(s) recorded in total · promoted{' '}
              {signature.promotionCount}×, {signature.confirmedRealCount} confirmed real,{' '}
              {signature.falsePositiveCount} false positive
            </p>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
