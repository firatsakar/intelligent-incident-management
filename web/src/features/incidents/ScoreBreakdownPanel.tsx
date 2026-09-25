import { useQuery } from '@tanstack/react-query'

import { telemetryApi } from '@/api/endpoints'
import { InfoHint } from '@/components/InfoHint'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { formatDecimal, formatScore, scoreTerm, scoreTermHelp } from '@/lib/format'
import { useT } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import type { Incident } from '@/types/api'

/** Minutes either side of detectedAt. Generous, because the match is by id, not by time. */
const windowMinutes = 5

/**
 * Why this incident was opened, in the arithmetic the gate actually used.
 *
 * This panel is the product's claim. Everything else on the screen could be an alerting rule with a
 * nicer font; the deterministic terms, their signs and their sum are the part that can be argued
 * with afterwards. Tidying the arithmetic away to make the card calmer would remove the reason the
 * card exists, so the layout spends its space on the numbers and the glossary that makes them mean
 * something.
 *
 * No new endpoint was needed for this. Signal.DetectedAt is carried unchanged through the
 * promotion chain into Incident.DetectedAt, and the evidence query filters signals on DetectedAt
 * — so a narrow window around the incident's own timestamp is guaranteed to contain its signal,
 * and the match is then made on incidentId rather than on time.
 */
export function ScoreBreakdownPanel({ incident }: { incident: Incident }) {
  const t = useT().incidents.score
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

  // A telemetry incident is expected to have a signal, so the wait gets a placeholder rather than
  // nothing — the panel appearing late would shove the timeline down the page. Once the answer is
  // known to be "no signal", the panel goes away instead of explaining its own absence.
  if (query.isPending) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>{t.title}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-4/5" />
          <Skeleton className="h-4 w-2/3" />
        </CardContent>
      </Card>
    )
  }

  const signal = query.data?.signal

  if (!signal) return null

  const components = Object.entries(signal.scoreBreakdown)
    .filter(([key]) => key !== 'total')
    .sort((a, b) => b[1] - a[1])

  // A fatal error skips scoring entirely, so its breakdown carries no `total` key at all. Falling
  // back to the confidence keeps the sum honest; saying which of the two happened keeps the panel
  // honest, because "1.00" arrived by a completely different route in each case.
  const scored = 'total' in signal.scoreBreakdown
  const total = signal.scoreBreakdown.total ?? signal.confidence

  const signature = query.data?.signatures.find(
    (candidate) => candidate.id === signal.errorSignatureId,
  )

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t.title}</CardTitle>
        <CardDescription>{t.description}</CardDescription>
      </CardHeader>

      <CardContent className="space-y-4">
        <ScoreChart components={components} />

        <div className="flex items-baseline justify-between gap-3 border-t pt-3">
          <span className="text-sm font-medium">
            {scored ? t.total : t.confidence}
            <span className="text-muted-foreground ml-2 text-xs font-normal">
              {scored ? t.totalNote : t.confidenceNote}
            </span>
          </span>
          <span className="text-base font-semibold tabular-nums">{formatDecimal(total, 2)}</span>
        </div>

        <p className="text-muted-foreground text-sm break-words">
          {/* The threshold is per-rule and not exposed, so this states what the outcome was
              rather than inventing the number it was compared against. */}
          {signal.reason ?? t.defaultReason}
        </p>

        {signature && (
          <div className="space-y-1.5 border-t pt-3">
            <div className="flex items-center gap-2">
              <p className="text-sm font-medium">{t.signature}</p>
              {signature.isMuted && (
                // Worth surfacing here specifically: `muted` is a scoring term, so a muted
                // signature that was promoted anyway cleared the threshold despite a penalty.
                <span className="bg-inert text-dim-foreground border-inert-border rounded-full border px-2 py-0.5 text-[11px]">
                  {t.muted}
                </span>
              )}
            </div>

            {/* Shown whole rather than truncated. It is 32 characters, it is the key every other
                screen joins on, and half a fingerprint is no use to anybody copying it. */}
            <p className="text-muted-foreground font-mono text-xs break-all">
              {signature.fingerprint}
            </p>
            <p className="text-muted-foreground text-sm break-words">
              {signature.normalizedMessage}
            </p>
            <p className="text-dim-foreground text-xs tabular-nums">
              {t.occurrences(
                signature.occurrenceCount,
                signature.promotionCount,
                signature.confirmedRealCount,
                signature.falsePositiveCount,
              )}
            </p>
          </div>
        )}
      </CardContent>
    </Card>
  )
}

/**
 * The terms, drawn against a zero axis.
 *
 * Two of the gate's eight terms are penalties — a muted signature and a false-positive history —
 * and a penalty rendered as a bar growing rightwards from the left edge reads as a contribution,
 * which is the exact opposite of what it is. So zero is a drawn line and negative bars grow left
 * from it.
 *
 * Where that line sits comes from the data rather than from a fixed centre: a permanent centre axis
 * would throw away half the width on the common breakdown that has no penalties in it at all, and
 * a bar half as long as it could be is a bar that is harder to compare. The bars are normalised
 * against the largest term present, which makes this one decision's proportions legible; the
 * printed numbers are what stay comparable between incidents.
 */
function ScoreChart({ components }: { components: [string, number][] }) {
  const t = useT().incidents.score
  const maxPositive = Math.max(0, ...components.map(([, value]) => value))
  const maxNegative = Math.max(0, ...components.map(([, value]) => -value))
  const span = maxPositive + maxNegative

  // Every term was zero, which the gate can produce and which no bar can draw.
  if (span === 0) {
    return (
      <ul className="space-y-1.5">
        {components.map(([name, value]) => (
          <li key={name} className="flex items-baseline justify-between gap-3 text-sm">
            <span>{scoreTerm(name)}</span>
            <span className="tabular-nums">{formatScore(value)}</span>
          </li>
        ))}
      </ul>
    )
  }

  const zero = (maxNegative / span) * 100

  return (
    <ul className="space-y-2">
      {components.map(([name, value]) => {
        const magnitude = (Math.abs(value) / span) * 100
        const negative = value < 0
        const help = scoreTermHelp(name)

        return (
          <li key={name} className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-x-2">
            <span className="flex min-w-0 items-center gap-0.5 text-sm">
              <span className="truncate">{scoreTerm(name)}</span>
              {/* Only for terms this build has been taught. An unknown key still gets its row and
                  its number; what it does not get is an empty tooltip promising an explanation. */}
              {help && <InfoHint label={t.measuresLabel(scoreTerm(name))}>{help}</InfoHint>}
            </span>

            <span
              className={cn(
                'w-14 text-right text-sm tabular-nums',
                negative && 'text-alarm-ink font-medium',
              )}
            >
              {formatScore(value)}
            </span>

            <span className="bg-secondary relative col-span-2 h-1.5 overflow-hidden rounded-full">
              {maxNegative > 0 && (
                <span
                  className="bg-border absolute inset-y-0 w-px"
                  style={{ left: `${zero}%` }}
                  aria-hidden
                />
              )}

              <span
                className={cn(
                  'absolute inset-y-0 rounded-full',
                  negative ? 'bg-alarm' : 'bg-primary',
                )}
                style={{
                  left: `${negative ? zero - magnitude : zero}%`,
                  width: `${magnitude}%`,
                }}
              />
            </span>
          </li>
        )
      })}
    </ul>
  )
}
