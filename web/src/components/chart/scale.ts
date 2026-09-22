// The arithmetic behind the charts: the geometry a plot is drawn into, the two scales that map data
// onto it, and the tick selection that decides what can be labelled without collapsing into a smear.
//
// Same split as the signal map's treemap module, and for the same reason: everything with a decision
// in it lives here, pure and free of React and the DOM, and the components only draw what they are
// handed. Same inputs, same numbers, every time.
//
// Three invariants the drawing side leans on:
//
//   1. Nothing here divides by zero. An empty window, a single band and a zero maximum are ordinary
//      inputs, not edge cases — a dashboard opens on all three the day the platform is installed.
//   2. Slots tile the track exactly. Adjacent bands share an edge, so a pointer anywhere over the
//      plot is over exactly one band and there is no dead strip between two of them.
//   3. A value of zero maps to the baseline and a value above zero never does, so "rare" cannot
//      render as "absent".

export interface Box {
  x: number
  y: number
  width: number
  height: number
}

export interface Margin {
  top: number
  right: number
  bottom: number
  left: number
}

export interface Span {
  x: number
  width: number
}

/**
 * The drawable area inside the margins, which is where every coordinate below is measured from.
 *
 * The margins are not decoration: they are the strips the axis labels live in. A chart with none
 * draws its own tick labels over its own bars.
 */
export function plotArea(width: number, height: number, margin: Margin): Box {
  return {
    x: margin.left,
    y: margin.top,
    width: Math.max(0, width - margin.left - margin.right),
    height: Math.max(0, height - margin.top - margin.bottom),
  }
}

/**
 * A coordinate a 1px line can sit on.
 *
 * An integer coordinate puts a hairline across the boundary between two pixels, and the browser
 * draws it as two half-lit ones — a 2px grey smudge where a 1px rule was asked for. Half a pixel
 * over lands it inside one.
 */
export function crisp(value: number): number {
  return Math.round(value) + 0.5
}

export interface LinearScale {
  /** The top of the domain. Zero is always the bottom; these charts count things. */
  max: number
  /** The pixel length the domain was mapped onto. */
  length: number
  /** How long a span of `value` units draws — a bar's height, a segment's thickness. */
  span(value: number): number
  /** Where a value sits, measured down from the top of the range, as SVG counts. */
  at(value: number): number
}

/**
 * A domain of `[0, max]` onto a length of pixels.
 *
 * Zero is always the floor. Every chart in this console counts something — incidents, signals,
 * deliveries — and a bar chart whose baseline is not zero exaggerates whatever it draws, which is
 * the one lie a bar is structurally incapable of admitting to.
 */
export function linearScale(max: number, length: number): LinearScale {
  // A domain with nothing in it has no shape to draw. Rather than dividing by it, the scale
  // collapses onto the baseline — which is exactly what an empty window should look like.
  const unit = max > 0 ? length / max : 0

  return {
    max,
    length,
    span: (value) => value * unit,
    at: (value) => length - value * unit,
  }
}

/**
 * The gap that separates one band from the next.
 *
 * A gap says "these are separate days". Below a few pixels of step it stops saying anything and
 * only steals from the bar, so it shrinks with the step and then disappears rather than eating it:
 * at ninety days on a phone the whole step is three pixels, and a 1px gutter there would be a third
 * of the data.
 */
function gapFor(step: number): number {
  if (step >= 14) return 3
  if (step >= 7) return 2
  if (step >= 4) return 1

  return 0
}

export interface BandScale {
  count: number
  /** Distance from one slot to the next. */
  step: number
  /** The drawn width of a band, once the gap is taken off. */
  width: number
  /** Where the track starts, when the bands do not fill the extent. */
  inset: number
  /** The whole slot a band owns: the hit target, and the reason there is no dead ground. */
  slot(index: number): Span
  /** The band as drawn, centred in its slot. */
  bar(index: number): Span
  /** The middle of a slot — where a tick label or a point mark belongs. */
  center(index: number): number
  /** The band an offset falls in, or null outside the track. */
  indexAt(offset: number): number | null
}

