import { MailIcon, WebhookIcon } from 'lucide-react'

import type { Dictionary } from '@/lib/i18n'
import { notificationChannels, type Integration, type NotificationChannelType } from '@/types/api'

import { DiscordMark, JiraMark, PagerDutyMark, SlackMark, TeamsMark } from './BrandIcons'
import type { PlannedIntegrationId } from './plannedIds'
import type { MarkComponent } from './SettingsCatalogue'

/**
 * What the catalogue is allowed to claim.
 *
 * The connectable list is not a hand-kept array: it is `notificationChannels` — the same union the
 * server's NotificationChannelType enum and its keyed DI resolve — mapped through a Record that the
 * compiler forces to be total. Adding a channel to the union breaks this file until someone writes
 * the tile, and no tile can appear here for a channel the backend cannot dispatch. That is the
 * whole line between "connect this" and "coming soon", and it is drawn by the type system rather
 * than by memory.
 */

export interface CatalogueEntry {
  channel: NotificationChannelType
  /**
   * A protocol gets lucide's line icon, a product gets its own filled mark. The two weights
   * sitting in one grid is not an oversight — it is the tile saying whether what you are about to
   * configure is a standard or somebody's product.
   */
  mark: MarkComponent
}

// The name and the summary are in the dictionary — `labels.channel` and
// `settings.integrations.summary`, both total over this same union. What stays here is the mark,
// which is not language.
const entries: Record<NotificationChannelType, CatalogueEntry> = {
  Email: { channel: 'Email', mark: MailIcon },
  Webhook: { channel: 'Webhook', mark: WebhookIcon },
  Jira: { channel: 'Jira', mark: JiraMark },
}

export const connectable: CatalogueEntry[] = notificationChannels.map((channel) => entries[channel])

/**
 * Destinations with no code behind them. They are listed because a catalogue of three reads as a
 * product with three integrations rather than as a product whose directory is filling up, and they
 * are inert because a button that errors is worse than an absent one.
 */
export const planned: { id: PlannedIntegrationId; name: string; mark: MarkComponent }[] = [
  // The names are trademarks, so they are not translated; the summaries are.
  { id: 'slack', name: 'Slack', mark: SlackMark },
  { id: 'teams', name: 'Microsoft Teams', mark: TeamsMark },
  { id: 'pagerduty', name: 'PagerDuty', mark: PagerDutyMark },
  { id: 'discord', name: 'Discord', mark: DiscordMark },
]

/**
 * An integration with no filters matches every incident. Left as two blank form controls that is
 * indistinguishable from an unfinished setup, so the rule is written out wherever the integration
 * is shown.
 */
export function describeFilters(
  t: Dictionary,
  minPriority: Integration['minPriority'],
  categoryFilter: Integration['categoryFilter'],
): string {
  const text = t.settings.integrations
  const parts: string[] = []

  if (minPriority) parts.push(text.andAbove(t.labels.priority[minPriority]))
  if (categoryFilter) parts.push(text.category(categoryFilter))

  return parts.length > 0 ? text.sends(parts.join(' · ')) : text.sendsEverything
}

/**
 * The same filters as the *parts* of somebody else's sentence, with no verb and no full stop.
 *
 * The paused row needs to say "when resumed it will send X", and X cannot be a finished
 * sentence — spliced in, the English bound "when resumed" to the wrong clause and the Turkish
 * ended up with two colons. Null means there are no filters at all, which is a different
 * sentence rather than an empty one.
 */
export function filterParts(
  t: Dictionary,
  minPriority: Integration['minPriority'],
  categoryFilter: Integration['categoryFilter'],
): string | null {
  const text = t.settings.integrations
  const parts: string[] = []

  if (minPriority) parts.push(text.andAbove(t.labels.priority[minPriority]))
  if (categoryFilter) parts.push(text.category(categoryFilter))

  return parts.length > 0 ? parts.join(' · ') : null
}
