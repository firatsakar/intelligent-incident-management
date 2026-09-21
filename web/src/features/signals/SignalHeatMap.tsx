import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { cn } from '@/lib/utils'
import type { SignalStatus } from '@/types/api'

import { cellKey, intensityOf, otherColumn, type HeatMap } from './heatmap'

// A sequential ramp, not red-green: the quantity has one direction, and a diverging or hue-based
// scale would imply a midpoint that does not exist here.
const intensityClass = [
  'bg-muted/40',
  'bg-sky-500/15',
  'bg-sky-500/30',
  'bg-sky-500/50',
  'bg-sky-600/70 text-white',
  'bg-sky-700/90 text-white',
]

// A second channel on top of colour: colour says how loud, the dot says whether the gate acted.
// Together the map answers both questions at once — where the noise is, and where the noise
// turned into an incident.
const bandDot: Record<SignalStatus, string | null> = {
  Promoted: 'bg-red-600',
  Deduplicated: 'bg-sky-600',
  Weak: 'bg-amber-500',
  Recorded: null,
  Suppressed: null,
}

const bandTitle: Record<SignalStatus, string> = {
  Promoted: 'opened an incident',
  Deduplicated: 'counted into an open incident',
  Weak: 'weak — shown, not raised',
  Recorded: 'recorded only',
  Suppressed: 'suppressed (muted signature)',
}

export function SignalHeatMap({
  map,
  selected,
  onSelect,
}: {
  map: HeatMap
  selected: { service: string; errorKey: string } | null
  onSelect: (cell: { service: string; errorKey: string } | null) => void
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Where the errors are</CardTitle>
        <CardDescription>
          Colour is how many times it fired, on a square-root scale. The dot is how far the gate
          took it.
        </CardDescription>
      </CardHeader>

      <CardContent>
        {map.services.length === 0 ? (
          // A freshly installed system has no signals, and a blank grid reads as broken rather
          // than as empty.
          <p className="text-muted-foreground py-6 text-center text-sm">
            No signals in this window. Nothing has crossed a detection rule yet.
          </p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full border-separate border-spacing-1 text-sm">
              <thead>
                <tr>
                  <th className="w-40" />
                  {map.columns.map((column) => (
                    <th
                      key={column}
                      className="text-muted-foreground px-1 pb-1 text-left align-bottom text-xs font-normal"
                    >
                      <span className="block max-w-28 truncate" title={column}>
                        {column}
                      </span>
                    </th>
                  ))}
                </tr>
              </thead>

              <tbody>
                {map.services.map((service) => (
                  <tr key={service}>
                    <td className="text-muted-foreground max-w-40 truncate pr-2 text-xs">
                      <span title={service}>{service}</span>
                    </td>

                    {map.columns.map((column) => {
                      const cell = map.cells.get(cellKey(service, column))
                      const isSelected =
                        selected?.service === service && selected?.errorKey === column

                      if (!cell) {
                        return (
                          <td key={column} className="p-0">
                            <div className="bg-muted/20 h-11 w-full rounded-md" />
                          </td>
                        )
                      }

                      // A plain title rather than a tooltip component: the cell already carries
                      // the number, and this adds the rest without a popup layer over a grid.
                      const label = column === otherColumn ? 'other signatures' : column
                      const description =
                        service +
                        ' · ' +
                        label +
                        ' — ' +
                        cell.occurrences +
                        ' occurrence(s) across ' +
                        cell.signalCount +
                        ' signal(s) — ' +
                        bandTitle[cell.band]

                      return (
                        <td key={column} className="p-0">
                          <button
                            type="button"
                            title={description}
                            onClick={() =>
                              onSelect(isSelected ? null : { service, errorKey: column })
                            }
                            className={cn(
                              'relative h-11 w-full rounded-md transition-all',
                              intensityClass[intensityOf(cell.occurrences, map.max)],
                              'hover:ring-primary/60 hover:ring-2',
                              isSelected && 'ring-primary ring-2',
                            )}
                          >
                            <span className="text-xs font-medium tabular-nums">
                              {cell.occurrences}
                            </span>

                            {bandDot[cell.band] && (
                              <span
                                className={cn(
                                  'absolute right-1 top-1 size-1.5 rounded-full',
                                  bandDot[cell.band],
                                )}
                              />
                            )}
                          </button>
                        </td>
                      )
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="text-muted-foreground mt-4 flex flex-wrap items-center gap-4 text-xs">
          <span className="flex items-center gap-1.5">
            <span className="size-1.5 rounded-full bg-red-600" /> opened an incident
          </span>
          <span className="flex items-center gap-1.5">
            <span className="size-1.5 rounded-full bg-amber-500" /> weak — shown, not raised
          </span>
          <span className="flex items-center gap-1.5">
            <span className="size-1.5 rounded-full bg-sky-600" /> counted into an open incident
          </span>

          {map.unplaced > 0 && (
            <span>{map.unplaced} signal(s) could not be placed — their signature is gone.</span>
          )}
        </div>
      </CardContent>
    </Card>
  )
}
