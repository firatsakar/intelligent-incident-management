// The dashboard's window, in whole UTC days.
//
// Deliberately not `lib/window.ts`. That one is minutes wide — thirty minutes to seven days — and
// is shared by the signal map and the evidence explorer, which both read a live stream. Adding
// ninety days to it would put "Last 90 days" in those two screens' pickers, where it would pull
// a quarter of the log store through a page that exists to show the last two hours. Different
// question, different vocabulary.

export const dayWindows = [7, 30, 90] as const

export type DayWindow = (typeof dayWindows)[number]

export const defaultDayWindow: DayWindow = 30

/** The URL is the source of truth and anyone can type into it, so an unknown value is a default. */
export function resolveDayWindow(value: string | null): DayWindow {
  const parsed = Number(value)

  return dayWindows.find((days) => days === parsed) ?? defaultDayWindow
}

/**
 * `days` whole UTC days, ending with today.
 *
 * The server buckets by UTC day, so the window starts at a UTC midnight rather than at "now minus
 * N times twenty-four hours". Asked the second way, the oldest bucket is a part-day and draws as a
 * short bar for a reason that is nowhere on screen — and "7 days" comes back as eight columns.
 *
 * Today's own column is still partial, which is inherent rather than fixable: it is the day that is
 * still happening. The chart says so in words.
 */
export function dayRange(days: DayWindow): { from: string; to: string } {
  const now = new Date()
  const midnight = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate())

  return {
    from: new Date(midnight - (days - 1) * 86_400_000).toISOString(),
    to: now.toISOString(),
  }
}

/** How the window is named in a card's own scope line. */
export function dayWindowLabel(days: DayWindow): string {
  return `last ${days} days`
}
