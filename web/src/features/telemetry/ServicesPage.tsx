import { ArrowDownIcon, ArrowUpIcon, ChevronsUpDownIcon } from 'lucide-react'
import { useSearchParams } from 'react-router-dom'

import { InfoHint } from '@/components/InfoHint'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { WindowSelect } from '@/components/WindowSelect'
import { formatCount, formatDateTime, formatRelative } from '@/lib/format'
import { T, useT, type Dictionary } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { resolveWindowPreset } from '@/lib/window'
import type { ServiceHealth } from '@/types/api'

import { defaultStatsWindow, useTelemetryStats } from './queries'

/**
 * Which service is producing the errors, and how far up the pipeline they got.
 *
 * Two rows here are easy to design away and both have to survive.
 *
 * A service that produced log records and no signals at all is in the rollup with zeros, and
 * dropping it would report it as healthy — a service throwing errors that nothing has fired on is
 * a fact worth a row, not an absent one. Its cells say what is absent rather than sitting blank.
 *
 * And a row can read `(signature gone)`. That is the server's own name for signals whose signature
 * has since been deleted: they still happened, so they are grouped under a name rather than
 * dropped. It is a bucket, not a service, so it is pinned below the real ones whichever way the
 * table is sorted and says what it is.
 */

/** The literal `GetTelemetryStatsQueryHandler.UnknownService` groups orphaned signals under. */
const goneService = '(signature gone)'

type SortKey = 'service' | 'logRecords' | 'signals' | 'promoted' | 'incidents' | 'lastSignalAt'
type SortDirection = 'asc' | 'desc'

/**
 * Incidents first, descending.
 *
 * The column that says "this service woke somebody" outranks the one that says "this service is
 * noisy": the screen is health, and volume is only the question until something has come of it.
 */
const defaultSort: SortKey = 'incidents'
const defaultDirection: SortDirection = 'desc'

type ServicesText = Dictionary['telemetry']['services']

interface Column {
  key: SortKey | 'topSignature'
  /** Keyed into the dictionary: a column cannot be added without a heading in both languages. */
  label: keyof ServicesText & `column${string}`
  /** Right-aligned, tabular, and a fresh click sorts it largest-first. */
  numeric: boolean
  /** Shared by the header and its cells so the two cannot drift apart on a breakpoint. */
  className: string
}

const columns: Column[] = [
  { key: 'service', label: 'columnService', numeric: false, className: 'pl-4' },
  // The widths are set by the *headers*, not the figures: a sortable header is its label plus a
  // sort marker, and a column cut to fit "11" clips the word "Signals" above it.
  {
    key: 'logRecords',
    label: 'columnLogRecords',
    numeric: true,
    className: 'hidden w-32 text-right md:table-cell',
  },
  { key: 'signals', label: 'columnSignals', numeric: true, className: 'w-24 text-right' },
  {
    key: 'promoted',
    label: 'columnPromoted',
    numeric: true,
    className: 'hidden w-28 text-right md:table-cell',
  },
  {
    key: 'incidents',
    label: 'columnIncidents',
    numeric: true,
    className: 'hidden w-28 text-right md:table-cell',
  },
  // Not sortable: it is a name, and ordering services by the alphabet of their worst exception
  // answers no question anybody has.
  {
    key: 'topSignature',
    label: 'columnTopSignature',
    numeric: false,
    className: 'hidden w-56 xl:table-cell',
  },
  {
    key: 'lastSignalAt',
    label: 'columnLastSignal',
    numeric: true,
    className: 'w-32 pr-4 text-right',
  },
]

const sortable = (column: Column): column is Column & { key: SortKey } =>
  column.key !== 'topSignature'

/** Looked up by key rather than by position, so reordering the columns cannot silently reassign
 *  a width to the wrong cell. */
const columnClass = Object.fromEntries(
  columns.map((column) => [column.key, column.className]),
) as Record<Column['key'], string>

