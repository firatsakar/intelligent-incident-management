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
  logRecords: LogRecord[]
  signatures: ErrorSignature[]
  signals: Signal[]
}

export type TelemetrySourceKind = 'Seq'

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
}

/** The value the API substitutes for anything that looks like a credential. */
export const maskedValue = '***'
