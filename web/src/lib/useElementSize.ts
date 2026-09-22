import { useLayoutEffect, useRef, useState } from 'react'

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
  const ref = useRef<T | null>(null)
  const [size, setSize] = useState({ width: 0, height: 0 })

  useLayoutEffect(() => {
    const node = ref.current

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
  }, [])

  return [ref, size] as const
}
