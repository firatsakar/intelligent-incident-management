import { cn } from '@/lib/utils'

/**
 * A stacked proportion bar.
 *
 * Flex rather than measured percentages: the segments are a ratio, and `flex-grow` is a ratio the
 * browser already knows how to resolve at any width. A minimum width so a segment holding one of
 * forty is a sliver rather than nothing — the whole point of a proportion bar is that the small
 * parts are still on it.
 *
 * `aria-hidden`, always. It is a picture of numbers that are printed beside it, and a screen reader
 * reading out a row of empty divs has been told nothing. Every caller carries a legend in words.
 */
export function ProportionBar({
  segments,
}: {
  segments: { key: string; value: number; className: string }[]
}) {
  const total = segments.reduce((sum, segment) => sum + segment.value, 0)

  if (total <= 0) return <div className="bg-muted h-2 w-full rounded-full" />

  return (
    <div aria-hidden className="bg-muted flex h-2 w-full gap-px overflow-hidden rounded-full">
      {segments
        .filter((segment) => segment.value > 0)
        .map((segment) => (
          <div
            key={segment.key}
            className={cn('h-full', segment.className)}
            style={{ flexGrow: segment.value, flexBasis: 0, minWidth: 3 }}
          />
        ))}
    </div>
  )
}
