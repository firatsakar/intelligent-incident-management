import { createBrowserRouter, Navigate } from 'react-router-dom'

import { AppLayout } from './AppLayout'
import { LoginPage } from '@/features/auth/LoginPage'
import { RequireAuth } from '@/features/auth/RequireAuth'
import { IncidentDetailPage } from '@/features/incidents/IncidentDetailPage'
import { IncidentListPage } from '@/features/incidents/IncidentListPage'
import { EvidencePage } from '@/features/evidence/EvidencePage'
import { SignalsPage } from '@/features/signals/SignalsPage'
import { IntegrationsPage } from '@/features/settings/IntegrationsPage'
import { TelemetrySourcesPage } from '@/features/settings/TelemetrySourcesPage'

export const router = createBrowserRouter([
  // Outside the shell: no rail, no header, nothing to navigate to from here.
  { path: '/login', element: <LoginPage /> },
  {
    // A pathless layout route. The guard wraps every application URL without appearing in any of
    // them, so no link, no bookmark and no shared filtered URL moves.
    element: <RequireAuth />,
    children: [
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
    ],
  },
])