export function ServicesPage() {
  const [params, setParams] = useSearchParams()
  const { telemetry, window: windowText } = useT()
  const t = telemetry.services

  const preset = params.get('window') ?? defaultStatsWindow
  const sort = resolveSort(params.get('sort'))
  const direction = resolveDirection(params.get('dir'))

  const query = useTelemetryStats(preset)

  const scope = windowText.scope[resolveWindowPreset(preset)]
  const rows = orderServices(query.data?.services ?? [], sort, direction)

  function sortBy(column: Column & { key: SortKey }) {
    // A fresh column starts the way that column is usually read — biggest first for a count,
    // A-to-Z for a name. Clicking the one already sorted turns it round.
    const next: SortDirection =
      sort === column.key ? (direction === 'asc' ? 'desc' : 'asc') : column.numeric ? 'desc' : 'asc'

    const updated = new URLSearchParams(params)

    // Back at the default, the params come off rather than being written out. A shared link then
    // reads the same as the one in the rail, and both mean the same thing.
    if (column.key === defaultSort && next === defaultDirection) {
      updated.delete('sort')
      updated.delete('dir')
    } else {
      updated.set('sort', column.key)
      updated.set('dir', next)
    }

    setParams(updated)
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-x-6 gap-y-3">
        <div className="max-w-2xl">
          <h1 className="text-2xl font-semibold tracking-tight">{t.title}</h1>
          <p className="text-muted-foreground text-sm">
            {/* The count only once there is one. "0 services produced something" is a sentence
                nobody writes, and the empty row below says the same thing properly. */}
            {query.data && rows.length > 0
              ? t.produced(formatCount(rows.length), rows.length)
              : t.intro}
          </p>
        </div>

        <WindowSelect value={preset} />
      </div>

      <Card className="overflow-hidden py-0">
        {/* table-fixed so the widths above are obeyed and long names truncate against the width
            they actually got, rather than widening the table until it scrolls sideways. */}
        <Table className="table-fixed">
          <TableCaption className="sr-only">{t.caption(scope)}</TableCaption>

          <TableHeader>
            <TableRow>
              {columns.map((column) => (
                <TableHead
                  key={column.key}
                  className={column.className}
                  aria-sort={
                    sortable(column) && sort === column.key
                      ? direction === 'asc'
                        ? 'ascending'
                        : 'descending'
                      : undefined
                  }
                >
                  {sortable(column) ? (
                    <button
                      type="button"
                      onClick={() => sortBy(column)}
                      className={cn(
                        'focus-visible:ring-ring/50 hover:text-foreground -mx-1 inline-flex min-h-6 items-center gap-1 rounded-sm px-1 align-middle outline-none focus-visible:ring-[3px]',
                        // The marker leads on a right-aligned column so the label itself stays
                        // flush with the numbers it heads.
                        column.numeric && 'flex-row-reverse',
                        sort === column.key ? 'text-foreground' : 'text-muted-foreground',
                      )}
                    >
                      {t[column.label]}
                      <SortMark active={sort === column.key} direction={direction} />
                    </button>
                  ) : (
                    t[column.label]
                  )}
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>

          <TableBody>
            {query.isPending &&
              Array.from({ length: 4 }).map((_, index) => (
                <TableRow key={index}>
                  <TableCell colSpan={columns.length} className="px-4">
                    <Skeleton className="h-6 w-full" />
                  </TableCell>
                </TableRow>
              ))}

            {query.isError && (
              <TableRow>
                <TableCell colSpan={columns.length} className="text-alarm-ink px-4 py-8 text-center">
                  {query.error instanceof Error ? query.error.message : t.loadError}
                </TableCell>
              </TableRow>
            )}

            {query.isSuccess && rows.length === 0 && (
              <TableRow>
                <TableCell colSpan={columns.length} className="px-4 py-10 text-center">
                  <p className="text-sm font-medium">{t.emptyTitle}</p>
                  <p className="text-muted-foreground mx-auto mt-1 max-w-md text-sm">{t.empty}</p>
                </TableCell>
              </TableRow>
            )}

            {rows.map((row) => (
              <ServiceRow key={row.service} row={row} />
            ))}
          </TableBody>
        </Table>
      </Card>

      <p className="text-muted-foreground max-w-3xl text-xs">
        <T
          text={t.footnote}
          values={{
            incidents: <strong className="font-medium">{t.columnIncidents}</strong>,
          }}
        />
      </p>
    </div>
  )
}

function SortMark({ active, direction }: { active: boolean; direction: SortDirection }) {
  const Icon = !active ? ChevronsUpDownIcon : direction === 'asc' ? ArrowUpIcon : ArrowDownIcon

  return (
    <Icon
      aria-hidden
      className={cn('size-3.5 shrink-0', active ? 'text-foreground' : 'text-dim-foreground')}
    />
  )
}

function ServiceRow({ row }: { row: ServiceHealth }) {
  const { labels, telemetry } = useT()
  const t = telemetry.services

  const gone = row.service === goneService
  const signature = describeTopSignature(t, row)

  return (
    <TableRow>
      <TableCell className={cn(columnClass.service, 'align-top')}>
        <span className="flex min-w-0 items-baseline gap-1">
          <span
            className={cn(
              'min-w-0 truncate font-medium',
              gone && 'text-muted-foreground font-normal',
            )}
            title={gone ? labels.goneService : row.service}
          >
            {/* A sentinel rather than a name, so its display is translated the way an enum's is.
                The literal stays the comparison key, which is what `gone` was decided from. */}
            {gone ? labels.goneService : row.service}
          </span>

          {gone && (
            <InfoHint label={t.goneHintLabel} side="right" className="-my-1">
              {t.goneHint}
            </InfoHint>
          )}
        </span>

        {/* What the narrow layouts drop, folded back in here rather than lost. The outer span goes
            away only once every column it carries is on screen. */}
        <span className="text-muted-foreground mt-0.5 block text-xs whitespace-normal xl:hidden">
          <span className="md:hidden">
            {t.folded(
              formatCount(row.logRecords),
              formatCount(row.promoted),
              formatCount(row.incidents),
            )}
          </span>
          {signature}
        </span>
      </TableCell>

      <Count className={columnClass.logRecords} value={row.logRecords} />
      <Count className={columnClass.signals} value={row.signals} />
      <Count className={columnClass.promoted} value={row.promoted} />
      <Count className={columnClass.incidents} value={row.incidents} />

      <TableCell className={cn(columnClass.topSignature, 'align-top text-sm')}>
        {row.topSignature ? (
          <>
            <span className="block truncate" title={row.topSignature}>
              {row.topSignature}
            </span>
            <span className="text-muted-foreground block text-xs tabular-nums">
              {formatCount(row.topSignatureOccurrences)}{' '}
              {t.occurrences(row.topSignatureOccurrences)}
            </span>
          </>
        ) : (
          <span className="text-dim-foreground block whitespace-normal">{signature}</span>
        )}
      </TableCell>

      <TableCell className={cn(columnClass.lastSignalAt, 'align-top text-sm tabular-nums')}>
        {row.lastSignalAt ? (
          <span title={formatDateTime(row.lastSignalAt)}>{formatRelative(row.lastSignalAt)}</span>
        ) : (
          // Never a date. There was no signal, so there is no time at which the last one was.
          <span className="text-dim-foreground" title={t.noSignal}>
            —
          </span>
        )}
      </TableCell>
    </TableRow>
  )
}

/** Zero is dimmed rather than hidden: it is a measurement, and most cells hold one. */
function Count({ className, value }: { className: string; value: number }) {
  return (
    <TableCell className={cn(className, 'align-top text-sm tabular-nums')}>
      <span className={cn(value === 0 && 'text-dim-foreground')}>{formatCount(value)}</span>
    </TableCell>
  )
}

/**
 * Why there is no top signature, which is two different facts.
 *
 * Nothing fired is the row this screen exists to keep — a service writing errors that the gate has
 * never fired on. A signature that fired and has since been deleted is the other, and it is the
 * only thing the `(signature gone)` bucket can ever say.
 */
function describeTopSignature(t: ServicesText, row: ServiceHealth): string {
  if (row.topSignature) {
    return `${row.topSignature} · ${formatCount(row.topSignatureOccurrences)}`
  }

  return row.signals > 0 ? t.signatureGone : t.nothingCrossed
}

/** The URL is the source of truth and anybody can type into it, so an unknown value is a default. */
function resolveSort(value: string | null): SortKey {
  const known = columns.filter(sortable).map((column) => column.key)

  return known.find((key) => key === value) ?? defaultSort
}

function resolveDirection(value: string | null): SortDirection {
  return value === 'asc' || value === 'desc' ? value : defaultDirection
}

function orderServices(
  rows: ServiceHealth[],
  key: SortKey,
  direction: SortDirection,
): ServiceHealth[] {
  const sign = direction === 'asc' ? 1 : -1

  // The orphan bucket is not a peer of the real services, so it is held out of the sort and put
  // back at the end — the same place every "other" bucket in this console goes. It is still fully
  // counted and fully visible; it just never claims to be the worst service you have.
  const services = rows.filter((row) => row.service !== goneService)
  const orphans = rows.filter((row) => row.service === goneService)

  const sorted = services.slice().sort((a, b) => {
    if (key === 'lastSignalAt') {
      const aMissing = a.lastSignalAt === null
      const bMissing = b.lastSignalAt === null

      // Outside the direction, deliberately. A service with no signal has no last-signal time at
      // all; flipping it to the top on an ascending sort would be reading that absence as 1970.
      if (aMissing !== bMissing) return aMissing ? 1 : -1
    }

    const gap = compareBy(a, b, key) * sign

    return gap !== 0 ? gap : a.service.localeCompare(b.service)
  })

  return [...sorted, ...orphans]
}

function compareBy(a: ServiceHealth, b: ServiceHealth, key: SortKey): number {
  if (key === 'service') return a.service.localeCompare(b.service)

  if (key === 'lastSignalAt') {
    // Parsed rather than compared as text: these carry a variable number of fractional digits, and
    // "…23.1Z" sorts above "…23.15Z" on a string comparison.
    return (a.lastSignalAt ? Date.parse(a.lastSignalAt) : 0) -
      (b.lastSignalAt ? Date.parse(b.lastSignalAt) : 0)
  }

  return a[key] - b[key]
}
