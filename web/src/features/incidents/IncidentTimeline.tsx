import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatDateTime, formatDuration } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { Incident, NotificationDelivery } from '@/types/api'

interface Stage {
  label: string
  at: string | null
  detail?: string
  pending?: boolean
  /** A stage that happened but whose exact moment the record does not keep. */
  done?: boolean
}

/**
 * An incident is a sequence, not a form. The spine is the timestamps the backend keeps
 * deliberately distinct: when the problem started, when the record was filed, when the analysis
 * landed, when people were told.
 *
 * The gap between the first two is detection latency, and it is the single clearest piece of
 * evidence that the platform noticed something on its own rather than being told about it.
 */
export function IncidentTimeline({
  incident,
  deliveries,
}: {
  incident: Incident
  deliveries: NotificationDelivery[]
}) {
  const sent = deliveries
    .filter((delivery) => delivery.sentAt)
    .sort((a, b) => (a.sentAt! < b.sentAt! ? -1 : 1))

  const stages: Stage[] = [
    incident.detectedAt
      ? {
          label: 'Problem started',
          at: incident.detectedAt,
          detail: 'On the source clock, not ours.',
        }
      : {
          label: 'Problem started',
          at: null,
          detail: 'Not recorded — this incident was opened by hand.',
        },
    {
      label: 'Incident opened',
      at: incident.createdAt,
      detail: incident.detectedAt
        ? `${formatDuration(incident.detectedAt, incident.createdAt)} after it started`
        : undefined,
    },
    // No timestamp, and deliberately so. The incident record does not store when the analysis
    // landed, and updatedAt moves on every change — a status transition an hour later would make
    // this stage claim the analysis happened then. A missing time is better than a wrong one.
    incident.isAiAnalyzed
      ? {
          label: 'Analysis applied',
          at: null,
          done: true,
          detail: incident.aiSuggestedCategory
            ? `Categorised as ${incident.aiSuggestedCategory}, priority set to ${incident.priority}`
            : 'Applied.',
        }
      : {
          label: 'Analysis applied',
          at: null,
          pending: true,
          detail: 'Waiting on the analysis service.',
        },
    sent.length > 0
      ? {
          label: 'People notified',
          at: sent[0]!.sentAt,
          detail: `${sent.length} of ${deliveries.length} channel(s) delivered`,
        }
      : {
          label: 'People notified',
          at: null,
          pending: deliveries.length === 0,
          detail:
            deliveries.length === 0
              ? 'No delivery recorded yet.'
              : 'Every configured channel failed — see below.',
        },
  ]

  if (incident.updatedAt) {
    // Honest about what updatedAt is: the last time anything about this incident changed, by a
    // human or by the analysis. It is not the analysis timestamp.
    stages.push({
      label: 'Last changed',
      at: incident.updatedAt,
      detail: 'Any edit — status, team, or the analysis landing.',
    })
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Timeline</CardTitle>
      </CardHeader>

      <CardContent>
        <ol className="relative space-y-6 border-l pl-6">
          {stages.map((stage) => (
            <li key={stage.label} className="relative">
              <span
                className={cn(
                  'absolute -left-[1.6875rem] top-1.5 size-3 rounded-full border-2',
                  stage.at || stage.done
                    ? 'bg-primary border-primary'
                    : stage.pending
                      ? 'bg-background border-muted-foreground/40'
                      : 'bg-background border-muted-foreground/20',
                )}
              />

              <div className="flex flex-wrap items-baseline justify-between gap-2">
                <span
                  className={cn(
                    'font-medium',
                    !stage.at && !stage.done && 'text-muted-foreground',
                  )}
                >
                  {stage.label}
                </span>
                <span className="text-muted-foreground text-sm tabular-nums">
                  {stage.at ? formatDateTime(stage.at) : stage.done ? 'done' : '—'}
                </span>
              </div>

              {stage.detail && (
                <p className="text-muted-foreground mt-0.5 text-sm">{stage.detail}</p>
              )}
            </li>
          ))}
        </ol>
      </CardContent>
    </Card>
  )
}