/**
 * `count` equal bands across `extent` pixels.
 *
 * `maxStep` caps how wide a band may get and is what a short window needs: seven days stretched
 * across nine hundred pixels is seven slabs, which reads as a different kind of chart entirely. Once
 * the cap bites, the track is narrower than the box and is **centred** in it. The x axis here is
 * categorical — the bands are the whole domain, not a sample of a continuum — so centring the domain
 * is honest, where pinning it left would leave an empty stretch that looks like missing days.
 */
export function bandScale(
  count: number,
  extent: number,
  options: { maxStep?: number } = {},
): BandScale {
  const usable = Math.max(0, extent)
  const step = count > 0 ? Math.min(usable / count, options.maxStep ?? Infinity) : 0
  // At least a pixel: a band that exists must be visible, whatever the arithmetic says.
  const width = step > 0 ? Math.max(1, step - gapFor(step)) : 0
  const inset = (usable - step * count) / 2

  return {
    count,
    step,
    width,
    inset,
    slot: (index) => ({ x: inset + index * step, width: step }),
    bar: (index) => ({ x: inset + index * step + (step - width) / 2, width }),
    center: (index) => inset + index * step + step / 2,
    indexAt: (offset) => {
      if (step <= 0) return null

      const index = Math.floor((offset - inset) / step)

      return index >= 0 && index < count ? index : null
    },
  }
}

export interface ValueTicks {
  /** The axis top, rounded up to land on a tick. Feed this to `linearScale`, not the raw maximum. */
  max: number
  values: number[]
}

/**
 * Round numbers covering `[0, max]`, about `target` of them.
 *
 * Steps of 1, 2 or 5 times a power of ten, because those are the ones a reader adds up without
 * thinking. `minStep` defaults to 1: these charts count things, and half an incident is not a
 * quantity — pass a smaller one for an axis in seconds.
 */
export function niceTicks(max: number, target: number, minStep = 1): ValueTicks {
  // An axis with nothing on it still needs a shape. 0..1 draws the frame and lets the plot say the
  // window is empty; a 0..0 axis collapses onto one line and reads as a chart that failed to load.
  if (!Number.isFinite(max) || max <= 0) return { max: minStep, values: [0, minStep] }

  const count = Math.max(1, Math.round(target))
  const raw = max / count
  const magnitude = 10 ** Math.floor(Math.log10(raw))
  const normalised = raw / magnitude
  const rounded = (normalised > 5 ? 10 : normalised > 2 ? 5 : normalised > 1 ? 2 : 1) * magnitude
  const step = Math.max(minStep, rounded)
  const top = Math.ceil(max / step) * step

  const values: number[] = []

  // Multiplied rather than accumulated: adding 0.1 eleven times does not arrive at 1.1, and a tick
  // labelled "0.30000000000000004" is a hard thing to explain.
  for (let index = 0; index * step <= top; index += 1) values.push(index * step)

  return { max: top, values }
}

/**
 * Which bands can carry a label without running into their neighbours, counted back from the last.
 *
 * Counting back is the whole point. On a time axis the newest band is the reference an operator
 * reads everything else against, so it is the one that must always be labelled — and taking a
 * uniform stride backwards from it means no two labels can ever collide, rather than picking evenly
 * from the front and hoping the last one lands somewhere legible.
 *
 * `minGap` is the width a label needs, which the caller knows and this file does not.
 */
export function pickBandTicks(count: number, step: number, minGap: number): number[] {
  if (count <= 0 || step <= 0) return []

  const stride = Math.max(1, Math.ceil(minGap / step))
  const picked: number[] = []

  for (let index = count - 1; index >= 0; index -= stride) picked.push(index)

  return picked.reverse()
}

/**
 * The running total in front of each value, which is where its segment starts in a stack.
 *
 * Returned rather than accumulated at the draw site so the drawing stays declarative: a segment
 * asks for its own base instead of every segment depending on the one before it having been drawn.
 */
export function stackOffsets(values: readonly number[]): number[] {
  const offsets: number[] = []

  let running = 0

  for (const value of values) {
    offsets.push(running)
    running += value
  }

  return offsets
}
