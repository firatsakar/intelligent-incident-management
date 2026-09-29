import {
  ArrowRightLeftIcon,
  CircleAlertIcon,
  CirclePlusIcon,
  MessageSquareIcon,
  SparklesIcon,
  UsersIcon,
  type LucideIcon,
} from 'lucide-react'
import { useId, useState } from 'react'

import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { formatDateTime, formatRelative } from '@/lib/format'
import { useT } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import type {
  IncidentActivity,
  IncidentActivityKind,
  IncidentPriority,
  IncidentStatus,
} from '@/types/api'

import { useAddComment, useIncidentActivity } from './queries'

const kindIcon: Record<IncidentActivityKind, LucideIcon> = {
  Opened: CirclePlusIcon,
  StatusChanged: ArrowRightLeftIcon,
  TeamAssigned: UsersIcon,
  AnalysisApplied: SparklesIcon,
  AnalysisFailed: CircleAlertIcon,
  Commented: MessageSquareIcon,
}

// The server's limit, counted the same way: UTF-16 units, after trimming.
const commentMax = 4000

/**
 * What the incident went through and who did it (Adım 14), oldest first, with the comments of
 * the people working it in the same stream.
 *
 * The timeline above answers "when did each stage happen"; this answers "who did what". They are
 * kept apart because they fail differently — the timeline is derived from the incident's own
 * timestamps and is complete for every incident ever opened, while this is a record that only
 * began to be kept at a certain point, and says so rather than letting its first row pass for the
 * beginning.
 */
export function ActivityPanel({
  incidentId,
  canComment,
}: {
  incidentId: string
  canComment: boolean
}) {
  const t = useT().incidents.activity
  const activity = useIncidentActivity(incidentId)

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t.title}</CardTitle>
      </CardHeader>

      <CardContent className="space-y-4">
        {activity.isPending ? (
          <div className="space-y-3">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-2/3" />
          </div>
        ) : activity.isError ? (
          <p className="text-muted-foreground text-sm">{t.loadError}</p>
        ) : (
          <ActivityList rows={activity.data} />
        )}

        {/* Not rendered for a Viewer, as the status and team controls are not: they read the
            history like everyone else and write nothing in it. */}
        {canComment && <CommentBox incidentId={incidentId} />}
      </CardContent>
    </Card>
  )
}

function ActivityList({ rows }: { rows: IncidentActivity[] }) {
  if (rows.length === 0) return null

  return (
    <ol className="space-y-3">
      {rows.map((row) => (
        <ActivityRow key={row.id} row={row} />
      ))}
    </ol>
  )
}

function ActivityRow({ row }: { row: IncidentActivity }) {
  const { incidents, labels } = useT()
  const t = incidents.activity
  const Icon = kindIcon[row.kind]

  const status = (value: string | null) =>
    labels.incidentStatus[value as IncidentStatus] ?? value ?? ''
  const priority = (value: string | null) =>
    labels.priority[value as IncidentPriority] ?? value ?? ''

  const actor =
    row.actorKind === 'Detector'
      ? t.detector
      : row.actorKind === 'Ai'
        ? t.ai
        : row.actorKind === 'ApiKey'
          ? t.apiKey(row.actorName ?? '')
          : (row.actorName ?? t.someone)

  const action = (() => {
    switch (row.kind) {
      case 'Opened':
        return t.opened
      case 'StatusChanged': {
        const moved = t.statusChanged(status(row.from), status(row.to))

        return row.verdict ? t.withDetail(moved, labels.verdict[row.verdict]) : moved
      }
      case 'TeamAssigned':
        return row.from
          ? t.teamReassigned(row.from, row.to ?? '')
          : t.teamAssigned(row.to ?? '')
      case 'AnalysisApplied':
        return t.withDetail(
          t.analysed(row.text),
          row.from === row.to
            ? t.priorityKept(priority(row.to))
            : t.priorityChanged(priority(row.from), priority(row.to)),
        )
      case 'AnalysisFailed':
        return t.analysisFailed
      case 'Commented':
        return t.commented
    }
  })()

  return (
    <li className="grid grid-cols-[auto_minmax(0,1fr)] gap-x-3">
      <span
        className={cn(
          'bg-muted text-muted-foreground mt-0.5 grid size-6 shrink-0 place-items-center rounded-full',
          row.kind === 'AnalysisFailed' && 'text-alarm-ink',
        )}
        aria-hidden
      >
        <Icon className="size-3.5" />
      </span>

      <div className="min-w-0 space-y-1.5">
        <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-0.5">
          <p className="min-w-0 text-sm leading-snug break-words">
            <span className="font-medium">{actor}</span>{' '}
            <span className="text-muted-foreground">{action}</span>
          </p>

          <time
            dateTime={row.at}
            title={formatDateTime(row.at)}
            className="text-dim-foreground shrink-0 text-xs tabular-nums"
          >
            {formatRelative(row.at)}
          </time>
        </div>

        {/* Plain text, as written. The whitespace is the writer's — a pasted command or a list
            reads as one — and a long token wraps rather than widening the page. */}
        {row.kind === 'Commented' && row.text && (
          <p className="bg-muted/60 rounded-md px-3 py-2 text-sm leading-relaxed break-words whitespace-pre-wrap">
            {row.text}
          </p>
        )}

        {row.kind === 'AnalysisFailed' && row.text && (
          <p className="text-muted-foreground text-xs leading-relaxed break-words">{row.text}</p>
        )}
      </div>
    </li>
  )
}

function CommentBox({ incidentId }: { incidentId: string }) {
  const t = useT().incidents.activity
  const fieldId = useId()
  const [text, setText] = useState('')
  const addComment = useAddComment(incidentId)

  const body = text.trim()
  const canSend = body.length > 0 && body.length <= commentMax && !addComment.isPending
  const remaining = commentMax - text.length

  const send = () => {
    if (!canSend) return

    // Cleared on success only: a failed send keeps what was written.
    addComment.mutate(body, { onSuccess: () => setText('') })
  }

  return (
    <form
      className="space-y-2 border-t pt-4"
      onSubmit={(event) => {
        event.preventDefault()
        send()
      }}
    >
      <label htmlFor={fieldId} className="sr-only">
        {t.commentLabel}
      </label>

      <textarea
        id={fieldId}
        value={text}
        onChange={(event) => setText(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
            event.preventDefault()
            send()
          }
        }}
        placeholder={t.commentPlaceholder}
        maxLength={commentMax}
        rows={3}
        className="border-input placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-ring/50 dark:bg-input/30 block min-h-20 w-full resize-y rounded-lg border bg-transparent px-2.5 py-2 text-base outline-none focus-visible:ring-3 md:text-sm"
      />

      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-dim-foreground text-xs">
          {remaining < 500 ? t.remaining(remaining) : t.commentHint}
        </p>

        <Button type="submit" size="sm" disabled={!canSend}>
          {addComment.isPending ? t.sending : t.send}
        </Button>
      </div>
    </form>
  )
}
