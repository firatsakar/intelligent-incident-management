import type { WindowPreset } from '@/lib/window'
import type { RealtimeStatus } from '@/app/RealtimeProvider'

import type { Language } from './locale'
import { plural } from './translate'

/**
 * The eight scoring terms this build has been taught. Not every key the gate can emit —
 * that set is open and `scoreTerm()` falls back to splitting the camel case — but these
 * eight must have words in every language, so they go through `byKey`.
 */
type ScoreTerm =
  | 'fatal'
  | 'burstBase'
  | 'overThreshold'
  | 'rateAnomaly'
  | 'precedent'
  | 'blastRadius'
  | 'falsePositivePrecedent'
  | 'muted'
import type {
  DeliveryStatus,
  IncidentPriority,
  IncidentSource,
  IncidentStatus,
  LogSeverity,
  NotificationChannelType,
  SignalKind,
  SignalStatus,
  TelemetrySourceKind,
} from '@/types/api'

/**
 * The source dictionary, and the shape every other language has to match.
 *
 * It is an object, not a lookup function. A screen writes `const { profile } = useT()` and then
 * `profile.title`, so there are no string key paths — which means a key cannot be mistyped, and a
 * diff that touches four hundred strings cannot silently render the wrong sentence. Anything that
 * takes a value is a function here, so the substitution is a template literal the compiler checks
 * rather than a runtime `t(key, { count })` it cannot.
 *
 * `tr.ts` is typed as `Dictionary`, so a missing Turkish key fails the build instead of quietly
 * falling back to English, and a signature mismatch fails it too.
 *
 * Every `Record<Union, string>` below is deliberate: adding a member to one of those unions breaks
 * this file until somebody writes the label, which is the same totality trick
 * `features/settings/integrationCatalogue.ts` uses to keep the catalogue honest.
 *
 * What is NOT here, on purpose: prose that arrives from the services. An analysis's reasoning, a
 * detection's reason, a provider's error message. Translating those would mean either translating
 * the model's output or inventing a message the provider did not send.
 */
/**
 * Forces a label map to be total over its union, and widens the values back to `string` so the
 * Turkish dictionary is not made to repeat the English words.
 *
 * `as Record<...>` would not do it — an assertion is not a check — and `satisfies` would pin the
 * values to their English literals, which is the opposite of what a translation file needs.
 */
const byKey = <K extends string>(labels: Record<K, string>): Record<K, string> => labels

