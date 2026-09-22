import { createBrowserRouter, Navigate } from 'react-router-dom'

import { AppLayout } from './AppLayout'
import { LoginPage } from '@/features/auth/LoginPage'
import { RequireAuth } from '@/features/auth/RequireAuth'
import { IncidentDetailPage } from '@/features/incidents/IncidentDetailPage'
import { IncidentListPage } from '@/features/incidents/IncidentListPage'
import { EvidencePage } from '@/features/evidence/EvidencePage'
import { SignalsPage } from '@/features/signals/SignalsPage'
import { IntegrationsPage } from '@/features/settings/IntegrationsPage'
import { ProfilePage } from '@/features/settings/ProfilePage'
import { SettingsLayout } from '@/features/settings/SettingsLayout'
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
          {
            // A layout route, so the sub-navigation is mounted once and does not re-enter on every
            // move between its pages.
            path: 'settings',
            element: <SettingsLayout />,
            children: [
              { index: true, element: <Navigate to="/settings/profile" replace /> },
              { path: 'profile', element: <ProfilePage /> },
              { path: 'telemetry', element: <TelemetrySourcesPage /> },
              { path: 'integrations', element: <IntegrationsPage /> },
            ],
          },
          // Telemetry moved under Settings. A bookmark or a shared link is the only record some
          // operators keep of where a thing lives, so the old URL keeps resolving rather than
          // becoming a blank screen. Outside the layout so the redirect does not paint a frame of
          // the settings chrome with no tab selected; `replace` so Back does not bounce off it.
          {
            path: 'settings/telemetry-sources',
            element: <Navigate to="/settings/telemetry" replace />,
          },
        ],
      },
    ],
  },
])
