/**
 * The planned entries on the two catalogue screens, as ids.
 *
 * Their names and summaries live in the dictionary, which is total over these unions — so a
 * destination cannot be listed as "coming soon" without words in both languages, and a tile
 * cannot be quietly left in English by whoever adds the next one.
 *
 * In their own file because `en.ts` needs the types and the catalogues need the values, and
 * routing that through either catalogue would put a dictionary import into a module the
 * dictionary imports back.
 */

export type PlannedIntegrationId = 'slack' | 'teams' | 'pagerduty' | 'discord'

export type PlannedSourceId = 'otlp' | 'alerts'
