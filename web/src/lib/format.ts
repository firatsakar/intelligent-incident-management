import { activeDictionary, activeIntlLocale } from '@/lib/i18n/active'
import type { DeliveryStatus, IncidentPriority, LogSeverity, SignalStatus } from '@/types/api'

/**
 * Every `Intl` object this file needs, built once per locale.
 *
 * They used to be module constants built with an `undefined` locale — the browser's. That was
 * right while the console spoke one language and wrong the moment it spoke two: someone on an
 * English browser who chooses Turkish would have read Turkish words above English dates. The
 * locale now comes from the chosen language, and the bundle is rebuilt when that changes.
 *
 * Rebuilt lazily rather than pushed from the provider, so this file keeps no subscription and the
 * i18n module keeps no knowledge of formatting. Constructing an `Intl.DateTimeFormat` is not free,
 * which is the whole reason they were hoisted in the first place, so the bundle is cached and the
 * cache is checked by locale rather than invalidated by hand.
 */
function buildFormatters(locale: string) {
  return {
    // Timestamps arrive as UTC ISO strings and are rendered in the operator's own zone. An
    // incident timeline that mixes zones is worse than useless.
    dateTime: new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'medium' }),
    timeOnly: new Intl.DateTimeFormat(locale, { timeStyle: 'medium' }),
    wholeNumber: new Intl.NumberFormat(locale, { maximumFractionDigits: 0 }),

    // One fraction digit for a duration, two for a score. Fixed rather than maximum, because a
    // score panel whose rows are 0,55 and 0,3 does not read as arithmetic.
    oneDecimal: new Intl.NumberFormat(locale, {
      minimumFractionDigits: 1,
      maximumFractionDigits: 1,
    }),
    twoDecimals: new Intl.NumberFormat(locale, {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }),

    // Day buckets are cut in UTC on the server, so their labels are pinned to UTC here rather
    // than passed through the operator's zone like every other timestamp in this file.
    // Formatting them locally would move a bucket by a day for anyone west of Greenwich, and the
    // label would then quietly disagree with the number it labels. The screens that draw these
    // say "UTC" on the axis.
    utcDay: new Intl.DateTimeFormat(locale, { month: 'short', day: 'numeric', timeZone: 'UTC' }),
    utcDayLong: new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeZone: 'UTC' }),
  }
}

let cache: { locale: string; formatters: ReturnType<typeof buildFormatters> } | null = null

function intl() {
  const locale = activeIntlLocale()

  if (!cache || cache.locale !== locale) cache = { locale, formatters: buildFormatters(locale) }

  return cache.formatters
}

/**
 * A count, with whatever thousands separator the operator's locale uses.
 *
 * Only the volume figures need it — a log window runs to five and six digits where everything else
 * in this console is a handful — but they need it badly: 124038 and 12403 are the same shape at a
 * glance, and the aggregate screens exist to be glanced at.
 */
export function formatCount(value: number): string {
  return intl().wholeNumber.format(value)
}

/**
 * A score, a total, anything with a fixed number of decimals.
 *
 * `toFixed` was doing this and always writes a dot. Turkish writes a comma, and the console was
 * already writing a comma for thousands through `formatCount` — so the same screen had a dot
 * meaning "decimal" and a dot meaning "thousands".
 */
export function formatDecimal(value: number, digits: 1 | 2): string {
  return digits === 1 ? intl().oneDecimal.format(value) : intl().twoDecimals.format(value)
}

/**
 * A percentage with its sign where the language puts it.
 *
 * English writes 38%, Turkish writes %38. It was a literal `%` in five components, which is a
 * punctuation mark carrying a grammar rule.
 */
export function formatPercent(value: number): string {
  return activeDictionary().format.percent(value)
}

export function formatDateTime(value: string | null | undefined): string {
  return value ? intl().dateTime.format(new Date(value)) : '—'
}

export function formatTime(value: string | null | undefined): string {
  return value ? intl().timeOnly.format(new Date(value)) : '—'
}

/**
 * A span in the largest unit that still reads as a number.
 *
 * The units come from the dictionary. They are as much text as "just now" is, and leaving them
 * here put "20 sa önce" and "1m 30s" in adjacent columns of the same incident row.
 */
function renderSpan(ms: number): string {
  const { span } = activeDictionary().format

  const abs = Math.abs(ms)
  const sign = ms < 0 ? '-' : ''

  // Rounded, because this branch is no longer only reached from two Dates. `formatSeconds` hands
  // it a double now — a dispatch median of 0.073502 seconds — and unrounded that prints as
  // "73.502ms", which claims a precision the measurement does not have.
  if (abs < 1000) return `${sign}${span.milliseconds(Math.round(abs))}`
  if (abs < 60_000) return `${sign}${span.seconds(formatDecimal(abs / 1000, 1))}`

  if (abs < 3_600_000) {
    return `${sign}${span.minutesSeconds(Math.floor(abs / 60_000), Math.round((abs % 60_000) / 1000))}`
  }

  return `${sign}${span.hoursMinutes(Math.floor(abs / 3_600_000), Math.round((abs % 3_600_000) / 60_000))}`
}

/**
 * The gap between two moments. Used for detection latency, which is the clearest single proof that
 * the platform noticed something on its own rather than being told.
 */
export function formatDuration(fromIso: string, toIso: string): string {
  const ms = new Date(toIso).getTime() - new Date(fromIso).getTime()

  if (!Number.isFinite(ms)) return '—'

  return renderSpan(ms)
}

