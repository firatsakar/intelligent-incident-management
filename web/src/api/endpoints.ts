import { api } from './client'
import type {
  EvidenceWindow,
  Incident,
  IncidentPriority,
  IncidentStats,
  IncidentStatus,
  Integration,
  NotificationChannelType,
  NotificationDelivery,
  NotificationStats,
  PagedResult,
  SignalPage,
  SignalStatus,
  TelemetrySource,
  TelemetryStats,
  TestResult,
} from '@/types/api'

// One function per endpoint, grouped by the service that owns it. Features call these rather
// than fetch, so a route change is a one-line edit here.

export interface IncidentListParams {
  status?: IncidentStatus
  priority?: IncidentPriority
  pageNumber?: number
  pageSize?: number
}

export const incidentsApi = {
  list: (params: IncidentListParams) =>
    api.get<PagedResult<Incident>>('/api/incidents', { ...params }),

  byId: (id: string) => api.get<Incident>(`/api/incidents/${id}`),

  /**
   * Arrivals by UTC day plus the current open picture. The window defaults to the last
   * thirty days server-side; open counts ignore it entirely.
   */
  stats: (params: { from?: string; to?: string } = {}) =>
    api.get<IncidentStats>('/api/incidents/stats', { ...params }),

  updateStatus: (id: string, newStatus: IncidentStatus) =>
    api.patch<void>(`/api/incidents/${id}/status`, { newStatus }),

  assignTeam: (id: string, team: string) =>
    api.patch<void>(`/api/incidents/${id}/team`, { team }),
}

export const telemetryApi = {
  /**
   * Without a window this is the queue for one status. With one it is every signal detected in
   * the span, across all statuses — which is what the heat map aggregates.
   */
  signals: (params: {
    status?: SignalStatus
    limit?: number
    from?: string
    to?: string
    offset?: number
  }) => api.get<SignalPage>('/api/telemetry/signals', { ...params }),

  evidence: (params: { service?: string; from?: string; to?: string }) =>
    api.get<EvidenceWindow>('/api/telemetry/evidence', { ...params }),

  /** The funnel and the per-service rollup. Window defaults to the last 24 hours server-side. */
  stats: (params: { from?: string; to?: string } = {}) =>
    api.get<TelemetryStats>('/api/telemetry/stats', { ...params }),
}

export interface TelemetrySourceInput {
  name: string
  config: Record<string, string>
  pollIntervalSeconds?: number
}

export const telemetrySourcesApi = {
  list: () => api.get<TelemetrySource[]>('/api/telemetry-sources'),

  create: (input: TelemetrySourceInput & { kind: 'Seq'; isEnabled: boolean }) =>
    api.post<TelemetrySource>('/api/telemetry-sources', input),

  update: (id: string, input: TelemetrySourceInput) =>
    api.put<TelemetrySource>(`/api/telemetry-sources/${id}`, input),

  setEnabled: (id: string, isEnabled: boolean) =>
    api.patch<TelemetrySource>(`/api/telemetry-sources/${id}/enabled`, { isEnabled }),

  remove: (id: string) => api.delete<void>(`/api/telemetry-sources/${id}`),

  /** Returns 502 with a body when the customer's source or credentials are wrong. */
  test: (id: string) => api.post<TestResult>(`/api/telemetry-sources/${id}/test`),
}

export const notificationsApi = {
  byIncident: (incidentId: string) =>
    api.get<NotificationDelivery[]>(`/api/notifications/incident/${incidentId}`),

  /** Delivery health per integration. Window defaults to the last seven days server-side. */
  stats: (params: { from?: string; to?: string } = {}) =>
    api.get<NotificationStats>('/api/notifications/stats', { ...params }),
}

export interface IntegrationInput {
  name: string
  config: Record<string, string>
  minPriority?: IncidentPriority | null
  categoryFilter?: string | null
}

export const integrationsApi = {
  list: () => api.get<Integration[]>('/api/integrations'),

  create: (input: IntegrationInput & { channel: NotificationChannelType; isEnabled: boolean }) =>
    api.post<Integration>('/api/integrations', input),

  update: (id: string, input: IntegrationInput) =>
    api.put<Integration>(`/api/integrations/${id}`, input),

  setEnabled: (id: string, isEnabled: boolean) =>
    api.patch<Integration>(`/api/integrations/${id}/enabled`, { isEnabled }),

  remove: (id: string) => api.delete<void>(`/api/integrations/${id}`),

  test: (id: string) => api.post<TestResult>(`/api/integrations/${id}/test`),
}
