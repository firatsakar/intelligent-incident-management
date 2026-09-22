import { ChevronRight } from 'lucide-react'
import { Fragment, type CSSProperties, type ReactNode } from 'react'
import { Link } from 'react-router-dom'

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useElementSize } from '@/lib/useElementSize'
import { cn } from '@/lib/utils'
import type { SignalStatus } from '@/types/api'

import { cellKey, intensityBounds, intensityOf, otherKey, type HeatCell, type HeatMap } from './heatmap'
import { layoutGroups, type Rect } from './treemap'
import { useHeatChanges } from './useHeatChanges'

// A market map, borrowed from the finance screens that do this better than any dashboard: tiles
// tile the whole area, area is the magnitude, colour is the same magnitude on a ramp, and the eye
// sweeps from the biggest tile top-left down to the tail. Nothing is spent on gutters.
//
// Three channels, deliberately independent:
//
//   area + colour   how many times it fired
//   corner mark     how far the detection gate took it
//   edge            it was written to just now
//
// Colour alone cannot carry the band — the ramp is a quantity now, and a quantity and a verdict on
// one channel is how a map stops answering "where did the noise turn into an incident". The mark is
// a shape rather than a second hue for the same reason, and because one operator in twelve cannot
// separate the hues anyway. Recency is the third, and it is an edge rather than a fill for exactly
// the same argument: the fill is already spoken for.

/** Indexed by `intensityOf`, which returns 0 only for an empty cell. */
const heatFill = [
  'bg-heat-empty',
  'bg-heat-1',
  'bg-heat-2',
  'bg-heat-3',
  'bg-heat-4',
  'bg-heat-5',
]

const bandTitle: Record<SignalStatus, string> = {
  Promoted: 'opened an incident',
  Deduplicated: 'counted into an open incident',
  Weak: 'weak — shown, not raised',
  Recorded: 'recorded only',
  Suppressed: 'suppressed (muted signature)',
}

// Four shapes and one absence. Recorded is the calm baseline — most cells, most of the time — and
// giving it a mark would put a glyph on the whole map to say "nothing happened". Suppressed does
// get one: the gate silenced it on purpose, and that is a decision worth seeing.
const bandShape: Record<SignalStatus, ReactNode> = {
  Promoted: <path d="M5 0.6 9.6 9H0.4Z" fill="currentColor" />,
  Deduplicated: (
    <path d="M1 2.7h8M1 7.3h8" stroke="currentColor" strokeWidth="2.1" strokeLinecap="butt" />
  ),
  Weak: <circle cx="5" cy="5" r="3.3" fill="none" stroke="currentColor" strokeWidth="1.9" />,
  Suppressed: (
    <path d="M1.6 8.4 8.4 1.6" stroke="currentColor" strokeWidth="2.1" strokeLinecap="round" />
  ),
  Recorded: null,
}

const bandLegend: SignalStatus[] = ['Promoted', 'Deduplicated', 'Weak', 'Suppressed']

function BandMark({ band, size }: { band: SignalStatus; size: number }) {
  const shape = bandShape[band]

  if (!shape) return null

  return (
    <svg viewBox="0 0 10 10" width={size} height={size} aria-hidden className="shrink-0">
      {shape}
    </svg>
  )
}

/** Height of a group's label strip. Two lines of nothing would cost more than the name is worth. */
const groupHeader = 22

type TileDetail = 'full' | 'compact' | 'count' | 'bare'

/**
 * What survives as a tile shrinks. The market maps this is modelled on drop the logo first, then
 * shrink the type, and keep a ticker and a number down to the smallest readable tile — the rule
 * being that a tile never shows half a word. Same order here, except the band mark outlives the
 * text: it is the second channel, and the text is recoverable from the panel and the label.
 *
 * A signature worth 1% of the window gets 1% of the area, which on a wide map can be a strip a few
 * pixels thick — under the 24px hit target the rest of this console holds itself to. That is
 * deliberate and it is not fixable from here: inflating the small tiles would make area stop
 * meaning magnitude, which is the one thing a treemap cannot lie about. Every tile is instead a
 * real button in descending order, so the tail is reachable by keyboard whatever size it drew at,
 * it carries its whole description in `title` and `aria-label`, and the signal list below the map
 * is the same data at full size.
 */
function detailFor(rect: Rect): TileDetail {
  if (rect.width >= 132 && rect.height >= 64) return 'full'
  if (rect.width >= 84 && rect.height >= 40) return 'compact'
  if (rect.width >= 38 && rect.height >= 22) return 'count'

  return 'bare'
}

