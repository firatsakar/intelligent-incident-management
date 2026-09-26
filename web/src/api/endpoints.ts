import { api } from './client'
import type {
  AiResponseLanguage,
  AiSettings,
  EvidenceWindow,
  GitHubConnection,
  Incident,
  IncidentApiKey,
  IncidentPriority,
  IncidentStats,
  IncidentStatus,
  IncidentVerdict,
  Integration,
  Invitation,
  InvitationIssued,
  InvitationPreview,
  IssuedIncidentApiKey,
  IssuedLink,
  Member,
  NotificationChannelType,
  NotificationDelivery,
  NotificationStats,
  PagedResult,
  PasswordResetPreview,
  RepositoryCheck,
  RepositoryMapping,
  SessionAccount,
  SignalPage,
  SignalStatus,
  TelemetrySource,
  TelemetrySourceKind,
  TelemetryStats,
  TestResult,
  UserRole,
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

  /** A verdict is required when closing an open incident, and refused on any other change. */
  updateStatus: (id: string, newStatus: IncidentStatus, verdict?: IncidentVerdict) =>
    api.patch<void>(`/api/incidents/${id}/status`, { newStatus, verdict }),

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

  /** A pushed source's response carries its key, once. */
  create: (input: TelemetrySourceInput & { kind: TelemetrySourceKind; isEnabled: boolean }) =>
    api.post<TelemetrySource>('/api/telemetry-sources', input),

  update: (id: string, input: TelemetrySourceInput) =>
    api.put<TelemetrySource>(`/api/telemetry-sources/${id}`, input),

  setEnabled: (id: string, isEnabled: boolean) =>
    api.patch<TelemetrySource>(`/api/telemetry-sources/${id}/enabled`, { isEnabled }),

  remove: (id: string) => api.delete<void>(`/api/telemetry-sources/${id}`),

  /** Returns 502 with a body when the customer's source or credentials are wrong. */
  test: (id: string) => api.post<TestResult>(`/api/telemetry-sources/${id}/test`),

  /** Revokes the key in the same write. The response is the only place the new one appears. */
  rotateKey: (id: string) =>
    api.post<TelemetrySource>(`/api/telemetry-sources/${id}/rotate-key`),
}

/** The organisation's incident API keys (Adım 27). Admin only. */
export const incidentApiKeysApi = {
  list: () => api.get<IncidentApiKey[]>('/api/incident-api-keys'),

  /** The response carries the key's value, once. */
  create: (name: string) => api.post<IssuedIncidentApiKey>('/api/incident-api-keys', { name }),

  remove: (id: string) => api.delete<void>(`/api/incident-api-keys/${id}`),
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

/**
 * The session. The two tokens never appear here because they never appear in a body — they are
 * cookies the browser holds, set by the server and unreadable from script.
 */
export const authApi = {
  signIn: (email: string, password: string) =>
    api.post<SessionAccount>('/api/auth/login', { email, password }),

  /** Ends this session. Always 204, so there is nothing to branch on. */
  signOut: () => api.post<void>('/api/auth/logout'),

  /**
   * Read on every page load. Answering from the database rather than from the token is what makes
   * a role change or a deactivation visible before the token would have expired.
   */
  me: () => api.get<SessionAccount>('/api/auth/me'),

  // The two one-time links. Anonymous: whose organisation it is comes from the link's own row,
  // and an invalid, expired or used link all answer the same 404.
  invitation: (token: string) =>
    api.get<InvitationPreview>(`/api/auth/invitations/${encodeURIComponent(token)}`),

  /** Creates the account and signs it in — the cookies arrive with the answer. */
  acceptInvitation: (token: string, displayName: string, password: string) =>
    api.post<SessionAccount>(`/api/auth/invitations/${encodeURIComponent(token)}/accept`, {
      displayName,
      password,
    }),

  passwordReset: (token: string) =>
    api.get<PasswordResetPreview>(`/api/auth/password-resets/${encodeURIComponent(token)}`),

  /** Sets the password, ends every other session of the account, and signs this one in. */
  completePasswordReset: (token: string, password: string) =>
    api.post<SessionAccount>(`/api/auth/password-resets/${encodeURIComponent(token)}/complete`, {
      password,
    }),

  /** Whether this installation still needs its first Admin (Adım 25). Nothing more. */
  setupStatus: () => api.get<{ required: boolean }>('/api/auth/setup'),

  /**
   * Creates the organisation and its first Admin with the one-time code from the identity
   * service's log, and signs the Admin in. Every refusal is the same 404.
   */
  completeSetup: (input: {
    setupCode: string
    organizationName: string
    displayName: string
    email: string
    password: string
  }) => api.post<SessionAccount>('/api/auth/setup/complete', input),

  /** One's own password. Other sessions end; this one is renewed. */
  changePassword: (currentPassword: string, newPassword: string) =>
    api.post<SessionAccount>('/api/auth/password', { currentPassword, newPassword }),
}

/**
 * The outside systems an analysis may read (Adım 17.5). Admin only, reads included.
 */
export const aiSourcesApi = {
  github: () => api.get<GitHubConnection>('/api/ai-sources/github'),

  /** A blank or absent token keeps the saved one. */
  saveGitHub: (input: { token?: string; isEnabled: boolean; repositories: RepositoryMapping[] }) =>
    api.put<GitHubConnection>('/api/ai-sources/github', input),

  removeGitHub: () => api.delete<void>('/api/ai-sources/github'),

  /** Reads each mapped repository once with the saved token. Reads only. */
  testGitHub: () =>
    api.post<{ repositories: RepositoryCheck[] }>('/api/ai-sources/github/test'),
}

/**
 * The organisation's people. Admin only, reads included — everyone else is answered 403, and a
 * member or invitation of another organisation 404.
 */
/** How the organisation's analyses are written (Adım 20.6). Admin only. */
export const aiSettingsApi = {
  get: () => api.get<AiSettings>('/api/ai-settings'),

  save: (responseLanguage: AiResponseLanguage) =>
    api.put<AiSettings>('/api/ai-settings', { responseLanguage }),
}

export const organizationApi = {
  /** The caller's own organisation; there is no id to aim at another. */
  rename: (name: string) => api.patch<{ id: string; name: string }>('/api/organization', { name }),

  members: () => api.get<Member[]>('/api/organization/members'),

  changeRole: (id: string, role: UserRole) =>
    api.patch<Member>(`/api/organization/members/${id}/role`, { role }),

  deactivate: (id: string) => api.post<Member>(`/api/organization/members/${id}/deactivate`),

  activate: (id: string) => api.post<Member>(`/api/organization/members/${id}/activate`),

  /** The link is in this answer and in the email, and nowhere else. */
  issuePasswordReset: (id: string) =>
    api.post<IssuedLink>(`/api/organization/members/${id}/password-reset`),

  invitations: () => api.get<Invitation[]>('/api/organization/invitations'),

  invite: (email: string, role: UserRole) =>
    api.post<InvitationIssued>('/api/organization/invitations', { email, role }),

  revokeInvitation: (id: string) => api.post<void>(`/api/organization/invitations/${id}/revoke`),
}
