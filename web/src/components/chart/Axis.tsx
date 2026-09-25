import { crisp, type BandScale, type Box, type LinearScale } from './scale'

// The two axes, drawn as quietly as they can be and still be read.
//
// Tufte's rule, applied literally: the ink here is chrome, not data. There is no axis line, no
// ticks and no frame — a horizontal rule at each labelled value does the job of all three, because
// the thing a reader actually does with a bar chart is carry a bar's top sideways to a number. The
// baseline is the one line drawn at full strength, since every bar is measured from it.
//
// Both are `aria-hidden`. An axis is scaffolding for the picture, and a screen reader reading out
// "0 2 4 6 8" has been told nothing; the chart states its own summary and carries the numbers
// themselves in a table beside it.

export function ValueAxis({
  plot,
  scale,
  values,
  format = String,
}: {
  plot: Box
  scale: LinearScale
  values: readonly number[]
  format?: (value: number) => string
}) {
  return (
    <g aria-hidden>
      {values.map((value) => {
        const y = crisp(plot.y + scale.at(value))

        return (
          <g key={value}>
            <line
              x1={plot.x}
              x2={plot.x + plot.width}
              y1={y}
              y2={y}
              // The baseline is structure; the rest are a reading aid and stay under the bars they
              // help measure.
              className={value === 0 ? 'stroke-border' : 'stroke-border/60'}
            />
            <text
              x={plot.x - 8}
              y={y}
              textAnchor="end"
              dominantBaseline="middle"
              className="fill-muted-foreground text-[11px] tabular-nums"
            >
              {format(value)}
            </text>
          </g>
        )
      })}
    </g>
  )
}

export function BandAxis({
  plot,
  band,
  picks,
  format,
  unit,
}: {
  plot: Box
  band: BandScale
  /** The indices that get a label, from `pickBandTicks`. */
  picks: readonly number[]
  format: (index: number) => string
  /** A word pinned in the left margin, level with the labels — the unit the bands were cut in. */
  unit?: string
}) {
  const y = plot.y + plot.height + 15

  return (
    <g aria-hidden>
      {unit && (
        <text
          x={plot.x - 8}
          y={y}
          textAnchor="end"
          dominantBaseline="middle"
          className="fill-dim-foreground text-[10px] font-medium tracking-wide"
        >
          {unit}
        </text>
      )}

      {picks.map((index) => (
        <text
          key={index}
          x={plot.x + band.center(index)}
          y={y}
          textAnchor="middle"
          dominantBaseline="middle"
          className="fill-muted-foreground text-[11px] tabular-nums"
        >
          {format(index)}
        </text>
      ))}
    </g>
  )
}
