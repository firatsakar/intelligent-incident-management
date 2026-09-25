import { DatabaseIcon, RadioTowerIcon, WebhookIcon } from 'lucide-react'

import {
  telemetrySourceKinds,
  type TelemetrySource,
  type TelemetrySourceKind,
} from '@/types/api'

import type { Dictionary } from '@/lib/i18n'

import type { PlannedSourceId } from './plannedIds'
import type { MarkComponent } from './SettingsCatalogue'

/**
 * What this end of the platform is allowed to claim, built the same way the integrations catalogue
 * is: `connectable` is not a hand-kept array but `telemetrySourceKinds` — the same union the
 * server's TelemetrySourceKind enum and its keyed connector resolver agree on — mapped through a
 * Record the compiler forces to be total. Adding a kind breaks this file until someone writes the
 * tile, and no tile can appear for a kind the poller cannot resolve a connector for.
 *
 * It did its job when OTLP arrived: adding the kind broke this file until the tile existed.
 */

export interface SourceCatalogueEntry {
  kind: TelemetrySourceKind
  mark: MarkComponent
}

const entries: Record<TelemetrySourceKind, SourceCatalogueEntry> = {
  Seq: {
    kind: 'Seq',
    // A line icon rather than a vendor mark, unlike the integrations grid. The marks there were
    // drawn from published silhouettes; a Seq logo reproduced from memory would be a guess at
    // somebody's brand, and a wrong logo is a worse claim than an honest generic one.
    mark: DatabaseIcon,
  },
  Otlp: {
    kind: 'Otlp',
    // The mark it had while planned, so the tile a customer was told about is the one that appears.
    mark: RadioTowerIcon,
  },
}

export const connectable: SourceCatalogueEntry[] = telemetrySourceKinds.map((kind) => entries[kind])

/**
 * What is planned but not built, listed for the same reason the integrations catalogue lists Slack
 * and PagerDuty. Inert, because a control that errors is worse than an absent one.
 */
// Unlike the integrations catalogue, the name is descriptive rather than a trademark, so it lives
// in the dictionary with its summary.
export const planned: { id: PlannedSourceId; mark: MarkComponent }[] = [
  { id: 'alerts', mark: WebhookIcon },
]

/**
 * A source with no filter still reads something specific, and the pair of blank form controls that
 * produced it does not say what. Written out wherever a source is shown, the way an integration's
 * filters are — same reason, different pair of facts: an integration is defined by what it lets
 * through, a source by how often it looks and at what.
 *
 * What the connector selects when the source does not say mirrors SeqTelemetryConnector.
 */
export function describeSchedule(t: Dictionary, source: TelemetrySource): string {
  return describeDraftSchedule(t, source.pollIntervalSeconds, source.config.Filter ?? '')
}

/**
 * What the source selects, with no cadence and no verb — the paused row builds its own sentence
 * around it, and a language has to be free to put the interval somewhere else in that sentence.
 */
export function scheduleTarget(t: Dictionary, filter: string): string {
  const text = t.settings.telemetry
  const trimmed = filter.trim()

  return trimmed ? text.matching(trimmed) : text.defaultFilter
}

/** The same sentence for a source that does not exist yet, where there is no stored config. */
export function describeDraftSchedule(
  t: Dictionary,
  pollIntervalSeconds: number,
  filter: string,
): string {
  const text = t.settings.telemetry
  const trimmed = filter.trim()

  return text.polls(pollIntervalSeconds, trimmed ? text.matching(trimmed) : text.defaultFilter)
}