/**
 * The same span for the aggregates, which report a latency as seconds rather than as two instants.
 *
 * It shares `renderSpan` rather than rounding to its own taste, so a median on the dashboard and a
 * gap on an incident cannot describe the same four minutes two different ways.
 *
 * A null is the aggregate declining to answer — nothing was measured — and renders as an em dash.
 * It must never become 0, which would claim the opposite fact.
 */
export function formatSeconds(seconds: number | null | undefined): string {
  if (seconds === null || seconds === undefined || !Number.isFinite(seconds)) return '—'

  return renderSpan(seconds * 1000)
}

/** A `YYYY-MM-DD` UTC bucket as a short axis label. */
export function formatUtcDay(day: string): string {
  return intl().utcDay.format(new Date(`${day}T00:00:00Z`))
}

/** The same bucket spelled out, for a readout that has room for it. */
export function formatUtcDayLong(day: string): string {
  return intl().utcDayLong.format(new Date(`${day}T00:00:00Z`))
}

export function formatRelative(value: string | null | undefined): string {
  if (!value) return '—'

  const { format } = activeDictionary()
  const ms = Date.now() - new Date(value).getTime()

  if (ms < 60_000) return format.justNow
  if (ms < 3_600_000) return format.minutesAgo(Math.floor(ms / 60_000))
  if (ms < 86_400_000) return format.hoursAgo(Math.floor(ms / 3_600_000))

  return format.daysAgo(Math.floor(ms / 86_400_000))
}

/** Null confidence means the analysis declined to give one, which is not the same as zero. */
export function formatConfidence(value: number | null | undefined): string {
  return value === null || value === undefined
    ? activeDictionary().format.notGiven
    : formatPercent(Math.round(value * 100))
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

  const { confidence } = activeDictionary().format

  if (value >= 0.8) return confidence.high
  if (value >= 0.5) return confidence.moderate

  return confidence.low
}

export function formatScore(value: number): string {
  const rendered = formatDecimal(value, 2)

  return value > 0 ? `+${rendered}` : rendered
}

// ---- the detection gate's score vocabulary ---------------------------------------------------
//
// The words moved to the dictionary; what stays here is the lookup and its fallback, because the
// fallback is behaviour rather than text.

/**
 * A term the gate emits that the dictionary has not been taught. Splitting the camel case is
 * enough to keep it readable, and far better than printing a raw key or, worse, dropping the row —
 * the arithmetic has to add up on screen.
 */
export function scoreTerm(key: string): string {
  const label = activeDictionary().scoreTerms.label as Record<string, string | undefined>

  return label[key] ?? key.replace(/([a-z0-9])([A-Z])/g, '$1 $2').toLowerCase()
}

/** Undefined for a term this build has not been taught: an unknown key still gets its row and its
 *  number, but not an empty tooltip promising an explanation. */
export function scoreTermHelp(key: string): string | undefined {
  const help = activeDictionary().scoreTerms.help

  return (help as Record<string, string | undefined>)[key]
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

/**
 * Exported so a screen can map a state of its own onto the shared tiers without retyping the class
 * triples — which is how a seventh colour gets invented. Which tier a state lands on is the
 * screen's judgement; what a tier looks like is not.
 */
export const statusTier = {
  alarm: 'bg-alarm text-alarm-foreground border-alarm-border',
  elevated: 'bg-elevated text-elevated-foreground border-elevated-border',
  caution: 'bg-caution text-caution-foreground border-caution-border',
  nominal: 'bg-nominal text-nominal-foreground border-nominal-border',
  info: 'bg-info text-info-foreground border-info-border',
  inert: 'bg-inert text-inert-foreground border-inert-border',
} as const

export const priorityClass: Record<IncidentPriority, string> = {
  Critical: statusTier.alarm,
  High: statusTier.elevated,
  Medium: statusTier.caution,
  Low: statusTier.inert,
}

// The same four priorities as a filled area, for the charts. A tier is a badge — a pale tint with
// an ink printed on it — and neither half of that survives being stretched into a bar, so the fill
// is its own token. Two maps rather than one composed name, because a class assembled at runtime is
// a class Tailwind never sees and therefore never generates.

/** Inside an `<svg>`. */
export const priorityFill: Record<IncidentPriority, string> = {
  Critical: 'fill-priority-critical',
  High: 'fill-priority-high',
  Medium: 'fill-priority-medium',
  Low: 'fill-priority-low',
}

/** The same colour for a proportion bar drawn in HTML, where there is nothing to measure. */
export const priorityBackground: Record<IncidentPriority, string> = {
  Critical: 'bg-priority-critical',
  High: 'bg-priority-high',
  Medium: 'bg-priority-medium',
  Low: 'bg-priority-low',
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
  Sent: statusTier.nominal,
  Failed: statusTier.alarm,
  Pending: statusTier.inert,
}

export const signalStatusClass: Record<SignalStatus, string> = {
  Promoted: statusTier.alarm,
  Weak: statusTier.caution,
  Recorded: statusTier.inert,
  // Suppressed is the one state the gate silenced on purpose, so it sits a step quieter than
  // Recorded rather than sharing its ink.
  Suppressed: 'bg-inert text-dim-foreground border-inert-border',
  Deduplicated: statusTier.info,
}

// The enum labels that used to live here are in the dictionary now — `labels.signalStatus`,
// `labels.incidentStatus` and the seven others — because they are text on screen and this file
// no longer owns text. What stays here is colour, which is not language.
