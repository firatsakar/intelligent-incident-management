import { cn } from '@/lib/utils'

import { ChartCanvas } from './ChartCanvas'
import { bandScale, linearScale } from './scale'

// A list of horizontal bars on one shared scale.
//
// Horizontal because both things it draws are named by a phrase rather than by a date — pipeline
// stages, integration names — and a vertical chart would either rotate those labels or drop them.
// The bands run down the extent instead of across it, which `bandScale` does without knowing the
// difference: a slot is an offset along a length, and which axis that length lies on is the
// caller's business.
//
// There is no value axis, and that is a decision rather than an omission. Every row prints its own
// figure, already formatted in whatever unit it is really in, so a row of tick labels underneath
// would be a second, rounder copy of numbers that are on screen exactly. What the bars are for is
// the ratio between them — twenty-to-one between two dispatch times, eighty-to-one between two
// funnel stages — and a ratio is the one thing a column of digits does not show.
//
// The track behind each bar is what makes that readable. Without it a bar worth 1% of the window
// is a stub floating in white space; against a drawn extent it is visibly 1% of something.

export interface BarRow {
  key: string
  label: string
  /**
   * The quantity the bar draws. Null means nothing was measured, which draws no bar at all —
   * never a zero-length one, because those are opposite facts. `display` says which it was.
   */
  value: number | null
  /** The figure printed at the end of the row. Formatted by the caller: only it knows the unit. */
  display: string
  /** For a row that reports an absence rather than a value. */
  dim?: boolean
}

const rowHeight = 50
const barHeight = 14
/** Where the bar sits inside its row, under the label line. */
const barOffset = 26

/** A bar that exists has to be visible. Below this it is indistinguishable from an empty track. */
const minBar = 3

export function BarRows({ rows, label }: { rows: BarRow[]; label: string }) {
  const max = Math.max(0, ...rows.map((row) => row.value ?? 0))

  return (
    <ChartCanvas
      height={Math.max(1, rows.length) * rowHeight}
      // No margins: the labels are HTML in the overlay and sit over the plot rather than beside
      // it, so there is no strip of chrome to reserve.
      margin={{ top: 0, right: 0, bottom: 0, left: 0 }}
      label={label}
      overlay={({ plot }) => {
        const band = bandScale(rows.length, plot.height)

        return rows.map((row, index) => (
          <div
            key={row.key}
            className="absolute flex items-baseline justify-between gap-3"
            style={{ left: plot.x, top: plot.y + band.slot(index).x, width: plot.width }}
          >
            {/* Truncated against the width the row actually got, with the whole value on the
                title — service and integration names run long and a wrapped one would push the
                bar out of its row. */}
            <span
              className={cn(
                'min-w-0 truncate text-sm font-medium',
                row.dim && 'text-muted-foreground',
              )}
              title={row.label}
            >
              {row.label}
            </span>

            <span
              className={cn(
                'shrink-0 text-sm font-semibold tabular-nums',
                row.dim && 'text-dim-foreground font-normal',
              )}
            >
              {row.display}
            </span>
          </div>
        ))
      }}
    >
      {({ plot }) => {
        const band = bandScale(rows.length, plot.height)
        const scale = linearScale(max, plot.width)

        return rows.map((row, index) => {
          const top = plot.y + band.slot(index).x + barOffset
          const drawn =
            row.value !== null && row.value > 0
              ? Math.max(minBar, Math.round(scale.span(row.value)))
              : 0

          return (
            <g key={row.key} aria-hidden>
              <rect
                x={plot.x}
                y={top}
                width={plot.width}
                height={barHeight}
                rx={3}
                className="fill-muted"
              />

              {drawn > 0 && (
                <rect
                  x={plot.x}
                  y={top}
                  width={drawn}
                  height={barHeight}
                  rx={3}
                  // Neutral, in both charts that use this. A stage count and a dispatch time are
                  // quantities, not verdicts, and colour here is reserved for the places the
                  // system actually committed to one.
                  className="fill-foreground/55"
                />
              )}
            </g>
          )
        })
      }}
    </ChartCanvas>
  )
}
