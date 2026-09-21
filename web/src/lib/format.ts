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
// Four enums, one shared set of tiers, defined as tokens in index.css. Nothing here writes a
// `dark:` twin: the token carries both themes, so a colour cannot drift apart between them the
// way hand-paired light and dark classes eventually do.
//
// The tiers, and what earns each one:
//
//   alarm     solid fill   the system committed and it is bad — Critical, Failed, Promoted
//   elevated  orange tint  above the line but below a verdict
//   caution   amber tint   noticed, deliberately not raised
//   nominal   green tint   worked
//   info      blue tint    folded into something already open
//   inert     neutral tint nothing happened; this is most rows, most of the time
//
// Only `alarm` is a solid fill, and only three states reach it. That is the point: if four things
// on a screen shout, the operator learns to read past all four.
//
// Colour is never the only channel here — every one of these renders next to its own label.

const tier = {
  alarm: 'bg-alarm text-alarm-foreground border-alarm-border',
  elevated: 'bg-elevated text-elevated-foreground border-elevated-border',
  caution: 'bg-caution text-caution-foreground border-caution-border',
  nominal: 'bg-nominal text-nominal-foreground border-nominal-border',
  info: 'bg-info text-info-foreground border-info-border',
  inert: 'bg-inert text-inert-foreground border-inert-border',
} as const

export const priorityClass: Record<IncidentPriority, string> = {
  Critical: tier.alarm,
  High: tier.elevated,
  Medium: tier.caution,
  Low: tier.inert,
}

/**
 * Severity is drawn as bare text inside a log line rather than as a badge, so it uses the
 * severity ink tokens — tuned against the page, not against a tint. Debug and Verbose stay
 * achromatic and differ only in weight of grey: they are the volume, not the news.
 */
export const severityClass: Record<LogSeverity, string> = {
  Fatal: 'text-severity-fatal font-semibold',
  Error: 'text-severity-error',
  Warning: 'text-severity-warning',
  Information: 'text-severity-info',
  Debug: 'text-muted-foreground',
  Verbose: 'text-dim-foreground',
}

export const deliveryClass: Record<DeliveryStatus, string> = {
  Sent: tier.nominal,
  Failed: tier.alarm,
  Pending: tier.inert,
}

export const signalStatusClass: Record<SignalStatus, string> = {
  Promoted: tier.alarm,
  Weak: tier.caution,
  Recorded: tier.inert,
  // Suppressed is the one state the gate silenced on purpose, so it sits a step quieter than
  // Recorded rather than sharing its ink.
  Suppressed: 'bg-inert text-dim-foreground border-inert-border',
  Deduplicated: tier.info,
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
