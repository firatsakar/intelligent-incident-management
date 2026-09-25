import type { WindowPreset } from '@/lib/window'
import type { RealtimeStatus } from '@/app/RealtimeProvider'
import type { ConfigFieldId } from '@/features/settings/configSchema'
import type {
  PlannedIntegrationId,
  PlannedSourceId,
} from '@/features/settings/plannedIds'

import type { Language } from './locale'
import { plural } from './translate'

/**
 * The scoring terms this build has been taught. Not every key the gate can emit — that set is
 * open and `scoreTerm()` falls back to splitting the camel case — but these must have words in
 * every language, so they go through `byKey`.
 *
 * `precedent` and `falsePositivePrecedent` are no longer emitted: Adım 24 folded them into
 * `history`. They stay because signals recorded before then carry them in their breakdown, and an
 * old record should keep reading as what it was.
 */
type ScoreTerm =
  | 'fatal'
  | 'burstBase'
  | 'overThreshold'
  | 'rateAnomaly'
  | 'history'
  | 'precedent'
  | 'blastRadius'
  | 'falsePositivePrecedent'
  | 'muted'
import type {
  DeliveryStatus,
  IncidentPriority,
  IncidentSource,
  IncidentStatus,
  IncidentVerdict,
  LogSeverity,
  NotificationChannelType,
  SignalKind,
  SignalStatus,
  TelemetrySourceKind,
  UserRole,
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

/** The same, for an entry that is more than one string. */
const byKeyOf = <K extends string, V>(entries: Record<K, V>): Record<K, V> => entries

export const en = {
  common: {
    selected: 'Selected',
    // The close control on every dialog and on the navigation drawer. It is icon-only, so this
    // is the whole of what a screen reader has to go on.
    close: 'Close',
    // One wording for one action: the account menu and the profile card both read this.
    signOut: 'Sign out',
    copy: 'Copy',
    copied: 'Copied',
    // Three ways a request can fail that say nothing about what was typed, each with its own next
    // step. Shared by every form that talks to the account endpoints.
    unreachable: 'Could not reach the server. Check your connection and try again.',
    tooMany: 'Too many attempts. Wait a minute and try again.',
    serverError: 'The server is not answering right now. Try again in a moment.',
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
    sections: 'Settings sections',
    pages: {
      profile: 'Profile',
      members: 'Members',
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
          'A finished analysis reaches the channels you configured, filtered by priority, and every attempt keeps its delivery result.',
      },
    },

    heading: 'Sign in',
    subheading: 'Your organisation is the one your account belongs to.',
    ownership:
      'Incidents, signals, sources and integrations belong to the organisation rather than to the person who opened them, so what you see here is your team’s.',
    emailLabel: 'Email address',
    passwordLabel: 'Password',
    // An empty field, not a rejected credential — the reader can see which one is blank, so this
    // only has to say that both are wanted.
    required: 'Enter your email address and password.',
    // The server refuses a wrong password, an unknown address and a deactivated account with one
    // answer, on purpose. Saying more here than it does would undo that.
    failed: 'That email address and password do not match an active account.',
    // Three ways this can fail that are not a wrong password, each with a different next step.
    // Reporting any of them as a refused credential sends the reader to change something that was
    // never wrong — and it is what this screen did until somebody met a stopped service and was
    // told their password no longer worked.
    unreachable: 'Could not reach the server. Check your connection and try again.',
    tooMany: 'Too many attempts. Wait a minute and try again.',
    serverError: 'Sign-in is not answering right now. Try again in a moment.',
    submit: 'Enter the console',
    submitting: 'Signing in…',
    forgot: 'Forgot your password? An Admin of your organisation can send you a link to set a new one.',
  },

  // ---- the account's own screens (Adım 16.5) ---------------------------------------------

  /** The rule is the server's (`PasswordRules`); these say it before and after it is broken. */
  passwordRules: {
    hint: 'At least 12 characters. A few unrelated words are easier to remember and harder to guess than one clever word.',
    tooShort: 'Use at least 12 characters.',
    tooLong: 'Too long — only the first 72 bytes would count. Use fewer characters.',
    mismatch: 'The two passwords are not the same.',
  },

  /** What the invitation and reset pages share. */
  oneTimeLink: {
    checking: 'Checking the link',
    deadTitle: 'This link does not work',
    // An unknown, expired, replaced and used link are one answer from the server, on purpose, so
    // this is one sentence that covers all four and says what to do about any of them.
    dead: 'It may have expired, been replaced by a newer one, or already been used. Ask an Admin of your organisation for a new one.',
    toSignIn: 'Go to sign in',
    unavailableTitle: 'The link could not be checked',
    retry: 'Try again',
  },

  invite: {
    title: (organization: string) => `Join ${organization}`,
    subtitle: 'Choose the name your team will see, and a password.',
    email: 'Email',
    role: 'Role',
    name: 'Your name',
    nameHint: 'How the rest of your organisation will see you.',
    nameRequired: 'Enter your name.',
    password: 'Password',
    repeat: 'Repeat the password',
    signedInAs: (name: string) =>
      `This browser is signed in as ${name}. Creating the account signs you in as the new one instead.`,
    expires: (when: string) => `This invitation works once, until ${when}.`,
    submit: 'Create account',
    submitting: 'Creating account…',
  },

  reset: {
    title: 'Choose a new password',
    subtitle: (name: string, email: string) => `${name} · ${email}`,
    password: 'New password',
    repeat: 'Repeat the new password',
    sessions:
      'Every session of this account is signed out, and this browser is signed in with the new password.',
    expires: (when: string) => `This link works once, until ${when}.`,
    submit: 'Set password',
    submitting: 'Setting password…',
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
    identity: {
      title: 'Identity',
      ownership:
        'Incidents, signals, sources and integrations belong to the organisation rather than to the person who opened them. Its Admins decide who is a member and what each member may do.',
    },

    password: {
      title: 'Password',
      description:
        'Changing it signs out every other session of this account. This one stays signed in.',
      current: 'Current password',
      currentRequired: 'Enter your current password.',
      wrongCurrent: 'That is not your current password.',
      next: 'New password',
      repeat: 'Repeat the new password',
      same: 'Choose a password different from the current one.',
      submit: 'Change password',
      submitting: 'Changing…',
      changed: 'Password changed. Other sessions were signed out.',
    },
  },


  incidents: {
    list: {
      title: 'Incidents',
      // Says which count this is. "8 on record" next to an active filter is a claim about the
      // whole table that the table is not showing.
      matching: (count: number) => `${count} match these filters`,
      onRecord: (count: number) => `${count} on record`,

      filterStatus: 'Filter by status',
      filterPriority: 'Filter by priority',
      anyStatus: 'Any status',
      anyPriority: 'Any priority',
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
      // One sentence per filter combination rather than one sentence with two holes. Spliced,
      // the unset half read "none of them is both Open and any priority" — which says every
      // incident has no priority, and the Turkish said it more explicitly still.
      emptyFiltered: (status: string, priority: string) =>
        `There are incidents on record; none of them is both ${status} and ${priority} priority.`,
      emptyFilteredStatus: (status: string) =>
        `There are incidents on record; none of them is ${status}.`,
      emptyFilteredPriority: (priority: string) =>
        `There are incidents on record; none of them is ${priority} priority.`,
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

      closed: (relative: string) => `closed ${relative}`,
      statusUpdated: 'Status updated',
      assignedTo: (team: string) => `Assigned to ${team}`,

      // Asked when an open incident is closed (Adım 24). Each option says what it does to the
      // detector, so the choice is made knowing its effect.
      verdictDialog: {
        title: 'Was this a real problem?',
        description: (status: string) =>
          `Asked once, before it is marked ${status}. The answer stays on the incident, and if the detector raised it, it counts towards that error’s track record.`,
        effect: byKey<IncidentVerdict>({
          Real: 'Something was actually wrong. The next burst of the same error is a little more likely to open an incident.',
          FalsePositive:
            'Nothing needed doing. The next burst of the same error has to be stronger to open an incident.',
        }),
        cancel: 'Cancel',
        confirm: (status: string) => `Mark as ${status}`,
      },
    },

    timeline: {
      title: 'Timeline',
      description:
        'Where a time is missing, it is missing from the record rather than from this screen.',

      problemStarted: 'Problem started',
      onSourceClock: 'On the source clock, not ours.',
      notRecorded: 'Not recorded — this incident was opened by hand.',

      incidentOpened: 'Incident opened',
      toDetect: (duration: string) => `+${duration} to detect`,
      ourClockGap: 'Our clock. The gap above is what detection cost.',

      analysisApplied: 'Analysis applied',
      categorised: (category: string, priority: string) =>
        `Categorised as ${category}, priority set to ${priority}`,
      applied: 'Applied — the analysis returned no category.',
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
      description: 'A deterministic score, not a judgement call.',

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
        'Waiting for the analysis service. This resolves itself — a failure would say so here instead.',

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
      history: 'track record',
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
      history:
        'What this signature’s past incidents turned out to be when they were closed: the share judged real, discounted while there are few verdicts. Always between −0.25 (every one a false positive) and +0.15 (every one real).',
      precedent: 'This signature has produced a confirmed real incident before.',
      blastRadius: 'Two or more services are raising it, not one.',
      falsePositivePrecedent:
        'This signature has been marked a false positive before, so the score is pulled down.',
      muted:
        'Somebody muted this signature. It is scored, and heavily penalised for being muted.',
    }),
  },


  telemetry: {
    /** The tail bucket, named once: it is a cell key, a URL value and a heading, and spelling it
     *  out in three places is how the three stop agreeing. */
    otherSignatures: 'other signatures',
    unknownError: 'unknown error',
    unknownService: 'unknown service',

    signals: {
      title: 'Signals',
      intro: 'Everything the gate looked at, including what it did not raise.',
      loadError: 'Could not load signals',

      allSignals: 'All signals',
      shown: (count: number) => `${count} shown`,
      inWindow: (total: number) => `${total} in this window`,
      loaded: (loaded: number, total: number) => `${loaded} of ${total} loaded`,
      chips:
        'the chips are the gate’s score components, and the confidence is what they add up to',
      clearFilter: 'Clear filter',

      emptyTileTitle: 'No signals for this tile.',
      // Two causes, and from here they are indistinguishable: the link may carry a tile from
      // another window, or the signatures may have been re-ranked since.
      emptyTile:
        'Nothing in the current window matches this tile. The link may have been made against a different window, or the signatures may have been re-ranked since.',
      emptyWindowTitle: 'No signals in this window.',
      emptyWindow:
        'Nothing has crossed a detection rule yet. A quiet window and a source that is not being read look the same from here — Settings › Telemetry says which.',

      loadMore: (count: number) => `Load ${count} more`,
      notLoaded: (count: number) =>
        `${count} older ${plural('en', count, { one: 'signal', other: 'signals' })} in this window ${plural('en', count, { one: 'is', other: 'are' })} not loaded, so the map above does not count them.`,
      ceiling: (max: number, total: number, remaining: number) =>
        `${max} is as much as this endpoint will hand over at once, and this window holds ${total}. A shorter window is the way to see the rest — the remaining ${remaining} are older than everything above.`,

      occurrences: (count: number) =>
        `${count} ${plural('en', count, { one: 'occurrence', other: 'occurrences' })}`,
      viewIncident: 'View incident',
    },

    heatmap: {
      title: 'Where the errors are',
      description:
        'One tile per error signature. Size and colour are both how often it fired — biggest and reddest top-left.',
      // Said on the map rather than only on the list below it. A treemap that claims to show
      // where the errors are while covering a third of the window is a quiet lie.
      partial: (loaded: number, total: number) =>
        `Built from the ${loaded} most recent of ${total} signals in this window — Load more below widens it.`,
      empty: 'No signals in this window. Nothing has crossed a detection rule yet.',

      allServices: 'All services',
      counts: (services: number, tiles: number, occurrences: number) =>
        `${services} ${plural('en', services, { one: 'service', other: 'services' })} · ${tiles} ${plural('en', tiles, { one: 'tile', other: 'tiles' })} · ${occurrences} ${plural('en', occurrences, { one: 'occurrence', other: 'occurrences' })}`,

      /** What the gate did with the cell's signals, as a phrase rather than the enum name. */
      band: byKey<SignalStatus>({
        Promoted: 'opened an incident',
        Deduplicated: 'counted into an open incident',
        Weak: 'weak — shown, not raised',
        Recorded: 'recorded only',
        Suppressed: 'suppressed (muted signature)',
      }),

      // Colour is never the only channel, and neither is an animation: a mark that can only be
      // seen by watching is a mark an operator who looked away has missed.
      tile: (
        service: string,
        label: string,
        occurrences: number,
        signals: number,
        band: string,
      ) =>
        `${service} · ${label} — ${occurrences} ${plural('en', occurrences, { one: 'occurrence', other: 'occurrences' })} across ${signals} ${plural('en', signals, { one: 'signal', other: 'signals' })} — ${band}`,
      justUpdated: ' — updated just now',

      // The number is styled, so it arrives as a slot rather than as a prefix the component
      // prints ahead of a bare unit. Turkish happens to front the figure too, which is exactly
      // why the old shape looked correct while owning a rule it had no business owning.
      panelCounts: '{occurrences} occurrences · {signals} signals',
      incidentLink: 'Incident →',

      legendOccurrences: 'occurrences',
      noMark: 'no mark — recorded only',
      legendFlash: 'updated in the last few seconds',
      unplaced: (count: number) =>
        `${count} ${plural('en', count, { one: 'signal', other: 'signals' })} could not be placed — their signature is gone.`,
    },

    evidence: {
      title: 'Evidence',
      loadError: 'Could not load evidence',

      filterService: 'Filter by service',
      clearService: 'Clear the service filter',

      logRecords: 'Log records',
      // The endpoint caps at 200 but reports the true total, so a truncated view is shown as
      // truncated rather than quietly lying about the volume.
      showingRecent: (shown: number, total: number) =>
        `Showing the most recent ${shown} of ${total}`,
      inWindow: (total: number) => `${total} in this window`,
      logListLabel: 'Log records in this window',
      emptyLogTitle: 'Nothing logged in this window.',
      emptyLogForService: (service: string) =>
        `No record from "${service}" in the window. Either it was quiet, or nothing by that name is being read — the name has to match what the source reports.`,
      emptyLog:
        'A quiet window and a source that is not being read look the same from here — Settings › Telemetry says which.',

      signatures: 'Signatures',
      signaturesCount: (count: number) =>
        `${count} distinct ${plural('en', count, { one: 'error', other: 'errors' })} behind the signals below.`,
      signaturesCountTruncated: (count: number) =>
        `${count} distinct ${plural('en', count, { one: 'error', other: 'errors' })} behind the signals shown — not behind the whole window.`,
      signaturesAllTime: 'The counters on each span all time, not this window.',
      signaturesListLabel: 'Signatures behind the signals in this window',
      emptySignaturesTitle: 'No signatures here.',
      emptySignatures:
        'A signature is created the first time an error is normalised, so an empty list means nothing in the window was an error.',

      signals: 'Signals',
      signalsDescription: 'What the gate made of those signatures',
      signalsRecent: (shown: number, total: number) => `the most recent ${shown} of ${total}`,
      signalsListLabel: 'Signals in this window',
      emptySignalsTitle: 'No signals here.',
      emptySignals:
        'Errors were logged but no burst cleared a detection rule, so the gate had nothing to decide.',

      arrived: (records: number) =>
        `${records} new log ${plural('en', records, { one: 'record', other: 'records' })} ingested since this window was read.`,
      // A whole second sentence rather than a clause appended after the full stop. Turkish puts
      // the adverbial before the verb, and it could not move a fragment the component had
      // already punctuated.
      arrivedAcrossPolls: (records: number, polls: number) =>
        `${records} new log ${plural('en', records, { one: 'record', other: 'records' })} ingested across ${polls} polls since this window was read.`,
      // The tick counts records, not records matching a filter: the summary is per source, and
      // the service a record belongs to is not in it.
      allServicesNote: ' Counted across all services, not just the one filtered here.',
      reread: 'Re-read the window',
      rereading: 'Re-reading…',

      clockSkew: 'clock skew',
      // A burst is stored as a few sample rows, the newest carrying the rest of the count.
      folded: (count: number) => `×${count}`,
      foldedTitle: (count: number) =>
        `This row stands for ${count} ${plural('en', count, { one: 'event', other: 'events' })} of one burst. The rest of the burst was counted onto it rather than stored.`,
      ingestionLag: 'Ingestion lag — from the source’s timestamp to ours',
      /** The column has no visible heading, so the affix is the whole of what it says. */
      lag: (duration: string) => `+${duration}`,
      muted: 'muted',
      signatureCounts: (
        total: number,
        promotions: number,
        real: number,
        falsePositive: number,
      ) => `${total} total · promoted ${promotions}× · ${real} real, ${falsePositive} false`,
      signalCounts: (occurrences: number, when: string) =>
        `${occurrences} ${plural('en', occurrences, { one: 'occurrence', other: 'occurrences' })} · ${when}`,
    },

    funnel: {
      title: 'Signal funnel',
      loadError: 'Could not load the funnel',

      notRaised: 'Not raised',
      notRaisedDescription: (scope: string) => `${scope}.`,
      // The denominator travels with the numerator. It is the whole defence against a zero here
      // being read as an empty window, so it cannot be somewhere the eye can skip.
      ofScored: (signals: number) =>
        `of ${signals} ${plural('en', signals, { one: 'signal', other: 'signals' })} the gate scored`,
      heldBack: 'held back',
      actedOn: 'acted on',

      zeroHeldBack:
        'Everything the gate scored in this window, it acted on — all {count} crossed the threshold, so there was nothing left to hold back. {emphasis} What it looked at is the number beside it, and the stages below.',
      zeroEmphasis: 'A zero here means the gate refused nothing, not that it looked at nothing.',
      someHeldBack: (notRaised: string, signals: string) =>
        `${notRaised} of ${signals} were scored and left where they were: no incident, no page, no email. That is the thing an alerting rule cannot do — decide, on arithmetic you can read back, that this one was not worth a human.`,
      // The exclusion is deliberate on the server and is worth showing rather than hiding: it is
      // the difference between restraint and a claim of restraint.
      deduplicated: (count: number) =>
        `${count} deduplicated ${plural('en', count, { one: 'signal', other: 'signals' })} count as acted on, not as held back. Each was folded into an incident that was already open, so somebody was woken — just earlier.`,

      nothingScored: 'Nothing was scored in this window.',
      nothingScoredRecords: (records: string, count: number) =>
        `${records} log ${plural('en', count, { one: 'record', other: 'records' })} arrived and none of them crossed a detection rule, so no burst ever reached the score. The filtering here happened a stage earlier than this number measures — the stages below are where to read it.`,
      nothingArrived:
        'No telemetry arrived in this window at all, so the gate had nothing to look at. A quiet window and a source that is not being read look the same from here — Settings › Telemetry says which.',

      stagesTitle: 'From log records to signals',
      stagesDescription: (scope: string) =>
        `On one scale — ${scope}. They count different things: records are lines of log, signatures are distinct fingerprints cut from them, and signals are bursts the gate was asked to score.`,
      stageLogRecords: 'Log records',
      stageSignatures: 'Signatures',
      stageSignals: 'Signals',
      stagesChartLabel: (records: string, signatures: string, signals: string) =>
        `Pipeline stages. ${records} log records, ${signatures} signatures, ${signals} signals.`,

      nothingToFingerprint: 'Nothing arrived, so there was nothing to fingerprint.',
      nothingFingerprinted:
        'Nothing in this window was fingerprinted, which should not happen — the records arrived without one.',
      // The drop fingerprinting bought, stated as a ratio rather than left to be inferred from a
      // bar that is two pixels wide.
      folding: (perSignature: string, signatures: string, records: string, count: number) =>
        `About ${perSignature} records per signature. That fold is what fingerprinting bought: the gate reasons about ${signatures} ${plural('en', count, { one: 'thing', other: 'things' })}, not ${records}.`,

      neverScored: 'No burst crossed a detection rule, so the gate was never asked to score anything.',
      bursting: (signatures: string, signatureCount: number, signals: string, signalCount: number) =>
        `${signatures} ${plural('en', signatureCount, { one: 'signature', other: 'signatures' })} produced ${signals} ${plural('en', signalCount, { one: 'burst', other: 'bursts' })} for the gate to score.`,
      /** The one stage that can widen, which a funnel drawn without saying so would misreport. */
      burstingWider:
        ' A signature can fire more than once, which is why this stage is wider than the one above it rather than narrower.',

      whatArrived: 'What arrived',
      noLogRecord: 'No log record arrived in this window.',

      verdictsTitle: 'How the gate ruled',
      verdictsDescription: (scope: string) =>
        `Every verdict the gate can reach, and how many landed on each — ${scope}.`,
      noVerdicts: 'The gate scored nothing in this window, so it reached none of these.',

      wokenHeading: 'Somebody was woken',
      wokenNote: 'Not counted as held back.',
      notWokenHeading: 'Nobody was woken',
      notWokenNote: 'These three are what "not raised" counts.',

      /**
       * What each verdict means, in the operator's language rather than the enum's. Says what the
       * gate *did*, not how it scored: the weights live in SignalScoring and a number copied into
       * the frontend is a number that goes stale without anyone noticing.
       */
      verdict: byKey<SignalStatus>({
        Promoted: 'Cleared the threshold, and the gate opened an incident for it.',
        Deduplicated:
          'Folded into an incident that was already open. Somebody was woken — earlier.',
        Weak:
          'Scored, and scored under the threshold. Kept where you can see it; nobody was called.',
        Recorded: 'Kept for the record and nothing more.',
        Suppressed:
          'The signature is muted, so the gate scored it and then silenced it on purpose.',
      }),
    },

    services: {
      title: 'Service health',
      // The count only once there is one: "0 services produced something" is a sentence nobody
      // writes, and the empty row says the same thing properly.
      produced: (count: string, raw: number) =>
        `${count} ${plural('en', raw, { one: 'service', other: 'services' })} produced something in this window.`,
      loadError: 'Could not load service health',

      caption: (scope: string) =>
        `Log volume, signals and incidents per service — ${scope}. Sortable by every column except the top signature.`,

      columnService: 'Service',
      columnLogRecords: 'Log records',
      columnSignals: 'Signals',
      columnPromoted: 'Promoted',
      columnIncidents: 'Incidents',
      columnTopSignature: 'Top signature',
      columnLastSignal: 'Last signal',

      emptyTitle: 'Nothing arrived in this window.',
      empty:
        'No service wrote a log record and nothing crossed a detection rule. A genuinely quiet window and a telemetry source that is not being read look the same from here — Settings › Telemetry says which.',

      goneHintLabel: 'What (signature gone) means',
      goneHint:
        'These signals were detected, but the error signature behind them has since been deleted — and the service name lived on the signature. The counts are real; the name they belong to is not recoverable.',

      folded: (records: string, promoted: string, incidents: string, signature: string) =>
        `${records} records · ${promoted} promoted · ${incidents} incidents · ${signature}`,
      // Takes the count and embeds it, like `telemetry.signals.occurrences` — the two had the
      // same name and opposite contracts, which is how the same number ended up printed with a
      // unit in the table and without one on the folded line.
      occurrences: (count: string, raw: number) =>
        `${count} ${plural('en', raw, { one: 'occurrence', other: 'occurrences' })}`,
      topSignature: (signature: string, occurrences: string) => `${signature} · ${occurrences}`,
      noSignal: 'No signal from this service in this window',

      signatureGone: 'signature no longer on record',
      nothingCrossed: 'nothing crossed a detection rule in this window',

      footnote:
        'Counted from the signal and signature side. {incidents} is the number of distinct incidents this service’s signals reached, so an incident somebody opened by hand is attributed to no service — an incident record does not carry one, and reading it out of the title would be a guess.',
    },
  },


  dashboard: {
    title: 'Dashboard',
    days: (count: number) => `${count} days`,
    loadError: 'Could not load the figures',

    open: {
      title: 'Open right now',
      description:
        'Every incident still Open or In progress, however old — which is why this count ignores the window.',
      // On the card, not in a tooltip. Which numbers move with the picker is the kind of thing a
      // reader has to be able to check at a glance rather than by hovering.
      notWindowed: 'not windowed',
      open: 'open',
    },

    byDate: {
      title: 'Incidents by date',
      description:
        'Stacked by priority — {scope}. Days are cut in {utc} on the server, not in your zone, and the last column is today, still filling.',
    },

    detection: {
      title: 'Detection',
      description: (scope: string) => `${scope}, UTC.`,
      empty: 'No incidents in this window, so there is nothing to have noticed. Try a longer window.',
      share: 'noticed by the platform itself',
      noticed: 'Noticed',
      filed: 'Filed by hand',
      median: 'Median latency',
      p95: '95th percentile',
      // An em dash with no explanation invites the reader to supply one, and the one they supply
      // is "zero". These are opposite facts, so the reason is spelled out.
      nothingNoticed:
        'Nothing in this window was noticed automatically, so there is no latency to measure. That is not a latency of zero — it is the absence of one.',
      allSkewed:
        'Every detection in this window came back with the record filed before the problem started, which is two clocks disagreeing rather than a latency. Those rows sit out of the percentiles.',
    },

    sources: {
      title: 'Where incidents come from',
      description: (scope: string) => `${scope}, UTC.`,
      empty: 'No incidents in this window, from any source.',
      // Fixed order, most autonomous first, because the order is the point being made.
      meaning: byKey<IncidentSource>({
        Telemetry: 'the platform found it in your log store',
        Alert: 'an external alert raised it',
        Manual: 'somebody opened it by hand',
      }),
    },

    latest: {
      title: 'Latest incidents',
      description: (rows: number) => `The ${rows} most recent, whatever the window. Updates live.`,
      loadError: 'Could not load incidents',
      emptyTitle: 'No incidents on record.',
      empty: 'Nothing has been opened by hand, and nothing has crossed a detection rule yet.',
      all: (total: number) => `All ${total} incidents`,
    },

    chart: {
      summary: (total: number, days: number) =>
        `${total} opened across ${days} ${plural('en', days, { one: 'day', other: 'days' })}`,
      busiest: (count: number) => ` · busiest day ${count}`,
      hint: ' · hover or focus the chart for one day',
      dayTotal: (total: number) =>
        `${total} ${plural('en', total, { one: 'incident', other: 'incidents' })}`,
      ariaLabel: (days: number) =>
        `Incidents opened per UTC day across ${days} ${plural('en', days, { one: 'day', other: 'days' })}. Use the arrow keys to read one day at a time.`,
      // A drawn-but-empty grid reads as a chart that failed rather than as a quiet month.
      emptyPlot: 'Nothing opened in this window. Every day in it is empty.',
      // The whole readout, separators included. It is `aria-live`, so the order the component
      // was imposing was also the order it was spoken in.
      dayReadout: '{day} · {total} — {breakdown}',
      dayReadoutEmpty: '{day} · {total}',
      priorityCount: (count: number, priority: string) => `${count} ${priority}`,
      caption: 'Incidents opened per UTC day, by priority.',
      columnDay: 'Day (UTC)',
      columnTotal: 'Total',
      legendNote: 'Critical at the base of each bar',
    },
  },


  deliveries: {
    title: 'Delivery health',
    intro: 'Across the whole window, not one incident at a time.',
    loadError: 'Could not load delivery health',

    /** The name a deleted integration has left. Its deliveries are kept on purpose. */
    deleted: 'Deleted integration',

    totals: {
      title: 'Deliveries',
      description: (scope: string) => `${scope}.`,
      emptyLead: 'Nothing was sent in this window. ',
      empty:
        'Notifications go out when an analysis finishes, so a window with no incidents in it and a dispatcher that has stopped look identical from here. The incidents screen says which of the two this is.',

      // Same shape as the funnel's headline, and for the same reason: the denominator is on the
      // line, so a zero cannot be read as "nothing was attempted".
      failedOf: (total: string, totalCount: number, channels: string, channelCount: number) =>
        `of ${total} ${plural('en', totalCount, { one: 'delivery', other: 'deliveries' })} failed, across ${channels} ${plural('en', channelCount, { one: 'integration', other: 'integrations' })}`,

      failed: 'failed',
      sent: 'sent',
      pending: 'pending',

      someFailing: (failing: string, count: number) =>
        `${failing} ${plural('en', count, { one: 'integration', other: 'integrations' })} recorded a failure in this window. The rows below say which, when, and what the channel said back.`,
      allThrough: 'Every delivery in this window got through. ',
      stillQueued: (pending: string, count: number) =>
        count === 1
          ? `${pending} delivery is still queued and has not been attempted yet — queued is not sent.`
          : `${pending} deliveries are still queued and have not been attempted yet — queued is not sent.`,
      nothingQueued: 'Nothing is queued and nothing is outstanding.',
    },

    dispatch: {
      title: 'Dispatch time',
      description: (scope: string) =>
        `Median, ${scope} — measured from the delivery being written to the channel acknowledging it.`,
      empty: 'Nothing was dispatched in this window, so there is nothing to have timed.',
      // One sentence with the muted half as a slot. Split across two entries, the connector
      // straddled the seam and the order was the component's.
      noneSucceeded:
        '{lead} so there is no dispatch time to draw. That is not a dispatch time of zero — it is the absence of one.',
      noneSucceededLead: 'Nothing succeeded in this window,',
      chartLabel: (scope: string, detail: string) =>
        `Median dispatch time per integration, ${scope}. ${detail}.`,
      /** One row of that label. It is screen-reader text and was being built outside the
       *  dictionary, where `check:i18n` cannot see it. */
      chartRow: (name: string, value: string) => `${name} ${value}`,
      dashNote:
        'An em dash is an integration that had nothing succeed in this window, so it has no median. It is not a dispatch time of zero.',
    },

    /**
     * What a channel is doing, as a word.
     *
     * A failure count alone is not a verdict: an integration that failed twice on Monday and has
     * delivered forty times since is working, and calling it "failing" all week teaches the
     * operator to ignore the word.
     */
    verdict: {
      failing: 'Failing',
      recovered: 'Recovered',
      delivering: 'Delivering',
      queued: 'Queued',
      silent: 'Nothing sent',
    },

    byIntegration: {
      title: 'By integration',
      description: (scope: string) => `Worst first — ${scope}.`,
      emptyTitle: 'No integration attempted a delivery.',
      empty:
        'An integration only appears here once it has something to report. One configured and enabled but never reached in this window is not on this list — Settings › Integrations is the roll of what exists.',

      // Only for an integration that still exists. A deleted one reads as disabled through the
      // same field, and saying "disabled" about something that is gone is two wrong words.
      disabled: 'Paused',
      deletedNote:
        'This integration has been deleted. Its deliveries are kept on purpose — they are the record that somebody was told — so the counts below are still true, and the name and channel they belonged to are gone.',
      id: (short: string) => `id ${short}`,
      lastFailure: 'Last failure',

      sent: 'Sent',
      failed: 'Failed',
      pending: 'Pending',
      median: 'Median dispatch',
      lastSent: 'Last sent',
    },
  },


  settings: {
    /** The vocabulary the two catalogue screens share — they are one pattern shown twice. */
    shared: {
      availableNow: 'Available now',
      comingSoon: 'Coming soon',
      counts: (total: number, enabled: number) => `${total} connected · ${enabled} active`,
      connected: ' connected',
      notConnected: 'Not connected.',
      test: 'Test',
      testing: 'Testing…',
      edit: 'Edit',
      cancel: 'Cancel',
      deleting: 'Deleting…',
      paused: 'Paused',
      name: 'Name',
      dismissTest: 'Dismiss test result',
      testReport: '{status} at {time} — {detail}',
      secretKept: 'A secret left blank keeps the value already stored.',
      saving: 'Saving…',
      saveChanges: 'Save changes',
      connect: 'Connect',
      enabledSwitch: (name: string) => `${name} enabled`,
      deleteAria: (name: string) => `Delete ${name}`,
      deleteTitle: (name: string) => `Delete “${name}”?`,
    },

    config: {
      keepCurrent: 'Leave blank to keep the current value',
      // A stored key the schema does not declare. Appending it as a real field is what stops the
      // form deleting a credential nobody can retype.
      strayIntegration: 'Stored on this integration; this form does not know its shape.',
      straySource: 'Stored on this source; this form does not know its shape.',

      /**
       * One entry per declared config field. Keyed by a stable id rather than by the config key,
       * because the same key means different things in different connectors — `Url` is the webhook
       * endpoint in one place and the Seq instance in another.
       */
      fields: byKey<ConfigFieldId>({
        'email.host': 'SMTP host',
        'email.port': 'Port',
        'email.from': 'From',
        'email.to': 'To',
        'email.username': 'Username',
        'email.password': 'Password',
        'webhook.url': 'URL',
        'webhook.timeout': 'Timeout (seconds)',
        'webhook.authorization': 'Authorization header',
        'jira.baseUrl': 'Base URL',
        'jira.projectKey': 'Project key',
        'jira.email': 'Account email',
        'jira.apiToken': 'API token',
        'jira.issueType': 'Issue type',
        'seq.url': 'Seq URL',
        'seq.apiKey': 'API key',
        'seq.filter': 'Filter',
        'seq.serviceProperty': 'Service property',
        'seq.initialLookback': 'Initial lookback (minutes)',
        'otlp.minimumSeverity': 'Minimum severity',
      }),

      hints: {
        'webhook.authorization': 'Any setting prefixed Header: is sent as a request header.',
        'seq.apiKey':
          'Only needed when the Seq instance has authentication enabled. The key needs Read permission.',
        'seq.filter': 'Seq filter expression. Left blank, the connector reads errors and fatals.',
        'seq.serviceProperty': 'Which event property names the service a log line came from.',
        'seq.initialLookback':
          'How far back the first poll reads. Later polls resume from where the last one stopped.',
        'otlp.minimumSeverity':
          'Warning or Error. Left blank, only errors and fatals are kept; anything lower is dropped on arrival.',
      } as Partial<Record<ConfigFieldId, string>>,
    },

    integrations: {
      title: 'Integrations',
      intro:
        'A notification goes out when an analysis completes. One channel can hold several integrations — two Email entries with different filters is a normal setup.',
      loadError: 'Could not load integrations.',

      // Nothing connected and everything paused are different configurations with the same
      // consequence: an analysis finishes and no one is told.
      silenceNone: 'Nothing is connected. When an analysis completes, no one is told.',
      silenceOne: 'The only integration is paused. When an analysis completes, no one is told.',
      silenceMany: (total: number) =>
        `All ${total} integrations are paused. When an analysis completes, no one is told.`,

      comingSoonNote:
        'Until these land, a custom endpoint is reachable through Webhook, which posts this platform’s own JSON rather than any vendor’s payload format.',

      addAnother: (name: string) => `Add another ${name}`,
      connectOne: (name: string) => `Connect ${name}`,

      summary: byKey<NotificationChannelType>({
        Email: 'SMTP to a mailbox or a distribution list.',
        Webhook: 'An HTTP POST of the incident and its analysis to an endpoint you control.',
        Jira: 'Opens an issue in a project, with the reasoning in the description.',
      }),

      planned: byKey<PlannedIntegrationId>({
        slack: 'Post to a channel.',
        teams: 'Post to a team channel.',
        pagerduty: 'Page whoever is on call.',
        discord: 'Post to a channel.',
      }),

      deleted: 'Integration deleted',
      updated: 'Integration updated',
      connected: (channel: string) => `${channel} connected`,

      testOk: 'The channel accepted a test notification.',
      testRejected: 'The channel rejected the test.',
      testOkLabel: 'Test sent',
      testFailLabel: 'Test failed',

      deleteBody: (channel: string) =>
        `This ${channel} integration and the credentials stored with it are removed. Delivery history for incidents already sent is kept.`,
      deleteConfirm: 'Delete integration',

      editTitle: (channel: string) => `Edit ${channel} integration`,
      connectTitle: (channel: string) => `Connect ${channel}`,
      namePlaceholder: (channel: string) => `${channel} — on-call`,
      // One channel can hold several integrations, so the name is what tells them apart.
      nameHint: 'How this integration is identified on the integrations page.',
      minPriority: 'Minimum priority',
      anyPriority: 'Any priority',
      andAbove: (priority: string) => `${priority} and above`,
      categoryFilter: 'Category filter',
      categoryPlaceholder: 'Any category',

      // An integration with no filters matches every incident. Left as two blank form controls
      // that is indistinguishable from an unfinished setup, so the rule is written out.
      sends: (parts: string) => `Sends ${parts}.`,
      sendsEverything: 'Sends every incident — no filters set.',
      category: (value: string) => `category ${value}`,
      // Two whole sentences rather than one that splices a finished sentence into itself. The
      // spliced version read "Sends every incident — no filters set when resumed", which binds
      // the condition to the wrong clause.
      pausedNote: (parts: string) =>
        `— nothing is sent here. When resumed it will send ${parts}.`,
      pausedNoteEverything: '— nothing is sent here. When resumed it will send every incident.',
    },

    telemetry: {
      title: 'Telemetry',
      intro:
        'Detection never watches this platform itself — nothing reaches it that you have not connected here.',
      loadError: 'Could not load sources.',

      // The most consequential configuration gap in the product: with no source being read,
      // detection has no input, so the incident list stays empty and reads as "quiet" rather
      // than as "deaf".
      blindnessNone:
        'No source is connected. Nothing is being read, so nothing will ever be detected.',
      blindnessOne: 'The only source is paused. Nothing is being read, so nothing will be detected.',
      blindnessMany: (total: number) =>
        `All ${total} sources are paused. Nothing is being read, so nothing will be detected.`,

      comingSoonNote:
        'Not built yet. For logs, OTLP above is the general answer: one standard wire format, and everything else handled by the shipper you already run.',

      addAnother: (name: string) => `Add another ${name} source`,
      connectOne: (name: string) => `Connect ${name}`,

      summary: byKey<TelemetrySourceKind>({
        Seq: 'Pulls from a Seq instance’s query API on a schedule.',
        Otlp: 'Your collector or SDK pushes logs in OpenTelemetry’s wire format — whatever log store you run.',
      }),

      planned: byKeyOf<PlannedSourceId, { name: string; summary: string }>({
        alerts: {
          name: 'Alert webhook ingest',
          summary: 'Alerts from your monitoring, not logs.',
        },
      }),

      deleted: 'Source deleted',
      updated: 'Source updated',
      connected: (kind: string) => `${kind} connected`,

      // Connecting and matching are different successes. A source that answers but shows nothing
      // is a filter problem or a quiet window.
      probeAnswered: 'The source answered the probe.',
      probeNoMatch:
        'The source answered, but nothing matched the filter in the window probed. Either the window is quiet or the filter is too narrow.',
      probeMatched: (count: number) =>
        `The source answered with ${count} matching ${plural('en', count, { one: 'event', other: 'events' })} visible.`,
      probeRejected: 'The source rejected the probe.',
      testOkLabel: 'Probe succeeded',
      testFailLabel: 'Probe failed',

      deleteBody: (kind: string) =>
        `This ${kind} source and the credentials stored with it are removed, and detection stops reading from it immediately. Logs and signatures already ingested are kept — they are the evidence behind incidents already opened. A source connected here again starts from its initial lookback window rather than from where this one stopped.`,
      // A pushed source is not read, so deleting it does not stop reading: it stops accepting.
      deleteBodyPushed: (kind: string) =>
        `This ${kind} source is removed and its key stops working immediately — anything still sending with it gets 401. Logs and signatures already received are kept; they are the evidence behind incidents already opened.`,
      deleteConfirm: 'Delete source',

      editTitle: (kind: string) => `Edit ${kind} source`,
      connectTitle: (kind: string) => `Connect ${kind}`,
      namePlaceholder: (kind: string) => `${kind} — production`,
      nameHint: 'How this source is identified on the telemetry page and in detection logs.',
      pollLabel: 'Poll interval (seconds)',
      pollInvalid: (minimum: number) =>
        `Enter a whole number of seconds, ${minimum} or more. Polling faster than that hammers the source for no benefit.`,

      // A pushed source has no schedule; what it does is receive, and keep what clears its floor.
      pushedSchedule: (minimum: string) => `Receives pushed logs and keeps ${minimum} and above.`,
      pushedPausedNote:
        '— pushes are refused with 403 until it is resumed, and a sender does not retry a 403.',
      endpointLabel: 'Endpoint',
      keyLabel: 'Key',
      rotateKey: 'New key',
      rotatingKey: 'Issuing…',
      rotated: 'New key issued. The old one stopped working just now.',
      lastReceived: (when: string) => `The last batch arrived ${when}.`,
      // Follows the "Nothing received" label, so it says what to do rather than repeating it.
      nothingReceived: 'Point a collector or an SDK at the endpoint, with this source’s key.',
      receivedOkLabel: 'Receiving',
      receivedFailLabel: 'Nothing received',

      // Shown once, straight after the key is issued. Everything a person needs to point a sender
      // at this source is on this one screen, because the key cannot be shown a second time.
      keyPanel: {
        title: (name: string) => `Send logs to ${name}`,
        once: 'This key is shown once. Only its hash is stored, so it cannot be shown again — if it is lost, issue a new one.',
        keyLabel: 'Ingest key',
        endpointLabel: 'OTLP/HTTP endpoint',
        endpointHint:
          'Protobuf or JSON, gzip welcome. Exporters append /v1/logs to this address themselves.',
        headerHint: (header: string) => `Send the key in the ${header} header.`,
        collectorLabel: 'OpenTelemetry Collector',
        collectorHint:
          'Filters to errors before sending. Filtering belongs at the source; this source also drops anything below its minimum severity on arrival.',
        sdkLabel: 'Straight from an SDK',
        sdkHint: 'Environment variables every OpenTelemetry SDK reads, no collector in between.',
        done: 'Done',
      },

      // A source with no filter still reads something specific, and the pair of blank form
      // controls that produced it does not say what.
      polls: (seconds: number, what: string) => `Polls every ${seconds}s for ${what}.`,
      matching: (filter: string) => `events matching ${filter}`,
      defaultFilter: 'errors and fatals',
      pausedNote: (seconds: number, what: string) =>
        `— nothing is read from here. When resumed it polls every ${seconds}s for ${what}.`,
    },

    members: {
      title: 'Members',
      description:
        'Who can sign in to this organisation, and what each of them may do. Accounts are opened by invitation only.',
      invite: 'Invite',
      loadError: 'Could not load members',
      you: 'You',
      deactivatedBadge: 'Deactivated',
      roleOf: (name: string) => `Role of ${name}`,
      actionsFor: (name: string) => `Actions for ${name}`,
      issueReset: 'Send a password reset link',
      deactivate: 'Deactivate',
      activate: 'Activate',
      // Why a control on the row is inert, said on the row. The server refuses both anyway.
      selfHint: 'Another Admin changes your role and account. Your password is on your Profile.',
      lastAdminHint:
        'The last active Admin. Make someone else an Admin before changing this account.',
      roleChanged: (name: string, role: string) => `${name} is now ${role}.`,
      activated: (name: string) => `${name} can sign in again.`,
      deactivated: (name: string) => `${name} can no longer sign in.`,

      deactivateDialog: {
        title: (name: string) => `Deactivate ${name}?`,
        // The fifteen minutes is the access token's lifetime: it cannot be withdrawn, only left to
        // expire, and saying so is kinder than a promise the platform does not keep.
        body: 'They can no longer sign in, and their sessions end — a page they already have open stops working within fifteen minutes. What they did stays on the record. You can activate the account again later.',
        cancel: 'Cancel',
        confirm: 'Deactivate',
      },

      pending: {
        title: 'Pending invitations',
        description:
          'Each link works once and expires on its own. Inviting the same address again replaces the earlier link.',
        empty: 'No invitations are waiting.',
        expires: (when: string) => `expires ${when}`,
        revoke: 'Revoke',
        revokeAria: (email: string) => `Revoke the invitation to ${email}`,
        revoked: (email: string) => `The invitation to ${email} no longer works.`,
      },

      inviteDialog: {
        title: 'Invite a member',
        description:
          'They receive an email with a link to choose their name and password. The account belongs to this organisation.',
        email: 'Email address',
        emailRequired: 'Enter an email address.',
        emailInvalid: 'That does not look like an email address.',
        hasAccount: 'This address already has an account, so it cannot be invited.',
        role: 'Role',
        cancel: 'Cancel',
        submit: 'Send invitation',
        submitting: 'Sending…',
      },

      link: {
        invitationTitle: (email: string) => `Invitation for ${email}`,
        resetTitle: (name: string) => `Password reset for ${name}`,
        once: 'This link is shown once. Only its hash is stored, so it cannot be shown again — if it is lost, issue a new one.',
        label: 'Link',
        expires: (when: string) => `Works once, until ${when}.`,
        emailed: (email: string) => `Also emailed to ${email}.`,
        notEmailed: (email: string) =>
          `The email to ${email} could not be sent. The link works — pass it on yourself.`,
        done: 'Done',
      },
    },
  },

  // ---- the enum vocabulary ---------------------------------------------------------------
  //
  // Display only. The wire value never changes: a filter still sends `InProgress`, and the badge
  // beside it still reads "In progress". One source, so a badge and a filter cannot drift apart.
  labels: {
    role: byKey<UserRole>({
      Admin: 'Admin',
      Engineer: 'Engineer',
      Viewer: 'Viewer',
    }),

    // What the role allows, in the words the server enforces it: Administer is Admin only, Operate
    // is Admin and Engineer, and everything else reads.
    roleDetail: byKey<UserRole>({
      Admin: 'Everything, including the organisation’s members, integrations and telemetry sources.',
      Engineer: 'Works incidents — status and assignment. Does not see the organisation’s settings.',
      Viewer: 'Reads incidents, signals, evidence and dashboards, and changes nothing.',
    }),

    incidentStatus: byKey<IncidentStatus>({
      Open: 'Open',
      InProgress: 'In progress',
      Resolved: 'Resolved',
      Closed: 'Closed',
    }),

    verdict: byKey<IncidentVerdict>({
      Real: 'Real problem',
      FalsePositive: 'False positive',
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

    /** The server's own name for signals whose signature has since been deleted. A sentinel
     *  rather than prose, so it is translated the way an enum is — the literal stays the key. */
    goneService: '(signature gone)',

    telemetryKind: byKey<TelemetrySourceKind>({
      Seq: 'Seq',
      Otlp: 'OTLP',
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

    /**
     * The same two, capitalised, for a card description that is now *only* the scope.
     *
     * Written out rather than capitalised in code, for the reason `scope` is written out rather
     * than lower-cased: changing a letter's case in a translated string is a language operation
     * with its own rules, not a display transform, and a component has no business performing
     * one. Four descriptions were reduced to their scope in the Adım 20.7 audit and each was
     * starting a line in lower case.
     */
    scopeCap: byKey<WindowPreset>({
      '30m': 'Last 30 minutes',
      '2h': 'Last 2 hours',
      '24h': 'Last 24 hours',
      '7d': 'Last 7 days',
    }),

    dayScopeCap: (days: number) => `Last ${days} days`,
  },

  format: {
    /**
     * The units a duration is spelled with.
     *
     * Separate entries rather than a suffix table, because a language is free to put the number
     * somewhere else. Found on the incident list, where a row was printing "20 sa önce" and
     * "1m 30s" in adjacent columns — `formatRelative` had been translated and `renderSpan`, in
     * the same file, had not.
     */
    /** English puts the sign after the figure; Turkish puts it before. */
    percent: (value: number) => `${value}%`,

    span: {
      milliseconds: (value: number) => `${value}ms`,
      seconds: (value: string) => `${value}s`,
      minutesSeconds: (minutes: number, seconds: number) => `${minutes}m ${seconds}s`,
      hoursMinutes: (hours: number, minutes: number) => `${hours}h ${minutes}m`,
    },

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
