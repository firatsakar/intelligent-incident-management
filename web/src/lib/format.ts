import type { DeliveryStatus, IncidentPriority, LogSeverity, SignalStatus } from '@/types/api'

// Timestamps arrive as UTC ISO strings and are rendered in the operator's own zone. An incident
// timeline that mixes zones is worse than useless.
const dateTime = new Intl.DateTimeFormat(undefined, {
  dateStyle: 'medium',
  timeStyle: 'medium',
})

const timeOnly = new Intl.DateTimeFormat(undefined, { timeStyle: 'medium' })

export function formatDateTime(value: string | null | undefined): string {
  return value ? dateTime.format(new Date(value)) : '—'
}

export function formatTime(value: string | null | undefined): string {
  return value ? timeOnly.format(new Date(value)) : '—'
}

/**
 * The gap between two moments, in the largest unit that still reads as a number. Used for
 * detection latency, which is the clearest single proof that the platform noticed something on
 * its own rather than being told.
 */
export function formatDuration(fromIso: string, toIso: string): string {
  const ms = new Date(toIso).getTime() - new Date(fromIso).getTime()

  if (!Number.isFinite(ms)) return '—'

  const abs = Math.abs(ms)
  const sign = ms < 0 ? '-' : ''

  if (abs < 1000) return `${sign}${abs}ms`
  if (abs < 60_000) return `${sign}${(abs / 1000).toFixed(1)}s`
  if (abs < 3_600_000) return `${sign}${Math.floor(abs / 60_000)}m ${Math.round((abs % 60_000) / 1000)}s`

  return `${sign}${Math.floor(abs / 3_600_000)}h ${Math.round((abs % 3_600_000) / 60_000)}m`
}

export function formatRelative(value: string | null | undefined): string {
  if (!value) return '—'

  const ms = Date.now() - new Date(value).getTime()

  if (ms < 60_000) return 'just now'
  if (ms < 3_600_000) return `${Math.floor(ms / 60_000)}m ago`
  if (ms < 86_400_000) return `${Math.floor(ms / 3_600_000)}h ago`

  return `${Math.floor(ms / 86_400_000)}d ago`
}

/** Null confidence means the analysis declined to give one, which is not the same as zero. */
export function formatConfidence(value: number | null | undefined): string {
  return value === null || value === undefined ? 'not given' : `${Math.round(value * 100)}%`
}

export function formatScore(value: number): string {
  const rendered = value.toFixed(2)

  return value > 0 ? `+${rendered}` : rendered
}

// ---- colour vocabulary ----------------------------------------------------------------------
//
// One place, so a priority looks the same in a table row, a badge and a timeline.

export const priorityClass: Record<IncidentPriority, string> = {
  Critical: 'bg-red-600/15 text-red-700 dark:text-red-400 border-red-600/30',
  High: 'bg-orange-500/15 text-orange-700 dark:text-orange-400 border-orange-500/30',
  Medium: 'bg-amber-500/15 text-amber-700 dark:text-amber-400 border-amber-500/30',
  Low: 'bg-slate-500/15 text-slate-700 dark:text-slate-400 border-slate-500/30',
}

export const severityClass: Record<LogSeverity, string> = {
  Fatal: 'text-red-600 dark:text-red-400 font-semibold',
  Error: 'text-red-600 dark:text-red-400',
  Warning: 'text-amber-600 dark:text-amber-400',
  Information: 'text-sky-600 dark:text-sky-400',
  Debug: 'text-muted-foreground',
  Verbose: 'text-muted-foreground',
}

export const deliveryClass: Record<DeliveryStatus, string> = {
  Sent: 'bg-emerald-600/15 text-emerald-700 dark:text-emerald-400 border-emerald-600/30',
  Failed: 'bg-red-600/15 text-red-700 dark:text-red-400 border-red-600/30',
  Pending: 'bg-slate-500/15 text-slate-700 dark:text-slate-400 border-slate-500/30',
}

export const signalStatusClass: Record<SignalStatus, string> = {
  Promoted: 'bg-red-600/15 text-red-700 dark:text-red-400 border-red-600/30',
  Weak: 'bg-amber-500/15 text-amber-700 dark:text-amber-400 border-amber-500/30',
  Recorded: 'bg-slate-500/15 text-slate-700 dark:text-slate-400 border-slate-500/30',
  Deduplicated: 'bg-sky-500/15 text-sky-700 dark:text-sky-400 border-sky-500/30',
  Suppressed: 'bg-slate-500/15 text-slate-600 dark:text-slate-500 border-slate-500/30',
}

/** Human labels for the enum names, which are fine in JSON and clumsy on screen. */
export const signalStatusLabel: Record<SignalStatus, string> = {
  Promoted: 'Promoted',
  Weak: 'Weak',
  Recorded: 'Recorded only',
  Deduplicated: 'Deduplicated',
  Suppressed: 'Suppressed',
}

export const incidentStatusLabel: Record<string, string> = {
  Open: 'Open',
  InProgress: 'In progress',
  Resolved: 'Resolved',
  Closed: 'Closed',
}