function panelFor(rect: Rect): 'full' | 'compact' | 'none' {
  if (rect.width >= 136 && rect.height >= 52) return 'full'
  if (rect.width >= 48 && rect.height >= 26) return 'compact'

  return 'none'
}

/**
 * Percentages, not the pixels the layout ran in. The map re-measures on resize and the two happen a
 * frame apart; in percentages the tiles still meet exactly in between instead of opening a seam.
 */
function toStyle(rect: Rect, box: { width: number; height: number }): CSSProperties {
  return {
    left: `${(rect.x / box.width) * 100}%`,
    top: `${(rect.y / box.height) * 100}%`,
    width: `${(rect.width / box.width) * 100}%`,
    height: `${(rect.height / box.height) * 100}%`,
  }
}

/** What the map was built from, when that is less than the window holds. */
export interface HeatCoverage {
  loaded: number
  total: number
}

export function SignalHeatMap({
  map,
  scope,
  coverage,
  selected,
  onSelect,
}: {
  map: HeatMap
  /** The window the map is drawn over. Only used to tell "the data moved" from "the question
   *  changed", so that switching window does not light up every tile at once. */
  scope: string
  coverage?: HeatCoverage
  selected: { service: string; errorKey: string } | null
  onSelect: (cell: { service: string; errorKey: string } | null) => void
}) {
  const [ref, size] = useElementSize<HTMLDivElement>()
  const changes = useHeatChanges(map, scope)

  const cells = [...map.cells.values()]

  /** The coverage, but only when it is worth saying — a map built from everything says nothing. */
  const partial = coverage && coverage.loaded < coverage.total ? coverage : null

  const byService = new Map<string, HeatCell[]>()

  for (const cell of cells) {
    const bucket = byService.get(cell.service)

    if (bucket) bucket.push(cell)
    else byService.set(cell.service, [cell])
  }

  // Recomputed on every render from `map`, which is itself rebuilt from the cached signal list.
  // That is the entire freshness story for this screen: a pushed signal changes the list, the map
  // and this layout, with no request and no timer. Laying out twenty tiles costs less than the
  // bookkeeping that would avoid it.
  const groups =
    size.width > 0 && size.height > 0
      ? layoutGroups(
          [...byService.entries()].map(([service, group]) => ({
            key: service,
            items: group.map((cell) => ({ value: cell.occurrences, data: cell })),
          })),
          { x: 0, y: 0, width: size.width, height: size.height },
          groupHeader,
        )
      : []

  const occurrences = cells.reduce((sum, cell) => sum + cell.occurrences, 0)

  return (
    <Card>
      <CardHeader>
        <CardTitle>Where the errors are</CardTitle>
        <CardDescription>
          Every tile is one error signature. Size and colour are both how often it fired — biggest
          and reddest top-left — the corner mark is how far the gate took it, and a tile outlines
          itself when a signal for it has just arrived.{' '}
          {partial && (
            // Said on the map rather than only on the list below it. A treemap that claims to
            // show where the errors are while covering a third of the window is the kind of quiet
            // lie this screen exists not to tell.
            <span className="text-foreground tabular-nums">
              Built from the {partial.loaded} most recent of {partial.total} signals in this
              window — Load more below widens it.
            </span>
          )}
        </CardDescription>
      </CardHeader>

      <CardContent>
        {map.services.length === 0 ? (
          // A freshly installed system has no signals, and a blank area reads as broken rather
          // than as empty.
          <p className="text-muted-foreground py-10 text-center text-sm">
            No signals in this window. Nothing has crossed a detection rule yet.
          </p>
        ) : (
          <>
            <div className="text-muted-foreground mb-2 flex flex-wrap items-center gap-x-1 gap-y-1 text-xs">
              {selected ? (
                <button
                  type="button"
                  onClick={() => onSelect(null)}
                  className="hover:text-foreground -mx-1 inline-flex min-h-6 items-center rounded-sm px-1 hover:underline"
                >
                  All services
                </button>
              ) : (
                <span className="text-foreground inline-flex min-h-6 items-center font-medium">
                  All services
                </span>
              )}

              {selected && (
                <>
                  <ChevronRight className="size-3 shrink-0" aria-hidden />
                  <span className="text-foreground max-w-40 truncate font-medium">
                    {selected.service}
                  </span>
                  <ChevronRight className="size-3 shrink-0" aria-hidden />
                  <span className="text-foreground max-w-56 truncate font-medium">
                    {selected.errorKey === otherKey ? 'other signatures' : selected.errorKey}
                  </span>
                </>
              )}

              <span className="ml-auto tabular-nums">
                {map.services.length} service(s) · {cells.length} tile(s) · {occurrences}{' '}
                occurrence(s)
              </span>
            </div>

            <div
              ref={ref}
              className="bg-heat-empty ring-foreground/10 relative aspect-4/3 w-full overflow-hidden rounded-md ring-1 sm:aspect-16/7 sm:max-h-[440px]"
            >
              {groups.map((group) => (
                <Fragment key={group.key}>
                  {group.header && (
                    <div
                      className="bg-card text-muted-foreground absolute flex items-center gap-2 overflow-hidden px-2 text-[11px] leading-none font-medium"
                      style={toStyle(group.header, size)}
                    >
                      <span className="truncate" title={group.key}>
                        {group.key}
                      </span>
                      <span className="text-dim-foreground shrink-0 tabular-nums">
                        {group.tiles.reduce((sum, tile) => sum + tile.value, 0)}
                      </span>
                    </div>
                  )}

                  {group.tiles.map((tile) => (
                    <Tile
                      key={tile.data.errorKey}
                      cell={tile.data}
                      rect={tile.rect}
                      style={toStyle(tile.rect, size)}
                      max={map.max}
                      showService={group.header === null}
                      // Looked up by cell key, which is the whole reason this survives a
                      // re-layout: the tile below may be a different size and in a different
                      // corner than it was a frame ago, and it is still the same cell.
                      flash={changes.marks.get(cellKey(tile.data.service, tile.data.errorKey))}
                      stormy={changes.storm}
                      selected={
                        selected?.service === tile.data.service &&
                        selected?.errorKey === tile.data.errorKey
                      }
                      onSelect={onSelect}
                    />
                  ))}
                </Fragment>
              ))}
            </div>

            <Legend map={map} />
          </>
        )}
      </CardContent>
    </Card>
  )
}

