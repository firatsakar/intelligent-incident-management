import { createBrowserRouter, Navigate } from 'react-router-dom'

import { AppLayout } from './AppLayout'
import { IncidentDetailPage } from '@/features/incidents/IncidentDetailPage'
import { IncidentListPage } from '@/features/incidents/IncidentListPage'
import { EvidencePage } from '@/features/evidence/EvidencePage'
import { SignalsPage } from '@/features/signals/SignalsPage'
import { IntegrationsPage } from '@/features/settings/IntegrationsPage'
import { TelemetrySourcesPage } from '@/features/settings/TelemetrySourcesPage'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppLayout />,
    children: [
      { index: true, element: <Navigate to="/incidents" replace /> },
      { path: 'incidents', element: <IncidentListPage /> },
      { path: 'incidents/:id', element: <IncidentDetailPage /> },
      { path: 'signals', element: <SignalsPage /> },
      { path: 'evidence', element: <EvidencePage /> },
      { path: 'settings/integrations', element: <IntegrationsPage /> },
      { path: 'settings/telemetry-sources', element: <TelemetrySourcesPage /> },
    ],
  },
])
