import type { WindowPreset } from '@/lib/window'
import type { RealtimeStatus } from '@/app/RealtimeProvider'

import type { Language } from './locale'
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
