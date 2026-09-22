// Hand-written mirrors of the service DTOs.
//
// Not generated: the four OpenAPI documents are only served while the services are running, so
// codegen would tie `npm run build` to a live backend. The surface is about a dozen records and
// has been stable since Adım 13.
//
// Every enum is serialised as a string by JsonStringEnumConverter, so each one is a string union
// here. Declaration order matters for IncidentPriority — Critical is the most severe.

export type IncidentStatus = 'Open' | 'InProgress' | 'Resolved' | 'Closed'
export type IncidentPriority = 'Critical' | 'High' | 'Medium' | 'Low'
export type IncidentSource = 'Manual' | 'Telemetry' | 'Alert'

export const incidentStatuses: IncidentStatus[] = ['Open', 'InProgress', 'Resolved', 'Closed']
export const incidentPriorities: IncidentPriority[] = ['Critical', 'High', 'Medium', 'Low']

export interface Incident {
  id: string
  title: string
  description: string
  status: IncidentStatus
  priority: IncidentPriority
  source: IncidentSource
  assignedTeam: string | null
  /** When the problem started, as opposed to createdAt, which is when the record was filed. */
  detectedAt: string | null
  aiSuggestedCategory: string | null
  aiReasoning: string | null
  isAiAnalyzed: boolean
  /** Null means the analysis declined to give one — not zero confidence. */
  aiConfidence: number | null
  /**
   * Non-null means the analysis ran and failed. A different state from `isAiAnalyzed: false`,
   * which means it has not run yet — one of those resolves itself and the other does not, and
   * before this field existed they looked identical on screen.
   */
  aiAnalysisError: string | null
  createdAt: string
  updatedAt: string | null
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}

// ---- telemetry -----------------------------------------------------------------------------

export type LogSeverity =
  | 'Verbose'
  | 'Debug'
  | 'Information'
  | 'Warning'
  | 'Error'
  | 'Fatal'

/**
 * Most severe first, matching `incidentPriorities`. The severity rollup on the funnel comes back
 * keyed and sparse — a severity nothing arrived at is absent rather than zero — so a screen that
 * wants a stable reading order has to bring one.
 */
export const logSeverities: LogSeverity[] = [
  'Fatal',
  'Error',
  'Warning',
  'Information',
  'Debug',
  'Verbose',
]

export type SignalKind = 'LogBurst' | 'RateAnomaly'

export type SignalStatus =
  | 'Recorded'
  | 'Weak'
  | 'Promoted'
  | 'Deduplicated'
  | 'Suppressed'

export const signalStatuses: SignalStatus[] = [
  'Weak',
  'Promoted',
  'Recorded',
  'Deduplicated',
  'Suppressed',
]

export interface Signal {
  id: string
  errorSignatureId: string
  kind: SignalKind
  status: SignalStatus
  detectedAt: string
  windowStart: string
  windowEnd: string
  occurrenceCount: number
  confidence: number
  /** Why the gate decided what it decided. Includes a "total" key alongside the components. */
  scoreBreakdown: Record<string, number>
  reason: string | null
  incidentId: string | null
  /** Carried from the signature. Null only if the signature has since gone. */
  service: string | null
  exceptionType: string | null
  normalizedMessage: string | null
}

export interface ErrorSignature {
  id: string
  fingerprint: string
  service: string
  exceptionType: string | null
  normalizedMessage: string
  firstSeenAt: string
  lastSeenAt: string
  occurrenceCount: number
  currentIncidentId: string | null
  isMuted: boolean
  promotionCount: number
  confirmedRealCount: number
  falsePositiveCount: number
}

export interface LogRecord {
  id: string
  service: string
  severity: LogSeverity
  message: string
  exceptionType: string | null
  fingerprint: string | null
  /** The source clock. */
  timestamp: string
  /** Our clock. The gap is ingestion lag. */
  ingestedAt: string
  hasClockSkew: boolean
}

export interface EvidenceWindow {
  from: string
  to: string
  service: string | null
  /** The true total; logRecords is capped at 200, so a truncated view can be shown as truncated. */
  totalLogRecords: number
  /** Signals are capped too now — they were one of two collections here that never had a limit. */
  totalSignals: number
  /** Distinct signatures behind the signals shown, not a separate count of the whole window. */
  totalSignatures: number
  logRecords: LogRecord[]
  signatures: ErrorSignature[]
  signals: Signal[]
}

export type TelemetrySourceKind = 'Seq'

/** The runtime twin of the union, so the settings catalogue can be built from it rather than by
    hand. One member today; the point is that a second one has to pass through here. */
export const telemetrySourceKinds: TelemetrySourceKind[] = ['Seq']

