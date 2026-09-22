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

/**
 * The confidence band as a word, so the figure is never carried by colour alone.
 *
 * Null stays null rather than becoming "low": the analysis declining to put a number on it says
 * nothing about how sure it was, and a band invented here would be this file asserting something
 * the model did not.
 */
export function confidenceBand(value: number | null | undefined): string | null {
  if (value === null || value === undefined) return null
  if (value >= 0.8) return 'high confidence'
  if (value >= 0.5) return 'moderate confidence'

  return 'low confidence'
}

export function formatScore(value: number): string {
  const rendered = value.toFixed(2)

  return value > 0 ? `+${rendered}` : rendered
}

// ---- the detection gate's score vocabulary ---------------------------------------------------
//
// SignalScoring emits its breakdown with the key names it uses internally, which are accurate and
// meaningless to anybody who has not read that file. Two screens print those keys — the incident's
// score panel and the signal list — so the translation lives here rather than in either of them.
//
// Kept as data, not prose in a component, because the set grows: the gate has eight terms today and
// only five appear in a typical window, so a term nobody has seen yet must still render as words.

export const scoreTermLabel: Record<string, string> = {
  fatal: 'fatal error',
  burstBase: 'burst base',
  overThreshold: 'over threshold',
  rateAnomaly: 'rate anomaly',
  precedent: 'precedent',
  blastRadius: 'blast radius',
  falsePositivePrecedent: 'false-positive history',
  muted: 'muted signature',
}

/**
 * What each term actually measures — the gate's reasoning, in the operator's language.
 *
 * Deliberately says what the term *is* rather than what it is worth: the weights are constants in
 * SignalScoring and a number copied into the frontend is a number that will quietly go stale.
 */
export const scoreTermHelp: Record<string, string> = {
  fatal: 'The process crashed. A crash is not a judgement call, so it skips scoring entirely and goes straight through.',
  burstBase: 'The starting score every burst gets for clearing its detection rule at all.',
  overThreshold:
    'How far past the rule’s threshold the burst went, counted in doublings and capped — twice over is meaningfully worse, fifty times over is not.',
  rateAnomaly:
    'This signature’s own rate history says this volume is unusual for it. The strongest corroboration available without a second data source.',
  precedent: 'This signature has produced a confirmed real incident before.',
  blastRadius: 'Two or more services are raising it, not one.',
  falsePositivePrecedent:
    'This signature has been marked a false positive before, so the score is pulled down.',
  muted: 'Somebody muted this signature. It is scored, and heavily penalised for being muted.',
}

/**
 * A term the gate emits that this file has not been taught yet. Splitting the camel case is enough
 * to keep it readable, and far better than printing a raw key or, worse, dropping the row — the
 * arithmetic has to add up on screen.
 */
export function scoreTerm(key: string): string {
  return scoreTermLabel[key] ?? key.replace(/([a-z0-9])([A-Z])/g, '$1 $2').toLowerCase()
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
