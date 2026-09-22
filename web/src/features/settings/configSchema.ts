import type { NotificationChannelType, TelemetrySourceKind } from '@/types/api'

// What each channel and kind actually needs, mirroring IntegrationConfigRules and
// TelemetrySourceConfigRules on the server. The bag is schemaless by design, so a form has to
// know the shape from somewhere; keeping it here rather than scattered through the dialog means
// one place to change when a connector is added.

export interface ConfigField {
  key: string
  label: string
  placeholder?: string
  required?: boolean
  /** Masked on read, so the form treats it as write-only. */
  secret?: boolean
  hint?: string
}

export const integrationFields: Record<NotificationChannelType, ConfigField[]> = {
  Email: [
    { key: 'Host', label: 'SMTP host', placeholder: 'localhost', required: true },
    { key: 'Port', label: 'Port', placeholder: '1025', required: true },
    { key: 'From', label: 'From', placeholder: 'incidents@example.com', required: true },
    { key: 'To', label: 'To', placeholder: 'oncall@example.com', required: true },
    { key: 'Username', label: 'Username' },
    { key: 'Password', label: 'Password', secret: true },
  ],
  Webhook: [
    { key: 'Url', label: 'URL', placeholder: 'https://example.com/hooks/incidents', required: true },
    { key: 'TimeoutSeconds', label: 'Timeout (seconds)', placeholder: '10' },
    {
      key: 'Header:Authorization',
      label: 'Authorization header',
      hint: 'Any setting prefixed Header: is sent as a request header.',
      secret: true,
    },
  ],
  Jira: [
    { key: 'BaseUrl', label: 'Base URL', placeholder: 'https://acme.atlassian.net', required: true },
    { key: 'ProjectKey', label: 'Project key', placeholder: 'OPS', required: true },
    { key: 'Email', label: 'Account email', required: true },
    { key: 'ApiToken', label: 'API token', required: true, secret: true },
    // Optional on the server, which falls back to Task. Absent from this list the form would drop
    // it on every edit, and the customer's chosen issue type would quietly revert.
    { key: 'IssueType', label: 'Issue type', placeholder: 'Task' },
  ],
}

export const telemetrySourceFields: Record<TelemetrySourceKind, ConfigField[]> = {
  Seq: [
    { key: 'Url', label: 'Seq URL', placeholder: 'http://localhost:8082', required: true },
    {
      key: 'ApiKey',
      label: 'API key',
      secret: true,
      hint: 'Only needed when the Seq instance has authentication enabled. The key needs Read permission.',
    },
    {
      key: 'Filter',
      label: 'Filter',
      placeholder: "@Level in ['Error','Fatal']",
      hint: 'Seq filter expression. Left blank, the connector reads errors and fatals.',
    },
    {
      key: 'ServiceProperty',
      label: 'Service property',
      placeholder: 'Service',
      hint: 'Which event property names the service a log line came from.',
    },
    // Optional on the connector, which falls back to 15. Absent from this list the form would drop
    // it on every edit — the same trap IssueType is in above, and the one this project's demo
    // source is already sitting on.
    {
      key: 'InitialLookbackMinutes',
      label: 'Initial lookback (minutes)',
      placeholder: '15',
      hint: 'How far back the first poll reads. Later polls resume from where the last one stopped.',
    },
  ],
}
