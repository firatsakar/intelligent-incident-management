import { useLayoutEffect, useState } from 'react'

/**
 * The size of a box, measured rather than assumed.
 *
 * Both things that draw by hand here — the signal map and the charts — lay their pixels out against
 * the width they actually got, which no amount of CSS will tell them. This was private to the map
 * until the charts needed exactly the same twenty lines; it moves up now that there is a second
 * caller, and not before.
 *
 * It lives in `lib` rather than under `components/chart` because it knows nothing about charts, and
 * a feature reaching into a component folder for a DOM hook is the wrong direction for a dependency
 * that is really just `ResizeObserver` with a first synchronous read.
 */
export function useElementSize<T extends HTMLElement>() {
  // A callback ref rather than a `useRef` object, which is not a style preference.
  //
  // With a ref object and an effect that runs once, the measurement only ever happens if the box
  // is in the tree on the hook's first commit. The signal map's box is not: while a window holds
  // no signals the card renders an empty state instead, so the observer attached to nothing — and
  // when the first signal then arrived over the socket, the box appeared with a measured size of
  // zero and the map drew no tiles at all. It stayed empty until the screen was remounted, which
  // is the exact case a pushed-first-signal is.
  //
  // React calls a callback ref whenever the node appears or goes away, so the measurement follows
  // the box instead of the hook.
  const [node, setNode] = useState<T | null>(null)
  const [size, setSize] = useState({ width: 0, height: 0 })

  useLayoutEffect(() => {
    if (!node) return

    // Read once synchronously so the first paint already has something laid out; the observer then
    // keeps up.
    setSize({ width: node.clientWidth, height: node.clientHeight })

    const observer = new ResizeObserver(([entry]) => {
      const { width, height } = entry.contentRect

      setSize((current) =>
        current.width === width && current.height === height ? current : { width, height },
      )
    })

    observer.observe(node)

    return () => observer.disconnect()
  }, [node])

  return [setNode, size] as const
}