export const en = {
  common: {
    selected: 'Selected',
    // One wording for one action: the account menu and the profile card both read this.
    signOut: 'Sign out',
  },

  nav: {
    skip: 'Skip to content',
    open: 'Open navigation',
    sections: 'Sections',
    drawer: 'Move between the console’s sections.',

    groups: {
      operations: 'Operations',
      pipeline: 'Pipeline',
    },

    // Keyed rather than listed, so `AppLayout`'s array indexes this by a key the compiler
    // checks — a nav entry cannot exist without a label, in either language.
    items: {
      dashboard: 'Dashboard',
      incidents: 'Incidents',
      signals: 'Signals',
      evidence: 'Evidence',
      funnel: 'Funnel',
      services: 'Services',
      deliveries: 'Deliveries',
      settings: 'Settings',
    },
  },

  account: {
    menu: (name: string) => `Account — ${name}`,
    unverifiedTitle: 'Unverified session',
    unverified:
      'Nothing checked who you are. This name labels the session; the services behind the console answer anyone who can reach them.',
  },

  realtime: {
    status: byKey<RealtimeStatus>({
      connecting: 'connecting',
      live: 'live',
      reconnecting: 'reconnecting',
      offline: 'offline',
    }),

    hint: byKey<RealtimeStatus>({
      connecting: 'Opening the update channel.',
      live: 'Updates are pushed as they happen.',
      reconnecting: 'The update channel dropped and is being re-established.',
      offline: 'Screens still work, but they will not update on their own.',
    }),

    /** Read aloud after the word, so "live" is not announced as a stray adjective. */
    suffix: '— realtime connection',
  },

  settingsNav: {
    title: 'Settings',
    intro:
      'Your profile, the log stores this platform reads from, and the destinations it sends what it finds to.',
    sections: 'Settings sections',
    pages: {
      profile: 'Profile',
      telemetry: 'Telemetry',
      integrations: 'Integrations',
    },
  },

  login: {
    tagline: 'It shows its work.',
    taglineDetail:
      'The platform watches your log store, decides on the record whether something is worth waking a human for, and then explains the decision it reached.',

    points: {
      gate: {
        title: 'A gate you can read',
        detail:
          'Signals are scored by explicit rules before any model is involved, and the arithmetic stays on the incident — including the components that came out negative.',
      },
      restraint: {
        title: 'And what it did not raise',
        detail:
          'Weak and suppressed signals stay on the record next to the detection latency, so a quiet hour reads as quiet rather than as unexplained.',
      },
      routing: {
        title: 'Routed, not broadcast',
        detail:
          'A finished analysis reaches the destinations you configured, filtered by priority, and every attempt keeps its delivery result.',
      },
    },

    heading: 'Sign in',
    subheading: 'Choose the name this session runs under.',
    organisation: 'Organisation',
    ownership:
      'Incidents, signals and integrations belong to the organisation rather than to the person who opened them. This build has one.',
    nameLabel: 'Your name',
    nameHint: 'Labels this session in the console. Nothing checks it.',
    // A required field, not a rejected credential. It says what is missing and why it is wanted,
    // and never implies that something was checked.
    nameRequired: 'Enter a name. It is only used to label this session.',
    noPasswordTitle: 'No password, because nothing would check it',
    noPassword:
      'This build has no authentication. The services behind this console answer anyone who can reach them, and signing in here only decides whose name the session carries. Real sign-in arrives with the gateway.',
    submit: 'Enter the console',
  },

  language: {
    change: 'Change language',
    title: 'Language',
    description:
      'Stored in this browser, not against your name — a second machine reads it from the browser again.',
    legend: 'Language',
    // Each option is titled with its own endonym from `languageName`; this says what choosing it
    // actually changes, which is more than the words — the dates and the thousands separators
    // move with it.
    options: byKey<Language>({
      en: 'The console’s own text, dates and numbers in English.',
      tr: 'The console’s own text, dates and numbers in Turkish.',
    }),
    // Said once, here, rather than as a footnote on every screen that shows a server message.
    passthrough:
      'The console’s own text is translated. Text that arrives from the services — an analysis’s reasoning, a detection’s reason, a provider’s error — is passed through exactly as it was written, in English.',
  },

  theme: {
    change: 'Change theme',
    title: 'Appearance',
    description:
      'Stored in this browser, not against your name — a second machine starts on System again.',
    legend: 'Theme',
    options: {
      light: { label: 'Light', detail: 'Always the light palette.' },
      dark: { label: 'Dark', detail: 'Always the dark palette.' },
      system: { label: 'System', detail: 'Follows your operating system.' },
    },
    palette: { light: 'light', dark: 'dark' },
    resolved: (palette: string) => `Your system is currently asking for the ${palette} palette.`,
  },

  profile: {
    title: 'Profile',
    intro:
      'The name this session runs under, the organisation everything on screen belongs to, and how the console looks and reads while you are in it.',
    identity: {
      title: 'Identity',
      description: 'Who this console thinks you are, and what that is worth.',
      notAnAccountTitle: 'This is not an account',
      notAnAccount:
        'The name above is stored in this browser and nothing verified it. There is no password, no profile on any server, and no permission attached to it — the services behind this console answer anyone who can reach them, whatever name a session carries. To run under a different one, sign out and enter it. Real sign-in arrives with the gateway, and this page is where it will land.',
      ownership:
        'Incidents, signals, sources and integrations belong to the organisation rather than to the person who opened them. This build has one.',
    },
  },


  incidents: {
    list: {
      title: 'Incidents',
      intro: 'Everything the platform has opened, by hand or on its own.',
      // Says which count this is. "8 on record" next to an active filter is a claim about the
      // whole table that the table is not showing.
      matching: (count: number) => `${count} match these filters`,
      onRecord: (count: number) => `${count} on record`,

      filterStatus: 'Filter by status',
      filterPriority: 'Filter by priority',
      anyStatus: 'Any status',
      anyPriority: 'Any priority',
      /** The same two, as they read inside the empty-state sentence rather than in a control. */
      anyStatusInline: 'any status',
      anyPriorityInline: 'any priority',
      clear: 'Clear',
      clearFilters: 'Clear filters',

      caption: 'Incidents, newest first. Each row links to the incident.',
      columns: {
        priority: 'Priority',
        incident: 'Incident',
        source: 'Source',
        status: 'Status',
        analysis: 'Analysis',
        detected: 'Detected',
        latency: 'Latency',
      },

      loadError: 'Could not load incidents',

      // Two different situations, and only one of them is fixable by touching the filters.
      emptyFilteredTitle: 'Nothing matches these filters.',
      emptyFiltered: (status: string, priority: string) =>
        `There are incidents on record; none of them is both ${status} and ${priority}.`,
      emptyTitle: 'No incidents on record.',
      empty: 'Nothing has been opened by hand, and nothing has crossed a detection rule yet.',

      page: (current: number, total: number) => `Page ${current} of ${total}`,
      previous: 'Previous',
      next: 'Next',

      analysisFailed: 'analysis failed',
      awaitingAnalysis: 'awaiting analysis',
      analysed: 'analysed',
      toOpen: (duration: string) => `+${duration} to open`,
      // Opened by hand: there is no detection to have been slow. A zero would claim the platform
      // found it instantly.
      openedByHand: 'Opened by hand — nothing detected it',
    },

    detail: {
      loadErrorTitle: 'Could not load this incident',
      unknownError: 'Unknown error',
      started: (relative: string) => `started ${relative}`,

      statusLabel: 'Incident status',
      assignPlaceholder: 'Assign a team',
      reassignPlaceholder: 'Reassign to…',
      assignLabel: 'Assign a team',
      reassignLabel: 'Reassign to a different team',
      assign: 'Assign',
      assigning: 'Assigning…',

      whatHappened: 'What happened',
      fromDetector:
        'Written by the detector from the log records themselves — this is the same text the analysis read.',
      fromOperator: 'As entered when the incident was opened.',

      openedByHand: 'Opened by hand',
      openedByHandDetail:
        'Nothing detected this, so there is no detection latency to measure — the platform was told rather than noticing. The score breakdown below is absent for the same reason.',
      problemStarted: 'Problem started',
      sourceClock: 'on the source’s clock',
      incidentOpened: 'Incident opened',
      ourClock: 'on ours',
      detectionLatency: 'Detection latency',
      clockDisagreement: 'Clock disagreement',
      latencyHintLabel: 'What detection latency measures',
      latencyHint:
        'From the first log line the source stamped to the moment this record was filed — the log store’s clock to ours. It covers the poll interval, the detection pass and the scoring, and it is the whole of what the platform spent noticing this by itself.',
      skewHint:
        'The source reported this as starting after we filed the record, which can only mean the two clocks disagree. The figure is the size of that disagreement, not a latency.',
      noticed: 'noticed without being told',
      skewNote: 'source clock is ahead of ours',
    },

    timeline: {
      title: 'Timeline',
      description:
        'Five moments the services record separately. Where a time is missing, it is missing from the record rather than from this screen.',

      problemStarted: 'Problem started',
      onSourceClock: 'On the source clock, not ours.',
      notRecorded: 'Not recorded — this incident was opened by hand.',

      incidentOpened: 'Incident opened',
      toDetect: (duration: string) => `+${duration} to detect`,
      ourClockGap: 'Our clock. The gap above is what detection cost.',

      analysisApplied: 'Analysis applied',
      categorised: (category: string, priority: string) =>
        `Categorised as ${category}, priority set to ${priority}`,
      applied: 'Applied.',
      analysisFailed: 'The analysis ran and returned nothing. See the panel for the reason.',
      analysisWaiting: 'Waiting on the analysis service.',

      peopleNotified: 'People notified',
      afterOpening: (duration: string) => `+${duration} after opening`,
      channelsDelivered: (sent: number, total: number) =>
        `${sent} of ${total} ${plural('en', total, { one: 'channel', other: 'channels' })} delivered`,
      noDeliveryYet: 'No delivery recorded yet.',
      everyChannelFailed: 'Every configured channel failed — see the notifications panel.',

      lastChanged: 'Last changed',
      anyEdit: 'Any edit — status, team, or the analysis landing.',

      // "done" rather than a time, and said as a word so nobody reads an em dash as "never
      // happened".
      doneUntimed: 'done · time not recorded',
      notYet: 'not yet',
    },

    score: {
      // Not "Why this was raised": the detector writes that exact phrase as a heading inside the
      // evidence summary, which renders in the card immediately above this one.
      title: 'How the gate scored it',
      description:
        'A deterministic score, not a judgement call. Every term is recorded so the decision can be argued with afterwards.',

      total: 'Total',
      totalNote: 'sum of the terms above, clamped to 1.00',
      confidence: 'Confidence',
      confidenceNote: 'scoring was bypassed',

      // The threshold is per-rule and not exposed, so this states what the outcome was rather
      // than inventing the number it was compared against.
      defaultReason: 'It met the promotion threshold for its detection rule.',

      signature: 'Signature',
      muted: 'muted',
      occurrences: (count: number, promotions: number, real: number, falsePositive: number) =>
        `${count} ${plural('en', count, { one: 'occurrence', other: 'occurrences' })} recorded in total · promoted ${promotions}×, ${real} confirmed real, ${falsePositive} false positive`,

      measuresLabel: (term: string) => `What ${term} measures`,
    },

    notifications: {
      title: 'Notifications',
      summary: (sent: number, total: number) => `${sent} of ${total} delivered`,
      failed: (count: number) => `${count} failed`,
      empty:
        'Nothing sent yet. Notifications go out once the analysis completes, to every enabled integration whose filters match.',
      // An integration deleted after the fact leaves its deliveries behind, which is correct: the
      // notification did happen, and the row is the only proof of it.
      deletedIntegration: 'deleted integration',
      took: (duration: string) => `took ${duration}`,
      // Queued rather than sent. Calling it "sent" is the one thing this panel must never do.
      queued: (when: string) => `queued ${when}`,
      attempts: (count: number) =>
        `${count} ${plural('en', count, { one: 'attempt', other: 'attempts' })}`,
    },

    analysis: {
      title: 'AI analysis',
      description:
        'Enrichment on top of the deterministic gate — it set the category and the priority, not whether this was raised.',

      failedDescription:
        'The analysis ran and did not produce a result. Nothing further is coming on its own — the priority and category below are the ones detection set.',
      failedTitle: 'Analysis failed',
      failedFooter: 'A later attempt that succeeds clears this and fills the panel in.',

      waiting:
        'Waiting for the analysis service. It reads the evidence summary carried on the incident, so no extra call is made on its behalf.',

      confidence: 'Confidence',
      confidenceHintLabel: 'What the confidence figure means',
      confidenceHint:
        'How sure the analysis was of its own category and priority — not how severe the incident is, and not how certain the gate was that something broke. Those are the score on the left.',
      // Null is a missing measurement, not a zero. An empty bar would read as "certain this is
      // nothing", which is the opposite of what it means.
      noConfidence:
        'The analysis did not put a number on it. That is not the same as being unsure — it declined to quantify, so there is nothing to draw.',

      reasoning: 'Reasoning',
    },
  },

  /**
   * The detection gate's own vocabulary.
   *
   * `SignalScoring` emits its breakdown with the key names it uses internally, which are accurate
   * and meaningless to anybody who has not read that file. The eight terms this build has been
   * taught are total below; a term the gate grows later still renders, through `scoreTerm`'s
   * camel-case fallback, because the arithmetic has to add up on screen.
   *
   * The help text says what each term *is* rather than what it is worth: the weights are constants
   * in `SignalScoring`, and a number copied into the frontend is a number that will go stale.
   */
  scoreTerms: {
    label: byKey<ScoreTerm>({
      fatal: 'fatal error',
      burstBase: 'burst base',
      overThreshold: 'over threshold',
      rateAnomaly: 'rate anomaly',
      precedent: 'precedent',
      blastRadius: 'blast radius',
      falsePositivePrecedent: 'false-positive history',
      muted: 'muted signature',
    }),

    help: byKey<ScoreTerm>({
      fatal:
        'The process crashed. A crash is not a judgement call, so it skips scoring entirely and goes straight through.',
      burstBase: 'The starting score every burst gets for clearing its detection rule at all.',
      overThreshold:
        'How far past the rule’s threshold the burst went, counted in doublings and capped — twice over is meaningfully worse, fifty times over is not.',
      rateAnomaly:
        'This signature’s own rate history says this volume is unusual for it. The strongest corroboration available without a second data source.',
      precedent: 'This signature has produced a confirmed real incident before.',
      blastRadius: 'Two or more services are raising it, not one.',
      falsePositivePrecedent:
        'This signature has been marked a false positive before, so the score is pulled down.',
      muted:
        'Somebody muted this signature. It is scored, and heavily penalised for being muted.',
    }),
  },

  // ---- the enum vocabulary ---------------------------------------------------------------
  //
  // Display only. The wire value never changes: a filter still sends `InProgress`, and the badge
  // beside it still reads "In progress". One source, so a badge and a filter cannot drift apart.
  labels: {
    incidentStatus: byKey<IncidentStatus>({
      Open: 'Open',
      InProgress: 'In progress',
      Resolved: 'Resolved',
      Closed: 'Closed',
    }),

    priority: byKey<IncidentPriority>({
      Critical: 'Critical',
      High: 'High',
      Medium: 'Medium',
      Low: 'Low',
    }),

    incidentSource: byKey<IncidentSource>({
      Manual: 'Manual',
      Telemetry: 'Telemetry',
      Alert: 'Alert',
    }),

    signalStatus: byKey<SignalStatus>({
      Promoted: 'Promoted',
      Weak: 'Weak',
      Recorded: 'Recorded only',
      Deduplicated: 'Deduplicated',
      Suppressed: 'Suppressed',
    }),

    // Lower case: these are printed inside a sentence rather than on a badge of their own.
    signalKind: byKey<SignalKind>({
      LogBurst: 'burst',
      RateAnomaly: 'rate anomaly',
    }),

    severity: byKey<LogSeverity>({
      Fatal: 'Fatal',
      Error: 'Error',
      Warning: 'Warning',
      Information: 'Information',
      Debug: 'Debug',
      Verbose: 'Verbose',
    }),

    deliveryStatus: byKey<DeliveryStatus>({
      Pending: 'Pending',
      Sent: 'Sent',
      Failed: 'Failed',
    }),

    // Product names, so most of these are the same in every language. They live here anyway so
    // that a screen has one place to ask, rather than two conventions for the same kind of thing.
    channel: byKey<NotificationChannelType>({
      Email: 'Email',
      Webhook: 'Webhook',
      Jira: 'Jira',
    }),

    telemetryKind: byKey<TelemetrySourceKind>({
      Seq: 'Seq',
    }),
  },

  window: {
    label: 'Time window',

    option: byKey<WindowPreset>({
      '30m': 'Last 30 minutes',
      '2h': 'Last 2 hours',
      '24h': 'Last 24 hours',
      '7d': 'Last 7 days',
    }),

    /**
     * The same window as it appears inside a sentence — "… per service, last 24 hours."
     *
     * Written out rather than lower-cased from the option above. `toLowerCase()` on a Turkish
     * string is not a display transform, it is a language operation with its own rules, and a
     * sentence that reads well is not always the picker's label with a small first letter.
     */
    scope: byKey<WindowPreset>({
      '30m': 'last 30 minutes',
      '2h': 'last 2 hours',
      '24h': 'last 24 hours',
      '7d': 'last 7 days',
    }),

    dayScope: (days: number) => `last ${days} days`,
  },

  format: {
    justNow: 'just now',
    minutesAgo: (minutes: number) => `${minutes}m ago`,
    hoursAgo: (hours: number) => `${hours}h ago`,
    daysAgo: (days: number) => `${days}d ago`,
    notGiven: 'not given',
    confidence: {
      high: 'high confidence',
      moderate: 'moderate confidence',
      low: 'low confidence',
    },
  },
}

export type Dictionary = typeof en
