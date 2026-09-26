import { useState, type KeyboardEvent } from 'react'

import { BandAxis, ValueAxis } from '@/components/chart/Axis'
import { ChartCanvas } from '@/components/chart/ChartCanvas'
import {
  bandScale,
  linearScale,
  niceTicks,
  pickBandTicks,
  stackOffsets,
} from '@/components/chart/scale'
import { formatUtcDay, formatUtcDayLong, priorityFill } from '@/lib/format'
import { T, useT } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { incidentPriorities, type IncidentDayBucket } from '@/types/api'

// Daily arrivals, stacked by priority.
//
// Drawn by hand because a charting library is a dependency this project has not taken, and because
// what it would buy — scales, ticks, a bar — is the part that is forty lines. What it would cost is
// a second theming system: every one of them ships its own palette, its own tooltip and its own
// idea of a focus ring, and none of those knows about the tokens in index.css.
//
// The stack always runs Critical at the base. The order is the second channel: three of the four
// fills are a warm ramp, which is the hue set roughly one man in twelve cannot take apart, so the
// position of a segment has to carry what the colour does. It is fixed for every day and it is what
// the legend lists.

const chartHeight = 236

// Left holds up to three digits of count, bottom holds one line of day label.
//
// The right margin holds nothing at all, and is there anyway: day labels are centred on their band
// and the last band ends at the plot's edge, so half of "22 Eyl" hangs past it and the SVG clips
// what it cannot fit. Measured at a phone width against the longest label this formatter produces,
// then rounded up.
const margin = { top: 10, right: 24, bottom: 26, left: 38 }

/** Roughly the width of "Sep 22" at 11px, plus enough air to read two of them side by side. */
const labelGap = 52

/** Seven days across a wide card would otherwise draw as seven slabs. */
const maxStep = 56

