import { useCanOperate } from '@/features/auth/AuthProvider'
import { ArrowLeftIcon, ArrowRightIcon, HandIcon, UsersIcon } from 'lucide-react'
import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'

import { InfoHint } from '@/components/InfoHint'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import {
  formatDateTime,
  formatDuration,
  formatRelative,
  priorityClass,
} from '@/lib/format'
import { useT } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { incidentStatuses, type Incident, type IncidentStatus } from '@/types/api'

import { AiAnalysisPanel } from './AiAnalysisPanel'
import { DeliveryStrip } from './DeliveryStrip'
import { IncidentTimeline } from './IncidentTimeline'
import { ScoreBreakdownPanel } from './ScoreBreakdownPanel'
import { useAssignTeam, useDeliveries, useIncident, useUpdateStatus } from './queries'

/**
 * One incident, read as a sequence rather than as a record.
 *
 * The screen is ordered by the question an operator asks at each step: what broke (description),
 * why the gate raised it (the score), when each thing happened (the timeline), what the analysis
 * concluded, and who was told. The detection latency comes before all of it, because it is the one
 * number that distinguishes this platform from a log search with an email rule — and because it is
 * derived from two timestamps the services deliberately keep apart, so it is the first thing that
 * would quietly break if either were confused for the other.
 */
