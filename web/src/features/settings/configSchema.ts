import type { NotificationChannelType, TelemetrySourceKind } from '@/types/api'

// What each channel and kind actually needs, mirroring IntegrationConfigRules and
// TelemetrySourceConfigRules on the server. The bag is schemaless by design, so a form has to
// know the shape from somewhere; keeping it here rather than scattered through the dialog means
// one place to change when a connector is added.

/**
 * A stable id per declared field, because the config key alone cannot name one: `Url` is the
 * webhook endpoint in one connector and the Seq instance in another, and they do not share a
 * label. The dictionary is total over this union, so a field cannot be declared without a label
 * in both languages.
 */
export type ConfigFieldId =
  | 'email.host'
  | 'email.port'
  | 'email.from'
  | 'email.to'
  | 'email.username'
  | 'email.password'
  | 'webhook.url'
  | 'webhook.timeout'
  | 'webhook.authorization'
  | 'jira.baseUrl'
  | 'jira.projectKey'
  | 'jira.email'
  | 'jira.apiToken'
  | 'jira.issueType'
  | 'seq.url'
  | 'seq.apiKey'
  | 'seq.filter'
  | 'seq.serviceProperty'
  | 'seq.initialLookback'
  | 'otlp.minimumSeverity'

export interface ConfigField {
  key: string
  /**
   * Absent on a stray — a key stored on the record that this schema does not declare. Those
   * render under their own config key, because there is nothing else to call them.
   */
  id?: ConfigFieldId
  placeholder?: string
  required?: boolean
  /** Masked on read, so the form treats it as write-only. */
  secret?: boolean
  /** Only a stray carries its own hint; a declared field's hint is in the dictionary. */
  hint?: string
}

// The placeholders stay here rather than moving to the dictionary: they are example values a
// customer types over — a hostname, a port, an address — not prose, and translating
// `incidents@example.com` would be translating data.
export const integrationFields: Record<NotificationChannelType, ConfigField[]> = {
  Email: [
    { key: 'Host', id: 'email.host', placeholder: 'localhost', required: true },
    { key: 'Port', id: 'email.port', placeholder: '1025', required: true },
    { key: 'From', id: 'email.from', placeholder: 'incidents@example.com', required: true },
    { key: 'To', id: 'email.to', placeholder: 'oncall@example.com', required: true },
    { key: 'Username', id: 'email.username' },
    { key: 'Password', id: 'email.password', secret: true },
  ],
  Webhook: [
    {
      key: 'Url',
      id: 'webhook.url',
      placeholder: 'https://example.com/hooks/incidents',
      required: true,
    },
    { key: 'TimeoutSeconds', id: 'webhook.timeout', placeholder: '10' },
    { key: 'Header:Authorization', id: 'webhook.authorization', secret: true },
  ],
  Jira: [
    { key: 'BaseUrl', id: 'jira.baseUrl', placeholder: 'https://acme.atlassian.net', required: true },
    { key: 'ProjectKey', id: 'jira.projectKey', placeholder: 'OPS', required: true },
    { key: 'Email', id: 'jira.email', required: true },
    { key: 'ApiToken', id: 'jira.apiToken', required: true, secret: true },
    // Optional on the server, which falls back to Task. Absent from this list the form would drop
    // it on every edit, and the customer's chosen issue type would quietly revert.
    { key: 'IssueType', id: 'jira.issueType', placeholder: 'Task' },
  ],
}

export const telemetrySourceFields: Record<TelemetrySourceKind, ConfigField[]> = {
  Seq: [
    { key: 'Url', id: 'seq.url', placeholder: 'http://localhost:8082', required: true },
    { key: 'ApiKey', id: 'seq.apiKey', secret: true },
    { key: 'Filter', id: 'seq.filter', placeholder: "@Level in ['Error','Fatal']" },
    { key: 'ServiceProperty', id: 'seq.serviceProperty', placeholder: 'Service' },
    // Optional on the connector, which falls back to 15. Absent from this list the form would drop
    // it on every edit — the same trap IssueType is in above, and the one this project's demo
    // source is already sitting on.
    { key: 'InitialLookbackMinutes', id: 'seq.initialLookback', placeholder: '15' },
  ],
  // The one setting a pushed source has of its own; everything else is on the sender's side. The
  // server accepts Warning or Error and reads blank as Error.
  Otlp: [{ key: 'MinimumSeverity', id: 'otlp.minimumSeverity', placeholder: 'Error' }],
}
