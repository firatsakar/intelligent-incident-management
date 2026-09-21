import { createBrowserRouter, Navigate } from 'react-router-dom'

import { AppLayout } from './AppLayout'
import { ConnectionCheckPage } from '@/features/incidents/ConnectionCheckPage'
import { IncidentDetailPage } from '@/features/incidents/IncidentDetailPage'
import { IncidentListPage } from '@/features/incidents/IncidentListPage'

// Screens land here as their chunks complete. Until then the placeholder proves the proxy path
// and the query wiring, which is the part everything else depends on.
export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppLayout />,
    children: [
      { index: true, element: <Navigate to="/incidents" replace /> },
      { path: 'incidents', element: <IncidentListPage /> },
      { path: 'incidents/:id', element: <IncidentDetailPage /> },
      { path: 'signals', element: <ConnectionCheckPage /> },
      { path: 'evidence', element: <ConnectionCheckPage /> },
      { path: 'settings/integrations', element: <ConnectionCheckPage /> },
      { path: 'settings/telemetry-sources', element: <ConnectionCheckPage /> },
    ],
  },
])
