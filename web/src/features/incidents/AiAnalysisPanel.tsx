import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatConfidence } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { Incident } from '@/types/api'

export function AiAnalysisPanel({ incident }: { incident: Incident }) {
  if (!incident.isAiAnalyzed) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="text-base">AI analysis</CardTitle>
        </CardHeader>
        <CardContent>
          {/* An explicit waiting state rather than an empty panel: the analysis arrives seconds
              after the incident, and a blank card reads as "nothing to say" instead of "not yet". */}
          <p className="text-muted-foreground text-sm">
            Waiting for the analysis service. It reads the evidence summary carried on the
            incident, so no extra call is made on its behalf.
          </p>
        </CardContent>
      </Card>
    )
  }

  const confidence = incident.aiConfidence

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between space-y-0">
        <CardTitle className="text-base">AI analysis</CardTitle>
        {incident.aiSuggestedCategory && (
          <Badge variant="secondary">{incident.aiSuggestedCategory}</Badge>
        )}
      </CardHeader>

      <CardContent className="space-y-4">
        <div>
          <div className="flex items-baseline justify-between text-sm">
            <span className="text-muted-foreground">Confidence</span>
            <span className="font-medium tabular-nums">{formatConfidence(confidence)}</span>
          </div>

          {confidence === null ? (
            // Null is a missing measurement, not a zero. An empty bar would read as "certain
            // this is nothing", which is the opposite of what it means.
            <p className="text-muted-foreground mt-1 text-xs">
              The analysis did not put a number on it.
            </p>
          ) : (
            <div className="bg-secondary mt-2 h-2 w-full overflow-hidden rounded-full">
              <div
                className={cn(
                  'h-full rounded-full transition-all',
                  confidence >= 0.8
                    ? 'bg-emerald-600'
                    : confidence >= 0.5
                      ? 'bg-amber-500'
                      : 'bg-slate-400',
                )}
                style={{ width: `${Math.round(confidence * 100)}%` }}
              />
            </div>
          )}
        </div>

        {incident.aiReasoning && (
          <div>
            <p className="text-muted-foreground mb-1 text-sm">Reasoning</p>
            <p className="text-sm leading-relaxed whitespace-pre-wrap">{incident.aiReasoning}</p>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