export function IncidentsByDayChart({ days }: { days: IncidentDayBucket[] }) {
  const { dashboard, labels } = useT()
  const t = dashboard.chart

  // Which day the readout is describing. Hover wins while the pointer is over the plot; otherwise
  // the keyboard cursor does, and only while the plot actually holds focus — a cursor left showing
  // after focus has gone is a readout that disagrees with the screen.
  const [hovered, setHovered] = useState<number | null>(null)
  const [focused, setFocused] = useState(false)
  const [cursor, setCursor] = useState<number | null>(null)

  const count = days.length
  // The window can change under the cursor, and an index into the old one points at nothing.
  const safeCursor = cursor === null ? count - 1 : Math.min(cursor, count - 1)
  const active = hovered ?? (focused ? safeCursor : null)
  const activeDay = active !== null && active >= 0 ? days[active] : undefined

  const totals = days.map((day) => day.total)
  const busiest = totals.length > 0 ? Math.max(...totals) : 0
  const windowTotal = totals.reduce((sum, value) => sum + value, 0)

  // What closed each day (Adım 20.8), drawn as a line over the bars. The axis has to reach
  // whichever is higher, or a day that closed more than it opened draws off the plot.
  const closed = days.map((day) => day.resolved ?? 0)
  const closedTotal = closed.reduce((sum, value) => sum + value, 0)
  const peak = Math.max(busiest, ...closed, 0)

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (count === 0) return

    const last = count - 1

    const next =
      event.key === 'ArrowRight'
        ? Math.min(safeCursor + 1, last)
        : event.key === 'ArrowLeft'
          ? Math.max(safeCursor - 1, 0)
          : event.key === 'Home'
            ? 0
            : event.key === 'End'
              ? last
              : null

    if (next === null) return

    // The arrow keys are the only way to move the cursor, so they must not also scroll the page.
    event.preventDefault()
    setCursor(next)
  }

  return (
    <div className="space-y-2">
      {/* The readout sits above the plot rather than following the pointer.

          A panel pinned to a band is unreachable on touch, and at ninety days it is chasing a
          three-pixel target; a fixed line is legible at every width, is where the eye already is
          after reading the title, and costs nothing to keep on screen. With nothing selected it
          carries the window's own summary, so the row is never empty space.

          `aria-live` because the arrow keys change it and nothing else announces that. */}
      <p aria-live="polite" className="min-h-9 text-sm">
        {activeDay ? (
          <>
            <T
              text={activeDay.total > 0 ? t.dayReadout : t.dayReadoutEmpty}
              values={{
                day: <span className="font-medium">{formatUtcDayLong(activeDay.day)}</span>,
                total: <span className="tabular-nums">{t.dayTotal(activeDay.total)}</span>,
                breakdown: (
                  <span className="text-muted-foreground tabular-nums">
                    {incidentPriorities
                      .filter((priority) => (activeDay.byPriority[priority] ?? 0) > 0)
                      .map((priority) =>
                        t.priorityCount(
                          activeDay.byPriority[priority] ?? 0,
                          labels.priority[priority],
                        ),
                      )
                      .join(' · ')}
                  </span>
                ),
              }}
            />
            {(activeDay.resolved ?? 0) > 0 && (
              <span className="text-muted-foreground tabular-nums">
                {t.dayResolved(activeDay.resolved)}
              </span>
            )}
          </>
        ) : (
          <span className="text-muted-foreground tabular-nums">
            {t.summary(windowTotal, count)}
            {closedTotal > 0 && t.resolvedSummary(closedTotal)}
            {busiest > 0 && t.busiest(busiest)}
            {t.hint}
          </span>
        )}
      </p>

      <div
        tabIndex={0}
        role="group"
        aria-label={t.ariaLabel(count)}
        onKeyDown={onKeyDown}
        onFocus={() => setFocused(true)}
        onBlur={(event) => {
          if (!event.currentTarget.contains(event.relatedTarget)) setFocused(false)
        }}
        className="focus-visible:ring-ring/50 rounded-md outline-none focus-visible:ring-[3px]"
      >
        <ChartCanvas
          height={chartHeight}
          margin={margin}
          label={`Incidents opened per UTC day. ${windowTotal} across ${count} days, busiest day ${busiest}.`}
          overlay={({ plot }) => {
            const band = bandScale(count, plot.width, { maxStep })

            return (
              <>
                {/* One hit area over the whole plot rather than a target per day: the slots tile
                    the track, so the pointer's x already says which band it is over. */}
                <div
                  aria-hidden
                  className="absolute"
                  style={{ left: plot.x, top: plot.y, width: plot.width, height: plot.height }}
                  onMouseMove={(event) => {
                    const box = event.currentTarget.getBoundingClientRect()

                    setHovered(band.indexAt(event.clientX - box.left))
                  }}
                  onMouseLeave={() => setHovered(null)}
                />

                {windowTotal === 0 && closedTotal === 0 && (
                  // A drawn-but-empty grid reads as a chart that failed rather than as a quiet
                  // month, and those are opposite things to learn at 3am.
                  <p
                    className="text-muted-foreground pointer-events-none absolute flex items-center justify-center px-4 text-center text-sm"
                    style={{ left: plot.x, top: plot.y, width: plot.width, height: plot.height }}
                  >
                    {t.emptyPlot}
                  </p>
                )}
              </>
            )
          }}
        >
          {({ plot }) => {
            const ticks = niceTicks(peak, Math.min(6, Math.max(2, Math.round(plot.height / 44))))
            const scale = linearScale(ticks.max, plot.height)
            const band = bandScale(count, plot.width, { maxStep })
            const picks = pickBandTicks(count, band.step, labelGap)

            return (
              <>
                <ValueAxis plot={plot} scale={scale} values={ticks.values} />

                {active !== null && band.step > 0 && (
                  <rect
                    aria-hidden
                    x={band.slot(active).x + plot.x}
                    y={plot.y}
                    width={band.slot(active).width}
                    height={plot.height}
                    className="fill-foreground/6"
                  />
                )}

                {days.map((day, index) => (
                  <DayColumn
                    key={day.day}
                    day={day}
                    x={plot.x + band.bar(index).x}
                    width={band.bar(index).width}
                    baseline={plot.y + plot.height}
                    scale={scale}
                    dimmed={active !== null && active !== index}
                  />
                ))}

                {closedTotal > 0 && band.step > 0 && (
                  <ClosedLine
                    values={closed}
                    xs={closed.map((_, index) => plot.x + band.slot(index).x + band.slot(index).width / 2)}
                    baseline={plot.y + plot.height}
                    scale={scale}
                    dots={band.step >= 8}
                  />
                )}

                <BandAxis
                  plot={plot}
                  band={band}
                  picks={picks}
                  format={(index) => formatUtcDay(days[index].day)}
                  unit="UTC"
                />
              </>
            )
          }}
        </ChartCanvas>
      </div>

      <Legend />

      {/* The numbers themselves, for a reader who is getting none of the above. The chart is one
          `role="img"` with a summary, which is the right amount for a glance and nowhere near
          enough to answer "which day was worst" — so the data is here in full rather than being
          approximated in a longer label. */}
      <table className="sr-only">
        <caption>{t.caption}</caption>
        <thead>
          <tr>
            <th scope="col">{t.columnDay}</th>
            <th scope="col">{t.columnTotal}</th>
            <th scope="col">{t.columnResolved}</th>
            {incidentPriorities.map((priority) => (
              <th key={priority} scope="col">
                {labels.priority[priority]}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {days.map((day) => (
            <tr key={day.day}>
              <th scope="row">{formatUtcDayLong(day.day)}</th>
              <td>{day.total}</td>
              <td>{day.resolved ?? 0}</td>
              {incidentPriorities.map((priority) => (
                <td key={priority}>{day.byPriority[priority] ?? 0}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

/**
 * One day's stack.
 *
 * Segment edges are rounded to whole pixels *as boundaries*, not as heights. Rounding each height
 * on its own accumulates into a stack that ends a pixel above or below the one beside it, and the
 * seams show as hairlines of card colour through the middle of a bar. Rounding the boundaries and
 * subtracting makes the segments meet exactly, whatever the arithmetic did on the way in — the same
 * trick the treemap uses when it snaps a row's last tile to the far edge.
 */
function DayColumn({
  day,
  x,
  width,
  baseline,
  scale,
  dimmed,
}: {
  day: IncidentDayBucket
  x: number
  width: number
  baseline: number
  scale: ReturnType<typeof linearScale>
  dimmed: boolean
}) {
  if (day.total <= 0) return null

  const values = incidentPriorities.map((priority) => day.byPriority[priority] ?? 0)
  const offsets = stackOffsets(values)

  // A whole-pixel bar. At ninety days a band is under three pixels wide, and a fractional edge
  // there is antialiased into a pale smear that reads as a smaller number than it is.
  const left = Math.round(x)
  const drawn = Math.max(1, Math.round(width))

  const segments = incidentPriorities
    .map((priority, index) => {
      const count = values[index]
      const bottom = Math.round(baseline - scale.span(offsets[index]))
      const top = Math.round(baseline - scale.span(offsets[index] + count))

      return { priority, count, bottom, height: Math.max(1, bottom - top) }
    })
    // A day with one incident on an axis that runs to a hundred is still a day with one incident
    // in it, so the segment is kept at a visible minimum rather than rounded away — but an empty
    // priority is genuinely absent and draws nothing.
    .filter((segment) => segment.count > 0)

  return (
    <g
      className={cn('transition-opacity duration-150 ease-out', dimmed && 'opacity-40')}
      aria-hidden
    >
      {segments.map((segment, index) => {
        // A hairline of card colour between consecutive segments, the way the signal map draws its
        // tiles: the boundary then shows whatever two fills meet at it, instead of depending on the
        // palette having put enough contrast between exactly that pair. Skipped on the topmost
        // segment, which has nothing above it, and on one too short to spare a pixel.
        const gap = index < segments.length - 1 && segment.height >= 3 ? 1 : 0

        return (
          <rect
            key={segment.priority}
            x={left}
            y={segment.bottom - segment.height + gap}
            width={drawn}
            height={segment.height - gap}
            className={priorityFill[segment.priority]}
          />
        )
      })}
    </g>
  )
}

/**
 * What closed each day, over the bars (Adım 20.8). A line rather than a second bar: the bars are
 * what arrived and are stacked by priority, and a second column per day would halve their width
 * at ninety days. Where the line runs above the bars, the team closed more than came in.
 */
function ClosedLine({
  values,
  xs,
  baseline,
  scale,
  dots,
}: {
  values: number[]
  /** The centre of each day's band, in the same order as the values. */
  xs: number[]
  baseline: number
  scale: ReturnType<typeof linearScale>
  dots: boolean
}) {
  const points = values.map((value, index) => ({
    x: xs[index],
    y: baseline - scale.span(value),
    value,
  }))

  return (
    <g aria-hidden className="pointer-events-none">
      <polyline
        points={points.map((point) => `${point.x},${point.y}`).join(' ')}
        className="stroke-foreground/80 fill-none"
        strokeWidth={1.5}
        strokeLinejoin="round"
        strokeLinecap="round"
      />
      {dots &&
        points
          .filter((point) => point.value > 0)
          .map((point) => (
            <circle
              key={point.x}
              cx={point.x}
              cy={point.y}
              r={2.5}
              className="fill-card stroke-foreground/80"
              strokeWidth={1.5}
            />
          ))}
    </g>
  )
}

/** In stacking order, bottom of the bar first, because that is the order the bars are in. */
function Legend() {
  const { dashboard, labels } = useT()

  return (
    <ul className="text-muted-foreground flex flex-wrap items-center gap-x-4 gap-y-1 text-[11px]">
      {incidentPriorities.map((priority) => (
        <li key={priority} className="flex items-center gap-1.5">
          <svg width="10" height="10" aria-hidden className="shrink-0">
            <rect width="10" height="10" rx="2" className={priorityFill[priority]} />
          </svg>
          {labels.priority[priority]}
        </li>
      ))}
      <li className="flex items-center gap-1.5">
        <svg width="14" height="10" aria-hidden className="shrink-0">
          <line
            x1="1"
            y1="5"
            x2="13"
            y2="5"
            className="stroke-foreground/80"
            strokeWidth={1.5}
            strokeLinecap="round"
          />
        </svg>
        {dashboard.chart.legendResolved}
      </li>
      <li className="text-dim-foreground">{dashboard.chart.legendNote}</li>
    </ul>
  )
}