function Tile({
  cell,
  rect,
  style,
  max,
  showService,
  flash,
  stormy,
  selected,
  onSelect,
}: {
  cell: HeatCell
  rect: Rect
  style: CSSProperties
  max: number
  showService: boolean
  /** Undefined when the cell has not changed lately; otherwise a number that changes on every
   *  fresh hit, which is what remounts the overlay and restarts its animation. */
  flash: number | undefined
  stormy: boolean
  selected: boolean
  onSelect: (cell: { service: string; errorKey: string } | null) => void
}) {
  const label = cell.errorKey === otherKey ? 'other signatures' : cell.errorKey
  const detail = detailFor(rect)
  const panel = panelFor(rect)

  // Colour is never the only channel, and neither is an animation. A mark that can only be seen
  // by watching is a mark an operator who looked away has missed.
  const description = `${cell.service} · ${label} — ${cell.occurrences} occurrence(s) across ${cell.signalCount} signal(s) — ${bandTitle[cell.band]}${
    flash === undefined ? '' : ' — updated just now'
  }`

  const markSize = detail === 'full' ? 10 : detail === 'compact' ? 9 : 7
  const showMark = rect.width >= 16 && rect.height >= 14

  return (
    <div
      className={cn(
        // Hairlines, not gutters: every tile draws its own right and bottom edge in the page
        // colour, so abutting tiles share exactly one line and no area is spent on gaps.
        'group absolute overflow-hidden shadow-[inset_-1px_-1px_0_0_var(--background)]',
        selected && 'outline-heat-foreground z-10 outline-2 -outline-offset-2',
      )}
      style={style}
    >
      <button
        type="button"
        aria-pressed={selected}
        aria-label={description}
        // Only where there is no panel to slide up; anywhere else this would be a second tooltip
        // fighting the first.
        title={panel === 'none' ? description : undefined}
        onClick={() =>
          onSelect(selected ? null : { service: cell.service, errorKey: cell.errorKey })
        }
        className={cn(
          'text-heat-foreground absolute inset-0 flex flex-col items-center justify-center gap-0.5 px-1 text-center leading-tight',
          // Two tones, because the fill under it runs from pale yellow to deep red and no single
          // ring colour clears 3:1 against both ends.
          'focus-visible:outline-none focus-visible:shadow-[inset_0_0_0_2px_var(--background),inset_0_0_0_4px_var(--foreground)]',
          heatFill[intensityOf(cell.occurrences, max)],
        )}
      >
        {showMark && (
          <span className="absolute top-1 right-1">
            <BandMark band={cell.band} size={markSize} />
          </span>
        )}

        {detail === 'full' && (
          <>
            <span className="max-w-full truncate text-[13px] font-semibold">{label}</span>
            <span className="text-[12px] tabular-nums">{cell.occurrences}</span>
          </>
        )}

        {detail === 'compact' && (
          <>
            <span className="max-w-full truncate text-[11px] font-semibold">{label}</span>
            <span className="text-[11px] tabular-nums">{cell.occurrences}</span>
          </>
        )}

        {/* Below this the name would only ever be an ellipsis, so it goes rather than arriving
            clipped. The number is the last thing to leave. */}
        {detail === 'count' && (
          <span className="text-[11px] font-medium tabular-nums">{cell.occurrences}</span>
        )}
      </button>

      {/* After the button so it draws over the fill, before the panel so a hovered tile's readout
          is never behind it. Keyed on the mark rather than on the cell: remounting is what makes a
          second hit flash a second time, and this element is decorative, so remounting it costs
          nothing an operator can notice. */}
      {flash !== undefined && (
        <span
          key={flash}
          aria-hidden
          className={cn('heat-flash', stormy && 'heat-flash-static')}
        />
      )}

      {panel !== 'none' && (
        <div
          className={cn(
            // Slides up from the tile's own bottom edge and covers part of it. Transform only, and
            // the content is in the DOM the whole time — under reduced motion the global rule in
            // index.css collapses the transition, so it appears instantly rather than never.
            'bg-card text-card-foreground pointer-events-none absolute inset-x-0 bottom-0 translate-y-full border-t px-2 py-1.5 transition-transform duration-150 ease-out',
            'group-hover:translate-y-0 group-focus-within:translate-y-0',
          )}
        >
          {panel === 'full' ? (
            <>
              <div className="flex items-baseline gap-1.5">
                <span className="text-sm font-semibold tabular-nums">{cell.occurrences}</span>
                <span className="text-muted-foreground truncate text-[11px]">
                  occurrence(s) · {cell.signalCount} signal(s)
                </span>
              </div>

              <div className="mt-0.5 flex items-center gap-1.5">
                <span className="text-muted-foreground flex min-w-0 items-center gap-1 text-[11px]">
                  <BandMark band={cell.band} size={9} />
                  <span className="truncate">{bandTitle[cell.band]}</span>
                </span>

                {cell.incidentId && (
                  <Link
                    to={`/incidents/${cell.incidentId}`}
                    className="text-primary pointer-events-auto ml-auto shrink-0 text-[11px] font-medium hover:underline"
                  >
                    Incident →
                  </Link>
                )}
              </div>

              {showService && (
                <div className="text-dim-foreground mt-0.5 truncate text-[11px]">
                  {cell.service}
                </div>
              )}
            </>
          ) : (
            <div className="flex items-center gap-1.5 text-[11px]">
              <span className="font-semibold tabular-nums">{cell.occurrences}</span>
              <span className="text-muted-foreground">
                <BandMark band={cell.band} size={9} />
              </span>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

function Legend({ map }: { map: HeatMap }) {
  const bounds = intensityBounds(map.max)

  return (
    <div className="text-muted-foreground mt-3 flex flex-wrap items-center gap-x-5 gap-y-3 text-[11px]">
      {bounds.length > 0 && (
        <div className="flex items-end gap-2">
          <span className="pb-3.5">occurrences</span>

          <div className="flex items-end gap-px">
            {bounds.map((bound, index) => {
              // A step whose bound repeats the one before it holds no count at all — over a range
              // of three occurrences most of the ramp is empty, and a dot says so where five
              // numbers would suggest otherwise.
              const distinct = bound > 0 && bound !== bounds[index - 1]

              return (
                <div key={index} className="flex flex-col items-center gap-1">
                  <span className={cn('h-3 w-7 rounded-[2px]', heatFill[index + 1])} />
                  <span className="tabular-nums" aria-hidden={!distinct}>
                    {distinct ? bound : '·'}
                  </span>
                </div>
              )
            })}
          </div>
        </div>
      )}

      <div className="flex flex-wrap items-center gap-x-4 gap-y-1.5">
        {bandLegend.map((band) => (
          <span key={band} className="flex items-center gap-1.5">
            <BandMark band={band} size={10} />
            {bandTitle[band]}
          </span>
        ))}
        <span className="text-dim-foreground">no mark — recorded only</span>

        {/* The third channel gets a legend entry like the other two. A swatch rather than a live
            example: the legend is read when nothing is happening, which is precisely when the
            thing it explains is not on screen. */}
        <span className="flex items-center gap-1.5">
          <span
            aria-hidden
            className="bg-heat-2 size-2.5 shrink-0 shadow-[inset_0_0_0_2px_var(--heat-flash)]"
          />
          updated in the last few seconds
        </span>
      </div>

      {map.unplaced > 0 && (
        <span className="basis-full">
          {map.unplaced} signal(s) could not be placed — their signature is gone.
        </span>
      )}
    </div>
  )
}
