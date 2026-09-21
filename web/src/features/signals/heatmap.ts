import type { Signal, SignalStatus } from '@/types/api'

// The aggregation behind the map. Kept apart from the component because it is the part with
// decisions in it, and because the map recomputes from the cached signal list on every render —
// a pushed signal updates it with no request, and signals that age out of the window drop off
// the same recomputation without a timer.

/** How many signature columns to show before everything else is folded into "other". */
const maxColumns = 8

export interface HeatCell {
  service: string
  errorKey: string
  occurrences: number
  signalCount: number
  /** The strongest verdict reached in this cell, which is what the marker encodes. */
  band: SignalStatus
}

export interface HeatMap {
  services: string[]
  columns: string[]
  cells: Map<string, HeatCell>
  max: number
  /** Signals with no signature attached, which cannot be placed. Counted rather than hidden. */
  unplaced: number
}

export const otherColumn = 'other'

/** Ranked strongest first; the marker shows whichever the cell reached. */
const bandRank: Record<SignalStatus, number> = {
  Promoted: 4,
  Deduplicated: 3,
  Weak: 2,
  Recorded: 1,
  Suppressed: 0,
}

export function errorKeyOf(signal: Signal): string {
  // Exception type where there is one, because it is the shortest thing that still identifies
  // the failure. The normalised message is the fallback, truncated so a column stays a column.
  if (signal.exceptionType) {
    const short = signal.exceptionType.includes('.')
      ? signal.exceptionType.slice(signal.exceptionType.lastIndexOf('.') + 1)
      : signal.exceptionType

    return short
  }

  const message = signal.normalizedMessage ?? 'unknown'

  return message.length > 40 ? `${message.slice(0, 40)}…` : message
}

export function cellKey(service: string, errorKey: string): string {
  return `${service}${errorKey}`
}

export function buildHeatMap(signals: Signal[]): HeatMap {
  const placed = signals.filter((signal) => signal.service)
  const unplaced = signals.length - placed.length

  // Rank the signatures by total occurrences so the columns that survive the cut are the ones
  // worth the width.
  const byError = new Map<string, number>()

  for (const signal of placed) {
    const key = errorKeyOf(signal)
    byError.set(key, (byError.get(key) ?? 0) + signal.occurrenceCount)
  }

  const ranked = [...byError.entries()].sort((a, b) => b[1] - a[1]).map(([key]) => key)

  const kept = new Set(ranked.slice(0, maxColumns))
  const hasOverflow = ranked.length > maxColumns

  const cells = new Map<string, HeatCell>()
  const services = new Set<string>()

  for (const signal of placed) {
    const service = signal.service!
    const raw = errorKeyOf(signal)
    const errorKey = kept.has(raw) ? raw : otherColumn

    services.add(service)

    const key = cellKey(service, errorKey)
    const existing = cells.get(key)

    if (existing) {
      existing.occurrences += signal.occurrenceCount
      existing.signalCount += 1

      if (bandRank[signal.status] > bandRank[existing.band]) existing.band = signal.status
    } else {
      cells.set(key, {
        service,
        errorKey,
        occurrences: signal.occurrenceCount,
        signalCount: 1,
        band: signal.status,
      })
    }
  }

  const columns = ranked.slice(0, maxColumns)

  if (hasOverflow) columns.push(otherColumn)

  return {
    services: [...services].sort(),
    columns,
    cells,
    max: Math.max(0, ...[...cells.values()].map((cell) => cell.occurrences)),
    unplaced,
  }
}

/**
 * Occurrence counts are skewed — one storm produces thirty and the background produces one — so
 * a linear ramp leaves the background indistinguishable from empty.
 *
 * A log ramp was tried first and over-corrected: against a max of 30 it put 30 and 15 on the
 * same shade, because log flattens the top of a short range. Two-to-one is exactly the kind of
 * difference a map exists to show.
 *
 * Square root sits between the two. It lifts the small counts clear of the empty cells while
 * keeping the ordering visible at the top: 30 / 15 / 1 land on distinct steps, and so do
 * 80 / 2 / 2.
 */
export function intensityOf(occurrences: number, max: number): number {
  if (occurrences <= 0 || max <= 0) return 0

  const scaled = Math.sqrt(occurrences / max)

  // Discrete steps rather than a continuous ramp: with a handful of cells, five distinguishable
  // shades read better than a gradient nobody can compare by eye. Anything non-zero gets at
  // least the first step, so "rare" never renders as "absent".
  return Math.min(5, Math.max(1, Math.ceil(scaled * 5)))
}
