import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { formatDateTime, formatDuration } from '@/lib/format'
import { useT } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import type { Incident, NotificationDelivery } from '@/types/api'

/**
 * Where a stage got to. A discriminator rather than a pair of booleans, because the four states are
 * genuinely different things and the marker has to tell them apart by shape as well as by weight —
 * "it happened and we know when", "it happened and the record does not keep when", "it has not
 * happened yet", and "it never will".
 */
type StageState = 'reached' | 'untimed' | 'failed' | 'pending' | 'absent'

interface Stage {
  label: string
  at: string | null
  state: StageState
  detail?: string
  /** Elapsed since the previous timed stage, which is what makes this read as a sequence. */
  since?: string
}

/**
 * An incident is a sequence, not a form. The spine is the timestamps the backend keeps
 * deliberately distinct: when the problem started, when the record was filed, when the analysis
 * landed, when people were told.
 *
 * The gap between the first two is detection latency, and it is the single clearest piece of
 * evidence that the platform noticed something on its own rather than being told about it. It is
 * stated twice on purpose — as the headline strip at the top of the screen, and here in the place
 * that proves it is a gap between two real recorded moments rather than a figure we assert.
 */
export function IncidentTimeline({
  incident,
  deliveries,
}: {
  incident: Incident
  deliveries: NotificationDelivery[]
}) {
  const { incidents, labels } = useT()
  const t = incidents.timeline

  const sent = deliveries
    .filter((delivery) => delivery.sentAt)
    .sort((a, b) => (a.sentAt! < b.sentAt! ? -1 : 1))

  const firstSentAt = sent[0]?.sentAt ?? null

  const stages: Stage[] = [
    incident.detectedAt
      ? {
          label: t.problemStarted,
          at: incident.detectedAt,
          state: 'reached',
          detail: t.onSourceClock,
        }
      : {
          label: t.problemStarted,
          at: null,
          state: 'absent',
          detail: incident.reportedBy ? t.notSent : t.notRecorded,
        },
    {
      label: t.incidentOpened,
      at: incident.createdAt,
      state: 'reached',
      since: incident.detectedAt
        ? t.toDetect(formatDuration(incident.detectedAt, incident.createdAt))
        : undefined,
      detail: incident.detectedAt ? t.ourClockGap : undefined,
    },
    // No timestamp, and deliberately so. The incident record does not store when the analysis
    // landed, and updatedAt moves on every change — a status transition an hour later would make
    // this stage claim the analysis happened then. A missing time is better than a wrong one.
    incident.isAiAnalyzed
      ? {
          label: t.analysisApplied,
          at: null,
          state: 'untimed',
          detail: incident.aiSuggestedCategory
            ? t.categorised(incident.aiSuggestedCategory, labels.priority[incident.priority])
            : t.applied,
        }
      : incident.aiAnalysisError
        ? {
            // Not 'pending'. A pending stage says "this has not happened yet", and on a spine
            // whose whole argument is that the timestamps are real, a stage that will never
            // happen must not sit there looking like one that still might.
            label: t.analysisApplied,
            at: null,
            state: 'failed',
            detail: t.analysisFailed,
          }
        : {
            label: t.analysisApplied,
            at: null,
            state: 'pending',
            detail: t.analysisWaiting,
          },
    firstSentAt
      ? {
          label: t.peopleNotified,
          at: firstSentAt,
          state: 'reached',
          since: t.afterOpening(formatDuration(incident.createdAt, firstSentAt)),
          detail: t.channelsDelivered(sent.length, deliveries.length),
        }
      : {
          label: t.peopleNotified,
          at: null,
          state: deliveries.length === 0 ? 'pending' : 'absent',
          detail: deliveries.length === 0 ? t.noDeliveryYet : t.everyChannelFailed,
        },
  ]

  if (incident.updatedAt) {
    // Honest about what updatedAt is: the last time anything about this incident changed, by a
    // human or by the analysis. It is not the analysis timestamp, and it must never be moved up
    // to sit beside one — an earlier version of this screen did exactly that and mis-stated the
    // time on the first incident it was checked against.
    stages.push({
      label: t.lastChanged,
      at: incident.updatedAt,
      state: 'reached',
      detail: t.anyEdit,
    })
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t.title}</CardTitle>
        <CardDescription>{t.description}</CardDescription>
      </CardHeader>

      <CardContent>
        <ol className="space-y-4">
          {stages.map((stage) => (
            <li
              key={stage.label}
              className="group/stage grid grid-cols-[auto_minmax(0,1fr)] gap-x-3"
            >
              {/* The rail is a flex child that fills whatever height the row turned out to be,
                  rather than an absolutely positioned line at a hand-measured offset — the rows
                  are different heights and the offsets would only be right on one of them. It is
                  suppressed on the last stage so the line never runs past the final marker. */}
              <span className="flex w-3 shrink-0 flex-col items-center pt-1.5" aria-hidden>
                <StageMarker state={stage.state} />
                <span className="bg-border mt-1 w-px flex-1 group-last/stage:hidden" />
              </span>

              <div className="min-w-0 pb-0.5">
                <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-0.5">
                  <span
                    className={cn(
                      'text-sm font-medium',
                      (stage.state === 'pending' || stage.state === 'absent') &&
                        'text-muted-foreground',
                    )}
                  >
                    {stage.label}
                  </span>

                  <span className="text-muted-foreground text-xs tabular-nums">
                    {stage.at ? (
                      formatDateTime(stage.at)
                    ) : stage.state === 'untimed' ? (
                      // "done" rather than a time, and said as a word so nobody reads an em dash
                      // as "never happened".
                      <span className="text-foreground">{t.doneUntimed}</span>
                    ) : stage.state === 'pending' ? (
                      t.notYet
                    ) : (
                      '—'
                    )}
                  </span>
                </div>

                {stage.since && (
                  <p className="text-foreground mt-0.5 text-xs font-medium tabular-nums">
                    {stage.since}
                  </p>
                )}

                {stage.detail && (
                  <p className="text-muted-foreground mt-0.5 text-xs leading-relaxed">
                    {stage.detail}
                  </p>
                )}
              </div>
            </li>
          ))}
        </ol>
      </CardContent>
    </Card>
  )
}

/**
 * Five markers, five shapes. Colour alone would leave "happened" and "never happened" as two
 * shades of the same dot, which is the one distinction on this card that actually matters.
 */
function StageMarker({ state }: { state: StageState }) {
  if (state === 'reached') {
    return <span className="bg-primary border-primary size-3 shrink-0 rounded-full border-2" />
  }

  // Happened, but the moment is unknown: a filled centre inside an open ring, so it reads as
  // "reached" at a glance and as "not the same as the others" on a second look.
  if (state === 'untimed') {
    return (
      <span className="border-primary bg-card grid size-3 shrink-0 place-items-center rounded-full border-2">
        <span className="bg-primary size-1 rounded-full" />
      </span>
    )
  }

  // Tried and came back with nothing. A solid ring like 'reached', so it reads as a stage that
  // ran, in alarm ink so it does not read as one that succeeded — and hollow, so it is not
  // mistaken for a completed step at a glance.
  if (state === 'failed') {
    return <span className="border-alarm-ink bg-card size-3 shrink-0 rounded-full border-2" />
  }

  if (state === 'pending') {
    return <span className="border-input bg-card size-3 shrink-0 rounded-full border-2" />
  }

  return (
    <span className="border-input bg-card size-3 shrink-0 rounded-full border-2 border-dashed" />
  )
}