export interface TelemetrySource {
  id: string
  name: string
  kind: TelemetrySourceKind
  isEnabled: boolean
  /** Credential-looking values read back as "***". Never send that value back. */
  config: Record<string, string>
  pollIntervalSeconds: number
  createdAt: string
  updatedAt: string | null
}

// ---- notifications --------------------------------------------------------------------------

export type NotificationChannelType = 'Email' | 'Webhook' | 'Jira'
export type DeliveryStatus = 'Pending' | 'Sent' | 'Failed'

export const notificationChannels: NotificationChannelType[] = ['Email', 'Webhook', 'Jira']

export interface Integration {
  id: string
  name: string
  channel: NotificationChannelType
  isEnabled: boolean
  /** Same masking rule as TelemetrySource.config. */
  config: Record<string, string>
  minPriority: IncidentPriority | null
  categoryFilter: string | null
  createdAt: string
  updatedAt: string | null
}

export interface NotificationDelivery {
  id: string
  integrationId: string
  incidentId: string
  eventId: string
  status: DeliveryStatus
  attemptCount: number
  lastError: string | null
  sentAt: string | null
  createdAt: string
}

export interface TestResult {
  isSuccess: boolean
  error: string | null
  /** Telemetry sources only: how many events the probe could see. A source that connects but
      matches nothing is a different problem from one that cannot connect. */
  matchedEvents?: number | null
}

/** The value the API substitutes for anything that looks like a credential. */
export const maskedValue = '***'

// ---- aggregates -------------------------------------------------------------------------------
//
// Added in Adım 20. Day buckets are UTC on the server, and the screens that draw them say so
// rather than leaving a reader to assume their own zone.

/** Counts keyed by enum name. The server fills every member, including the zeroes. */
export type CountsByKey = Record<string, number>

export interface IncidentDayBucket {
  /** A UTC date, `YYYY-MM-DD`. */
  day: string
  total: number
  byPriority: CountsByKey
}

export interface DetectionLatency {
  /** Carried a detectedAt: the platform saw these itself. */
  noticedCount: number
  /** No detectedAt: somebody filed these. */
  toldCount: number
  /** Null when nothing was noticed automatically — which is not the same as instantly. */
  medianSeconds: number | null
  p95Seconds: number | null
}

export interface IncidentStats {
  from: string
  to: string
  days: IncidentDayBucket[]
  byPriority: CountsByKey
  byStatus: CountsByKey
  bySource: CountsByKey
  /** Everything still unresolved, ignoring the window. */
  openByPriority: CountsByKey
  total: number
  openTotal: number
  detection: DetectionLatency
}

export interface Funnel {
  logRecords: number
  /** Distinct fingerprints behind those records. The drop is what fingerprinting bought. */
  signatures: number
  signals: number
  signalsByStatus: CountsByKey
  /**
   * Signals the gate saw and chose not to raise. Excludes Deduplicated, which was folded into an
   * incident that is already open — somebody was woken, just earlier.
   */
  notRaised: number
  logRecordsBySeverity: CountsByKey
}

export interface ServiceHealth {
  service: string
  logRecords: number
  signals: number
  promoted: number
  incidents: number
  topSignature: string | null
  topSignatureOccurrences: number
  lastSignalAt: string | null
}

export interface TelemetryStats {
  from: string
  to: string
  funnel: Funnel
  services: ServiceHealth[]
}

export interface IntegrationHealth {
  integrationId: string
  /** Null when the integration was deleted. Its deliveries outlive it by design. */
  name: string | null
  channel: string | null
  isEnabled: boolean
  sent: number
  failed: number
  pending: number
  /** Null when nothing succeeded in the window — not zero, which would read as instant. */
  medianDispatchSeconds: number | null
  lastError: string | null
  lastErrorAt: string | null
  lastSentAt: string | null
}

export interface NotificationStats {
  from: string
  to: string
  integrations: IntegrationHealth[]
  totalSent: number
  totalFailed: number
  totalPending: number
}

/** A window of signals with the count it was cut from, so a truncated list can say so. */
export interface SignalPage {
  items: Signal[]
  totalCount: number
}

/**
 * What one poll of one source brought in.
 *
 * Deliberately not a per-row push. A poll writes up to two hundred log records per source, which
 * at the configured floor is roughly forty messages a second fanned to every connected client —
 * so the server sends one summary per cycle instead. A screen does not need the rows to know its
 * window is out of date; it needs the number, and the operator decides whether to re-read.
 */
export interface IngestionTick {
  telemetrySourceId: string
  /** Records actually written, after the cursor's deliberate overlap was filtered out. */
  newRecords: number
  touchedSignatures: number
  /** The newest event in the batch, so a screen can tell whether the arrival is inside its own
   *  window rather than assuming it is. Null when the batch carried no timestamp. */
  latestEventAt: string | null
  completedAt: string
}
