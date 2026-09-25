import { AlertTriangleIcon, CircleCheckIcon, XIcon } from 'lucide-react'
import type { ComponentType, ReactNode } from 'react'

import { formatTime } from '@/lib/format'
import { T, useT } from '@/lib/i18n'
import { cn } from '@/lib/utils'

/**
 * The vocabulary the two catalogue screens are built from.
 *
 * Integrations and Telemetry ask the operator for the same thing in opposite directions — connect
 * an external system, see what is connected, prove it answers — so they are one pattern shown
 * twice. What lives here is the part of that pattern with no knowledge of either domain: the rule
 * over a section, the notice that states an operational gap, the inert row for something not built
 * yet, and the strip that reports a probe the operator just ran.
 *
 * Extracted when the second screen arrived, not in anticipation of it.
 */

/** Both lucide icons and the local vendor marks take a className; nothing else is asked of them. */
export type MarkComponent = ComponentType<{ className?: string }>

/** Something the roadmap names and the code does not implement. Inert on purpose. The words come
 *  from whichever catalogue is drawing it, because only that screen knows which union names it. */
export interface PlannedEntry {
  name: string
  mark: MarkComponent
  summary: string
}

export function SectionHeading({ title, detail }: { title: string; detail?: string }) {
  return (
    <div className="flex items-baseline gap-3">
      <h3 className="text-muted-foreground text-xs font-medium tracking-wider uppercase">
        {title}
      </h3>
      <span className="bg-border h-px flex-1" aria-hidden />
      {detail && <span className="text-muted-foreground text-xs tabular-nums">{detail}</span>}
    </div>
  )
}

/**
 * A stated fact about the configuration, not a verdict the gate reached — so `caution` rather than
 * the solid alarm fill, with an icon and words carrying it alongside the tint.
 */
export function Notice({ tone, children }: { tone: 'warn' | 'bad'; children: ReactNode }) {
  return (
    <p
      role={tone === 'bad' ? 'alert' : 'status'}
      className={cn(
        'flex items-start gap-2 rounded-lg border px-3 py-2 text-sm',
        tone === 'warn'
          ? 'bg-caution text-caution-foreground border-caution-border'
          : 'border-alarm-border/70 bg-alarm/10 text-alarm-ink',
      )}
    >
      <AlertTriangleIcon className="mt-0.5 size-4 shrink-0" aria-hidden />
      <span className="min-w-0">{children}</span>
    </p>
  )
}

export function PlannedRow({ entry }: { entry: PlannedEntry }) {
  const t = useT().settings.shared

  return (
    // min-w-0 because this is a grid item, and a grid track's floor is its content unless it is
    // told otherwise — without it the `truncate` below has nothing to truncate against and the
    // longest planned name widens the whole page. Found at 320px on the telemetry screen, where
    // "OTLP log alımı" and its summary are longer than anything the integrations grid holds.
    <div className="border-inert-border flex min-w-0 items-center gap-3 rounded-xl border border-dashed px-3 py-2.5">
      <span className="bg-muted/50 text-dim-foreground grid size-9 shrink-0 place-items-center rounded-lg">
        <entry.mark className="size-5" />
      </span>

      <div className="min-w-0">
        <p className="text-muted-foreground truncate text-sm font-medium">{entry.name}</p>
        <p className="text-dim-foreground truncate text-xs">{entry.summary}</p>
      </div>

      {/* Not a disabled button. A control that cannot ever be pressed is still a control, and it
          invites the press that does nothing. This is a label. */}
      <span className="border-inert-border text-dim-foreground ml-auto shrink-0 rounded-full border px-2 py-0.5 text-[11px] whitespace-nowrap">
        {t.comingSoon}
      </span>
    </div>
  )
}

/**
 * The last probe the operator ran, kept on the row rather than in a toast. A toast is gone before a
 * long SMTP error or somebody else's stack trace can be read, and this button is the only feedback
 * loop a customer has while setting a connection up.
 */
export interface TestOutcome {
  ok: boolean
  detail: string
  at: string
}

export function TestReport({
  outcome,
  okLabel,
  failLabel,
  onDismiss,
}: {
  outcome: TestOutcome
  okLabel: string
  failLabel: string
  onDismiss: () => void
}) {
  const t = useT().settings.shared

  return (
    <div
      role="status"
      className={cn(
        'mt-2 flex items-start gap-2 rounded-md border px-2.5 py-1.5 text-xs',
        outcome.ok
          ? 'bg-nominal text-nominal-foreground border-nominal-border'
          : // Not the solid alarm fill. That is reserved for a verdict the system reached on its
            // own; this is the output of a probe the operator just ran, and the reason can run to
            // several lines of somebody else's exception text.
            'border-alarm-border/70 bg-alarm/10 text-alarm-ink',
      )}
    >
      {outcome.ok ? (
        <CircleCheckIcon className="mt-px size-3.5 shrink-0" aria-hidden />
      ) : (
        <AlertTriangleIcon className="mt-px size-3.5 shrink-0" aria-hidden />
      )}

      <span className="min-w-0 flex-1 break-words">
        <T
          text={t.testReport}
          values={{
            status: <span className="font-medium">{outcome.ok ? okLabel : failLabel}</span>,
            time: formatTime(outcome.at),
            detail: outcome.detail,
          }}
        />
      </span>

      <button
        type="button"
        onClick={onDismiss}
        aria-label={t.dismissTest}
        className="focus-visible:ring-ring/50 -my-0.5 -mr-1 grid size-6 shrink-0 place-items-center rounded-sm outline-none hover:opacity-70 focus-visible:ring-3"
      >
        <XIcon className="size-3.5" aria-hidden />
      </button>
    </div>
  )
}
