import { createBrowserRouter, Navigate } from 'react-router-dom'

import { AppLayout } from './AppLayout'
import { ConnectionCheckPage } from '@/features/incidents/ConnectionCheckPage'

// Screens land here as their chunks complete. Until then the placeholder proves the proxy path
// and the query wiring, which is the part everything else depends on.
export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppLayout />,
    children: [
      { index: true, element: <Navigate to="/incidents" replace /> },
      { path: 'incidents', element: <ConnectionCheckPage /> },
      { path: 'signals', element: <ConnectionCheckPage /> },
      { path: 'evidence', element: <ConnectionCheckPage /> },
      { path: 'settings/integrations', element: <ConnectionCheckPage /> },
      { path: 'settings/telemetry-sources', element: <ConnectionCheckPage /> },
    ],
  },
])
