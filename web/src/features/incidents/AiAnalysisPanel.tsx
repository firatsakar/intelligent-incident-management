import { InfoHint } from '@/components/InfoHint'
import { Badge } from '@/components/ui/badge'
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { confidenceBand, formatConfidence } from '@/lib/format'
import type { Incident } from '@/types/api'

/**
 * What the analysis concluded, and how sure it was.
 *
 * The confidence bar used to run green / amber / grey off raw Tailwind palette classes — the last
 * place in the console where a colour was written outside the token layer. Replacing them with the
 * status tiers would have been the wrong fix: `nominal` means "worked" and `caution` means "noticed
 * and deliberately not raised", and a model being 93% sure is neither of those. Borrowing a verdict
 * colour for a measurement is how an operator learns that green means a good outcome when it only
 * ever meant the model was confident.
 *
 * So the bar is one fill, and the band is a word. One fill because the length already carries the
 * magnitude and a second channel saying the same thing is decoration; `bg-primary` because the
 * score panel on this same screen already draws a computed magnitude that way, and two bars on one
 * screen should not mean two different things. The word is what satisfies the rule that colour is
 * never the only channel — here by not asking colour to carry anything at all.
 */
export function AiAnalysisPanel({ incident }: { incident: Incident }) {
  // Three states, not two. "Not analysed yet" resolves itself in seconds; "analysis failed"
  // never does, because the thing that would have resolved it is what failed. Rendering both as
  // "waiting" leaves an operator watching for enrichment that is not coming — which is exactly
  // the state the backend gained a field for, and the screen has to spend it.
  if (incident.aiAnalysisError) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>AI analysis</CardTitle>
          <CardDescription>
            The analysis ran and did not produce a result. Nothing further is coming on its own —
            the priority and category below are the ones detection set.
          </CardDescription>
        </CardHeader>

        <CardContent>
          {/* Alarm ink on a tint rather than the solid fill. The solid one is reserved for a
              verdict the system reached about the customer's system; this is the platform
              reporting its own shortfall, which is worth seeing and is not an escalation. */}
          <div className="bg-alarm/10 border-alarm-border/40 rounded-md border p-3">
            <p className="text-alarm-ink text-sm font-medium">Analysis failed</p>
            {/* The provider's own words, unclamped. "Rate limit exceeded" and "invalid API key"
                call for different actions, and a shortened or categorised message hides that
                from the one person who has to choose between them. */}
            <p className="mt-1 text-sm leading-relaxed break-words whitespace-pre-wrap">
              {incident.aiAnalysisError}
            </p>
          </div>

          <p className="text-muted-foreground mt-3 text-xs">
            A later attempt that succeeds clears this and fills the panel in.
          </p>
        </CardContent>
      </Card>
    )
  }

  if (!incident.isAiAnalyzed) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>AI analysis</CardTitle>
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
  const band = confidenceBand(confidence)

  return (
    <Card>
      <CardHeader>
        <CardTitle>AI analysis</CardTitle>
        <CardDescription>
          Enrichment on top of the deterministic gate — it set the category and the priority, not
          whether this was raised.
        </CardDescription>

        {incident.aiSuggestedCategory && (
          <CardAction>
            <Badge variant="secondary">{incident.aiSuggestedCategory}</Badge>
          </CardAction>
        )}
      </CardHeader>

      <CardContent className="space-y-4">
        <div>
          <div className="flex items-baseline gap-1.5 text-sm">
            <span className="text-muted-foreground">Confidence</span>

            <InfoHint label="What the confidence figure means">
              How sure the analysis was of its own category and priority — not how severe the
              incident is, and not how certain the gate was that something broke. Those are the
              score on the left.
            </InfoHint>

            <span className="ml-auto font-medium tabular-nums">
              {formatConfidence(confidence)}
            </span>
          </div>

          {confidence === null ? (
            // Null is a missing measurement, not a zero. An empty bar would read as "certain
            // this is nothing", which is the opposite of what it means.
            <p className="text-muted-foreground mt-1 text-xs">
              The analysis did not put a number on it. That is not the same as being unsure — it
              declined to quantify, so there is nothing to draw.
            </p>
          ) : (
            <>
              <div className="bg-secondary mt-2 h-1.5 w-full overflow-hidden rounded-full">
                <div
                  className="bg-primary h-full rounded-full transition-[width] duration-150 ease-out"
                  style={{ width: `${Math.round(confidence * 100)}%` }}
                />
              </div>

              {band && <p className="text-muted-foreground mt-1.5 text-xs">{band}</p>}
            </>
          )}
        </div>

        {incident.aiReasoning && (
          <div>
            <p className="text-muted-foreground mb-1 text-sm">Reasoning</p>
            {/* Left whole. The model writes several sentences and cites earlier incidents by a
                short id, and a clamp here would cut exactly the corroboration that makes the
                paragraph worth reading. */}
            <p className="text-sm leading-relaxed break-words whitespace-pre-wrap">
              {incident.aiReasoning}
            </p>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