export function IncidentDetailPage() {
  const { labels, incidents } = useT()
  const t = incidents.detail
  const { id = '' } = useParams()

  const incident = useIncident(id)
  const deliveries = useDeliveries(id)

  const canOperate = useCanOperate()
  const updateStatus = useUpdateStatus(id)
  const assignTeam = useAssignTeam(id)

  const [team, setTeam] = useState('')

  if (incident.isPending) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-2/3" />
        <Skeleton className="h-20 w-full" />
        <div className="grid gap-6 lg:grid-cols-3">
          <Skeleton className="h-64 lg:col-span-2" />
          <Skeleton className="h-64" />
        </div>
      </div>
    )
  }

  if (incident.isError) {
    return (
      <div className="space-y-4">
        <BackLink />
        <Alert variant="destructive">
          <AlertTitle>{t.loadErrorTitle}</AlertTitle>
          <AlertDescription>
            {incident.error instanceof Error ? incident.error.message : t.unknownError}
          </AlertDescription>
        </Alert>
      </div>
    )
  }

  const data = incident.data

  return (
    <div className="space-y-6">
      <div className="space-y-3">
        <BackLink />

        <div className="flex flex-wrap items-start justify-between gap-x-6 gap-y-3">
          <div className="min-w-0 space-y-2">
            <h1 className="text-2xl font-semibold tracking-tight break-words">{data.title}</h1>

            <div className="flex flex-wrap items-center gap-x-2 gap-y-1.5 text-sm">
              <Badge variant="outline" className={cn('border', priorityClass[data.priority])}>
                {labels.priority[data.priority]}
              </Badge>
              <Badge variant="secondary">{labels.incidentStatus[data.status]}</Badge>
              <Badge variant="secondary">{labels.incidentSource[data.source]}</Badge>

              {data.assignedTeam && (
                <span className="text-muted-foreground flex min-w-0 items-center gap-1">
                  <UsersIcon className="size-3.5 shrink-0" aria-hidden />
                  <span className="max-w-40 truncate">{data.assignedTeam}</span>
                </span>
              )}

              <span className="text-dim-foreground tabular-nums">
                {t.started(formatRelative(data.detectedAt ?? data.createdAt))}
              </span>
            </div>
          </div>

          {/* Two controls, kept compact and off to the side. This screen is a record of what
              happened, and a pair of form fields given equal weight to the timeline would read as
              though the point of opening it were to edit something.

              Not rendered for a Viewer. Nothing is lost by it: status and team are both on the
              identity row above, and that is where they are read. */}
          {canOperate && (
            <div className="flex flex-wrap items-center gap-2">
              <Select
                value={data.status}
                onValueChange={(value) => updateStatus.mutate(value as IncidentStatus)}
                disabled={updateStatus.isPending}
              >
                <SelectTrigger className="w-36" aria-label={t.statusLabel}>
                  {/* Explicit for the same reason as the list filters: the trigger otherwise shows
                      the raw enum name. */}
                  <SelectValue>{labels.incidentStatus[data.status]}</SelectValue>
                </SelectTrigger>
                <SelectContent>
                  {incidentStatuses.map((status) => (
                    <SelectItem key={status} value={status}>
                      {labels.incidentStatus[status]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>

              <div className="flex items-center gap-1.5">
                <Input
                  value={team}
                  onChange={(event) => setTeam(event.target.value)}
                  // The current team is on the identity row above, not in here as a placeholder: a
                  // placeholder is not a value, it vanishes the moment you type, and an operator
                  // who reads one as the current assignment will believe they cleared it.
                  placeholder={data.assignedTeam ? t.reassignPlaceholder : t.assignPlaceholder}
                  aria-label={data.assignedTeam ? t.reassignLabel : t.assignLabel}
                  className="w-40"
                  onKeyDown={(event) => {
                    if (event.key !== 'Enter' || !team.trim() || assignTeam.isPending) return

                    assignTeam.mutate(team.trim())
                    setTeam('')
                  }}
                />
                <Button
                  variant="outline"
                  disabled={!team.trim() || assignTeam.isPending}
                  onClick={() => {
                    assignTeam.mutate(team.trim())
                    setTeam('')
                  }}
                >
                  {assignTeam.isPending ? t.assigning : t.assign}
                </Button>
              </div>
            </div>
          )}
        </div>
      </div>

      <DetectionStrip incident={data} />

      {/* min-w-0 on both columns, or the page grows a horizontal scrollbar on a phone. A grid item
          defaults to `min-width: auto`, which means min-content, and `break-words` does not reduce
          a long token's min-content contribution — so one unbroken stack-trace frame in the
          description was widening the whole document by the length of the longest file path. */}
      <div className="grid gap-6 lg:grid-cols-3">
        <div className="min-w-0 space-y-6 lg:col-span-2">
          <Card>
            <CardHeader>
              <CardTitle>{t.whatHappened}</CardTitle>
              <CardDescription>
                {data.source === 'Telemetry' ? t.fromDetector : t.fromOperator}
              </CardDescription>
            </CardHeader>
            <CardContent>
              {/* Telemetry writes a structured evidence summary in here, so the whitespace is
                  meaningful and must not be collapsed. break-words rather than overflow: the
                  summary ends in a stack trace whose frames carry absolute file paths, and a
                  horizontal scrollbar hides the one line that says where the crash was. */}
              <pre className="font-sans text-sm leading-relaxed break-words whitespace-pre-wrap">
                {data.description}
              </pre>
            </CardContent>
          </Card>

          <ScoreBreakdownPanel incident={data} />

          <IncidentTimeline incident={data} deliveries={deliveries.data ?? []} />
        </div>

        <div className="min-w-0 space-y-6">
          <AiAnalysisPanel incident={data} />

          <DeliveryStrip deliveries={deliveries.data ?? []} />
        </div>
      </div>
    </div>
  )
}

function BackLink() {
  const { nav } = useT()

  return (
    <Link
      to="/incidents"
      className="text-muted-foreground hover:text-foreground focus-visible:ring-ring/50 -mx-1 inline-flex min-h-6 items-center gap-1.5 rounded-sm px-1 text-sm outline-none focus-visible:ring-[3px]"
    >
      <ArrowLeftIcon className="size-3.5" aria-hidden />
      {nav.items.incidents}
    </Link>
  )
}

/**
 * The headline: two clocks and the gap between them.
 *
 * `detectedAt` is when the problem started, on the source's clock; `createdAt` is when this record
 * was filed, on ours. The services keep them apart precisely so this subtraction is possible, and
 * the result is the clearest single proof that the platform noticed something rather than being
 * told about it. It is therefore the first figure on the screen rather than a detail line inside
 * the timeline.
 *
 * A hand-opened incident has no `detectedAt` at all, and gets a different strip saying so. That is
 * not a degraded version of this one — it is the contrast that makes the number mean anything, and
 * rendering a zero or an em dash there would imply the platform detected something instantly.
 */
function DetectionStrip({ incident }: { incident: Incident }) {
  const t = useT().incidents.detail

  if (!incident.detectedAt) {
    return (
      <Card>
        <CardContent className="flex flex-wrap items-center gap-x-3 gap-y-1.5 py-1">
          <span className="text-muted-foreground flex items-center gap-2 text-sm font-medium">
            <HandIcon className="size-4 shrink-0" aria-hidden />
            {t.openedByHand}
          </span>
          <span className="text-sm tabular-nums">{formatDateTime(incident.createdAt)}</span>
          <span className="text-muted-foreground basis-full text-xs leading-relaxed">
            {t.openedByHandDetail}
          </span>
        </CardContent>
      </Card>
    )
  }

  const latencyMs =
    new Date(incident.createdAt).getTime() - new Date(incident.detectedAt).getTime()

  return (
    <Card>
      <CardContent className="flex flex-col gap-4 py-1 sm:flex-row sm:items-center sm:gap-6">
        <Moment label={t.problemStarted} at={incident.detectedAt} note={t.sourceClock} />

        <ArrowRightIcon
          className="text-muted-foreground hidden size-4 shrink-0 sm:block"
          aria-hidden
        />

        <Moment label={t.incidentOpened} at={incident.createdAt} note={t.ourClock} />

        <div className="sm:ml-auto sm:text-right">
          <p className="text-muted-foreground flex items-center gap-0.5 text-xs font-medium tracking-wider uppercase sm:justify-end">
            {latencyMs < 0 ? t.clockDisagreement : t.detectionLatency}

            <InfoHint
              label={t.latencyHintLabel}
              // Below rather than beside: this hint sits at the top-right of the page, and a
              // popup opening to its left lands squarely on top of the figure it is explaining.
              side="bottom"
              className="-my-1"
            >
              {latencyMs < 0 ? t.skewHint : t.latencyHint}
            </InfoHint>
          </p>

          <p
            className={cn(
              'text-2xl font-semibold tabular-nums',
              latencyMs < 0 && 'text-caution-foreground',
            )}
          >
            {formatDuration(incident.detectedAt, incident.createdAt)}
          </p>

          <p className="text-dim-foreground text-xs">
            {latencyMs < 0 ? t.skewNote : t.noticed}
          </p>
        </div>
      </CardContent>
    </Card>
  )
}

function Moment({ label, at, note }: { label: string; at: string; note: string }) {
  return (
    <div className="min-w-0">
      <p className="text-muted-foreground text-xs font-medium tracking-wider uppercase">
        {label}
      </p>
      <p className="text-sm tabular-nums">{formatDateTime(at)}</p>
      <p className="text-dim-foreground text-xs">{note}</p>
    </div>
  )
}
