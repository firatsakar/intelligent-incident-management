import { useEffect, useRef, useState } from 'react'

import type { HeatMap } from './heatmap'
import { cellKey } from './heatmap'

/**
 * Which tiles have just been written to, and whether the map is moving fast enough that showing
 * that with motion would be a strobe.
 *
 * The map is already live: a pushed signal lands in the cached list, the list rebuilds the map,
 * and the treemap re-lays-out — all with no request. What was missing is *seeing which tile
 * moved*, which on a map that re-sorts itself in the same frame is not something the eye can do
 * unaided.
 *
 * Derived by diffing the map against the last one seen rather than by listening to the socket.
 * Three reasons, and the third is the one that decided it:
 *
 *  1. It is ephemeral UI state, which belongs in the component rather than in a store this app
 *     does not have.
 *  2. It catches every cause of a change — a push, a reconnect refetch, a signal ageing out of
 *     the window — instead of only the one message it was wired to.
 *  3. It is keyed by cell, not by DOM node. The treemap recomputes on every push, so a tile can
 *     change size and position in the same frame it is marked; a mark attached to the cell
 *     follows it wherever the layout puts it.
 */
export interface HeatChanges {
  /**
   * Cell key to a number that changes every time that cell is marked afresh.
   *
   * The number is the element key for the flash overlay, which is how the animation restarts on a
   * second hit: an element that stays mounted keeps running the animation it started with, and a
   * tile hit twice in a row would flash once.
   */
  marks: ReadonlyMap<string, number>
  /** True once the map has been changing without a pause for long enough that the flash should
   *  stop moving and simply stand still. */
  storm: boolean
}

/**
 * The damping, which is the part of this that is a decision rather than bookkeeping.
 *
 * A storm is exactly when this fires most, and an animation an operator cannot dismiss is the
 * worst kind — the connection indicator was deliberately de-animated in Adım 19.5 for the same
 * reason. So there are two limits rather than one:
 *
 *   `coalesceMs`  at most one flash per tile per this long. Thirty signals in a second produce
 *                 one flash, not thirty, and the tile that moved is still the tile that lit up.
 *   `burstLimit`  after this many consecutive flashes with no quiet gap between them, the map
 *                 stops animating entirely and the marks simply stand there. Roughly two and a
 *                 half seconds of continuous change, which is long enough for a normal handful of
 *                 arrivals to flash and short enough that a real storm settles before it becomes
 *                 wallpaper.
 *   `quietMs`     no change at all for this long re-arms the animation. Trailing rather than
 *                 counted, so the map comes back to life on its own once the storm passes.
 *   `markMs`      how long a tile keeps its mark. Outlives the fade on purpose: while the map is
 *                 held static this is the whole signal, and under reduced motion it is the only
 *                 one.
 */
const coalesceMs = 600
const burstLimit = 4
const quietMs = 2500
const markMs = 4000

/**
 * @param scope What window the map is drawn over. When it changes the map is looking at different
 *   data, so the next diff re-baselines instead of flashing every tile at once.
 */
export function useHeatChanges(map: HeatMap, scope: string): HeatChanges {
  const [marks, setMarks] = useState<ReadonlyMap<string, number>>(() => new Map())
  const [storm, setStorm] = useState(false)

  // Everything below is a ref because the effect runs on every render — `map` is rebuilt from the
  // cached list each time — and a coalescing window that restarted whenever the page happened to
  // re-render would not coalesce anything.
  const seen = useRef<Map<string, number> | null>(null)
  const seenScope = useRef(scope)
  const waiting = useRef(new Set<string>())
  const flushTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const quietTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const generation = useRef(0)
  const flashes = useRef(0)
  const storming = useRef(false)

  useEffect(() => {
    const current = new Map<string, number>()

    for (const cell of map.cells.values()) {
      current.set(cellKey(cell.service, cell.errorKey), cell.occurrences)
    }

    const previous = seen.current
    const rebaselined = previous === null || seenScope.current !== scope

    seen.current = current
    seenScope.current = scope

    if (rebaselined) return

    const changed: string[] = []

    for (const [key, occurrences] of current) {
      const before = previous.get(key)

      // A cell that has appeared counts as much as one that grew: a signature firing for the
      // first time in this window is the arrival most worth pointing at.
      if (before === undefined || occurrences > before) changed.push(key)
    }

    if (changed.length === 0) return

    for (const key of changed) waiting.current.add(key)

    if (quietTimer.current) clearTimeout(quietTimer.current)

    quietTimer.current = setTimeout(() => {
      quietTimer.current = null
      flashes.current = 0

      if (storming.current) {
        storming.current = false
        setStorm(false)
      }
    }, quietMs)

    if (flushTimer.current) return

    flushTimer.current = setTimeout(() => {
      flushTimer.current = null

      const batch = [...waiting.current]
      waiting.current.clear()

      if (batch.length === 0) return

      flashes.current += 1

      if (flashes.current > burstLimit && !storming.current) {
        storming.current = true
        setStorm(true)
      }

      generation.current += 1

      const id = generation.current

      setMarks((live) => {
        const next = new Map(live)

        for (const key of batch) next.set(key, id)

        return next
      })

      setTimeout(() => {
        setMarks((live) => {
          const next = new Map(live)

          // Only the marks this batch put there. A tile hit again since has a newer generation
          // and its own timer, and clearing it here would cut that one short.
          for (const key of batch) {
            if (next.get(key) === id) next.delete(key)
          }

          return next.size === live.size ? live : next
        })
      }, markMs)
    }, coalesceMs)
  }, [map, scope])

  // Only on unmount. Clearing these per effect run would reset the coalescing window on every
  // render, which is the one thing this hook exists to avoid.
  useEffect(
    () => () => {
      if (flushTimer.current) clearTimeout(flushTimer.current)
      if (quietTimer.current) clearTimeout(quietTimer.current)
    },
    [],
  )

  return { marks, storm }
}
