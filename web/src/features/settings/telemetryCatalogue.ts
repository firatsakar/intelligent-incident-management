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
 * Today that union has exactly one member, so this screen is one tile rather than a grid. The
 * mechanism still earns its place: it is what makes the second connector a compile error here
 * instead of a screen nobody remembered to update.
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
}

export const connectable: SourceCatalogueEntry[] = telemetrySourceKinds.map((kind) => entries[kind])

/**
 * The two planned general solutions, listed for the same reason the integrations catalogue lists
 * Slack and PagerDuty: a customer whose log store is not Seq should be able to see that the answer
 * is a standard wire format rather than a queue of vendor connectors. Inert, because a control that
 * errors is worse than an absent one.
 */
// Unlike the integrations catalogue, these two names are descriptive rather than trademarks, so
// they live in the dictionary with their summaries.
export const planned: { id: PlannedSourceId; mark: MarkComponent }[] = [
  { id: 'otlp', mark: RadioTowerIcon },
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
