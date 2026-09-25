import type { ReactNode } from 'react'

import { useElementSize } from '@/lib/useElementSize'
import { cn } from '@/lib/utils'

import { plotArea, type Box, type Margin } from './scale'

export interface ChartGeometry {
  /** The measured outer box. Width is whatever the card gave it; height is fixed by the caller. */
  width: number
  height: number
  /** Where the data goes: inside the margins, with the axis labels outside it. */
  plot: Box
}

/**
 * A responsive SVG surface with its plot geometry worked out.
 *
 * Width is measured, not declared — a chart drawn at a width CSS has not agreed to yet lays its
 * bars out against the wrong number and then never corrects itself. Height is a prop, because
 * nothing here knows how much vertical room a card is willing to spend.
 *
 * Children are a function rather than elements: they need `plot`, and passing geometry down through
 * props would mean every caller measuring the same box a second time. Nothing is called until the
 * box has a width, so no child ever sees a zero-width plot.
 *
 * `overlay` is the HTML half. Some of a chart genuinely is not SVG — a hit area that has to catch a
 * pointer anywhere over the plot, an empty-state sentence that should wrap and truncate the way
 * text does — and drawing those in SVG means reimplementing layout. They share the same coordinate
 * space, because they are positioned from the same geometry.
 */
export function ChartCanvas({
  height,
  margin,
  label,
  className,
  children,
  overlay,
}: {
  height: number
  margin: Margin
  /** What the picture says, for someone who will never see it. */
  label: string
  className?: string
  children: (geometry: ChartGeometry) => ReactNode
  overlay?: (geometry: ChartGeometry) => ReactNode
}) {
  const [ref, size] = useElementSize<HTMLDivElement>()

  const ready = size.width > 0
  const geometry: ChartGeometry = {
    width: size.width,
    height,
    plot: plotArea(size.width, height, margin),
  }

  return (
    <div ref={ref} className={cn('relative w-full', className)} style={{ height }}>
      {/* The viewBox matches the measured box one-to-one, so a pixel in the scales is a pixel on
          screen and nothing is scaled behind the arithmetic's back. */}
      <svg
        width="100%"
        height={height}
        viewBox={`0 0 ${Math.max(1, size.width)} ${height}`}
        role="img"
        aria-label={label}
        className="block"
      >
        {ready && children(geometry)}
      </svg>

      {ready && overlay?.(geometry)}
    </div>
  )
}
