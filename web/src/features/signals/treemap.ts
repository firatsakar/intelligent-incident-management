// The layout behind the signal map: a squarified treemap, written by hand rather than pulling in a
// charting library for sixty lines of arithmetic.
//
// The point of "squarified" is the aspect ratio. Slicing the rectangle in one direction — the naive
// treemap — is four lines and produces slivers: a tile 400px wide and 3px tall carries its area
// honestly and is still unreadable and unclickable. Bruls/Huizing/van Wijk fill the rectangle row by
// row along its *shorter* side, and only keep adding to the current row while doing so makes that
// row's worst tile less elongated.
//
// Everything here is pure — same items and bounds in, same rects out — and there is no frontend test
// runner in this project yet, so the two invariants everything downstream leans on are established
// at the lines that make them true:
//
//   1. The returned tiles cover `bounds` exactly: no overlap, no seam. Rows and rows-within-rows
//      snap their last member to the remaining edge instead of trusting accumulated division.
//   2. Tiles come back largest first, which is also the DOM order and therefore the tab order.

export interface Rect {
  x: number
  y: number
  width: number
  height: number
}

export interface TreemapInput<T> {
  /** The magnitude the area encodes. Items at or below zero are dropped — a zero-area tile cannot
   *  be seen, hovered or clicked, so drawing one would be a lie about what is reachable. */
  value: number
  data: T
}

export interface TreemapTile<T> {
  data: T
  value: number
  rect: Rect
}

/**
 * The worst aspect ratio in a row of total area `sum` laid across a side of length `side`.
 *
 * Thickness is `sum / side`, so a tile of area `a` is `a / thickness` long and its ratio is
 * `max(thickness / length, length / thickness)`. Both extremes are driven by the row's smallest and
 * largest member alone, which is why this needs four numbers rather than the row itself — and why
 * growing a row stays O(n) overall instead of rescanning at every step.
 */
function worstAspect(min: number, max: number, sum: number, side: number): number {
  const sum2 = sum * sum
  const side2 = side * side

  return Math.max(sum2 / (side2 * min), (side2 * max) / sum2)
}

export function squarify<T>(items: readonly TreemapInput<T>[], bounds: Rect): TreemapTile<T>[] {
  const ranked = items.filter((item) => item.value > 0).sort((a, b) => b.value - a.value)

  if (ranked.length === 0 || bounds.width <= 0 || bounds.height <= 0) return []

  // Convert value to px² once. After this line every quantity in the function is an area, so a
  // row's thickness is simply its area divided by its length.
  const total = ranked.reduce((sum, item) => sum + item.value, 0)
  const scale = (bounds.width * bounds.height) / total
  const areas = ranked.map((item) => item.value * scale)

  const tiles: TreemapTile<T>[] = []

  let free: Rect = { ...bounds }
  let index = 0

  while (index < ranked.length) {
    const side = Math.min(free.width, free.height)

    // Nothing left to draw into. Also the loop's safety catch: `free` only shrinks by a positive
    // thickness, so without this a degenerate rect could spin here forever.
    if (side <= 0) break

    let end = index + 1
    let sum = areas[index]
    let min = areas[index]
    let max = areas[index]

    while (end < ranked.length) {
      const area = areas[end]
      const nextSum = sum + area
      const nextMin = Math.min(min, area)
      const nextMax = Math.max(max, area)

      // Stop at the first item that makes the row *worse*. The areas are sorted descending, so the
      // row can only get more lopsided from here — this is the whole heuristic.
      if (worstAspect(nextMin, nextMax, nextSum, side) > worstAspect(min, max, sum, side)) break

      sum = nextSum
      min = nextMin
      max = nextMax
      end += 1
    }

    // Rows run across the shorter side; that is what keeps tiles compact rather than slivered.
    const horizontal = free.width <= free.height
    const remaining = horizontal ? free.height : free.width
    const isLastRow = end >= ranked.length

    // The last row takes whatever is left rather than its computed thickness. Float error
    // accumulates down the rows above it, and a 0.4px strip of empty ground along the bottom of the
    // map reads as a rendering bug rather than as data.
    const thickness = isLastRow ? remaining : Math.min(sum / side, remaining)

    if (thickness <= 0) break

    const rowEnd = horizontal ? free.x + free.width : free.y + free.height
    let offset = horizontal ? free.x : free.y

    for (let i = index; i < end; i += 1) {
      // Same reasoning one axis down: the last tile in a row is snapped to the row's far edge.
      const extent = i === end - 1 ? Math.max(0, rowEnd - offset) : areas[i] / thickness

      tiles.push({
        data: ranked[i].data,
        value: ranked[i].value,
        rect: horizontal
          ? { x: offset, y: free.y, width: extent, height: thickness }
          : { x: free.x, y: offset, width: thickness, height: extent },
      })

      offset += extent
    }

    free = horizontal
      ? {
          x: free.x,
          y: free.y + thickness,
          width: free.width,
          height: Math.max(0, free.height - thickness),
        }
      : {
          x: free.x + thickness,
          y: free.y,
          width: Math.max(0, free.width - thickness),
          height: free.height,
        }

    index = end
  }

  return tiles
}

export interface TreemapGroup<T> {
  key: string
  items: readonly TreemapInput<T>[]
}

export interface TreemapGroupLayout<T> {
  key: string
  rect: Rect
  /** The strip reserved for the group's label, or null when the region cannot spare one. */
  header: Rect | null
  tiles: TreemapTile<T>[]
}

/**
 * Two levels of the same map: groups are squarified against the whole area, then each group's items
 * are squarified against what is left of its region under the header.
 *
 * A group's magnitude is the sum of its items', so the outer and inner maps agree — "biggest group
 * top-left" and "biggest item within it top-left" become one continuous sweep rather than two
 * orderings the eye has to reconcile.
 */
export function layoutGroups<T>(
  groups: readonly TreemapGroup<T>[],
  bounds: Rect,
  headerHeight: number,
): TreemapGroupLayout<T>[] {
  const outer = squarify(
    groups.map((group) => ({
      value: group.items.reduce((sum, item) => sum + Math.max(0, item.value), 0),
      data: group,
    })),
    bounds,
  )

  return outer.map(({ data: group, rect }) => {
    // The header is dropped, never shrunk, when it would eat the region: a label over two tiles too
    // small to read is worse than the two tiles, and the group name is still in the hover panel.
    const header =
      headerHeight > 0 && rect.height >= headerHeight * 3
        ? { x: rect.x, y: rect.y, width: rect.width, height: headerHeight }
        : null

    const inner = header
      ? {
          x: rect.x,
          y: rect.y + header.height,
          width: rect.width,
          height: rect.height - header.height,
        }
      : rect

    return { key: group.key, rect, header, tiles: squarify(group.items, inner) }
  })
}
