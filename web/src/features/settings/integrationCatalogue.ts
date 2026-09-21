import { MailIcon, WebhookIcon } from 'lucide-react'
import type { ComponentType } from 'react'

import { notificationChannels, type Integration, type NotificationChannelType } from '@/types/api'

import { DiscordMark, JiraMark, PagerDutyMark, SlackMark, TeamsMark } from './BrandIcons'

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

/** Both lucide icons and the local marks accept a className; nothing else is needed from them. */
export type MarkComponent = ComponentType<{ className?: string }>

export interface CatalogueEntry {
  channel: NotificationChannelType
  name: string
  /**
   * A protocol gets lucide's line icon, a product gets its own filled mark. The two weights
   * sitting in one grid is not an oversight — it is the tile saying whether what you are about to
   * configure is a standard or somebody's product.
   */
  mark: MarkComponent
  summary: string
}

const entries: Record<NotificationChannelType, CatalogueEntry> = {
  Email: {
    channel: 'Email',
    name: 'Email',
    mark: MailIcon,
    summary: 'SMTP to a mailbox or a distribution list.',
  },
  Webhook: {
    channel: 'Webhook',
    name: 'Webhook',
    mark: WebhookIcon,
    summary: 'An HTTP POST of the incident and its analysis to an endpoint you control.',
  },
  Jira: {
    channel: 'Jira',
    name: 'Jira',
    mark: JiraMark,
    summary: 'Opens an issue in a project, with the reasoning in the description.',
  },
}

export const connectable: CatalogueEntry[] = notificationChannels.map((channel) => entries[channel])

export interface PlannedEntry {
  name: string
  mark: MarkComponent
  summary: string
}

/**
 * Destinations with no code behind them. They are listed because a catalogue of three reads as a
 * product with three integrations rather than as a product whose directory is filling up, and they
 * are inert because a button that errors is worse than an absent one.
 */
export const planned: PlannedEntry[] = [
  { name: 'Slack', mark: SlackMark, summary: 'Post to a channel.' },
  { name: 'Microsoft Teams', mark: TeamsMark, summary: 'Post to a team channel.' },
  { name: 'PagerDuty', mark: PagerDutyMark, summary: 'Page whoever is on call.' },
  { name: 'Discord', mark: DiscordMark, summary: 'Post to a channel.' },
]

/**
 * An integration with no filters matches every incident. Left as two blank form controls that is
 * indistinguishable from an unfinished setup, so the rule is written out wherever the integration
 * is shown.
 */
export function describeFilters(
  minPriority: Integration['minPriority'],
  categoryFilter: Integration['categoryFilter'],
): string {
  const parts: string[] = []

  if (minPriority) parts.push(`${minPriority} and above`)
  if (categoryFilter) parts.push(`category ${categoryFilter}`)

  return parts.length > 0 ? `Sends ${parts.join(' · ')}` : 'Sends every incident — no filters set'
}
