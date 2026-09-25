import { createBrowserRouter, Navigate } from 'react-router-dom'

import { AppLayout } from './AppLayout'
import { AcceptInvitePage } from '@/features/auth/AcceptInvitePage'
import { LoginPage } from '@/features/auth/LoginPage'
import { RequireAuth } from '@/features/auth/RequireAuth'
import { ResetPasswordPage } from '@/features/auth/ResetPasswordPage'
import { DashboardPage } from '@/features/dashboard/DashboardPage'
import { DeliveriesPage } from '@/features/deliveries/DeliveriesPage'
import { IncidentDetailPage } from '@/features/incidents/IncidentDetailPage'
import { IncidentListPage } from '@/features/incidents/IncidentListPage'
import { EvidencePage } from '@/features/evidence/EvidencePage'
import { SignalsPage } from '@/features/signals/SignalsPage'
import { FunnelPage } from '@/features/telemetry/FunnelPage'
import { ServicesPage } from '@/features/telemetry/ServicesPage'
import { IntegrationsPage } from '@/features/settings/IntegrationsPage'
import { OrganizationPage } from '@/features/settings/OrganizationPage'
import { ProfilePage } from '@/features/settings/ProfilePage'
import { SettingsLayout } from '@/features/settings/SettingsLayout'

export const router = createBrowserRouter([
  // Outside the shell: no rail, no header, nothing to navigate to from here.
  { path: '/login', element: <LoginPage /> },
  // The two one-time links from email. Outside the guard: the person holding one has no session
  // yet, or has a different one, and the link is the only credential the page needs.
  { path: '/invite/:token', element: <AcceptInvitePage /> },
  { path: '/reset/:token', element: <ResetPasswordPage /> },
  {
    // A pathless layout route. The guard wraps every application URL without appearing in any of
    // them, so no link, no bookmark and no shared filtered URL moves.
    element: <RequireAuth />,
    children: [
      {
        path: '/',
        element: <AppLayout />,
        children: [
          // The index route is the dashboard now rather than a redirect to the list. Nothing
          // moved: `/incidents` is still its own route at its own URL, so every bookmark, every
          // shared filtered link and every `to="/incidents"` in the app resolves exactly where it
          // did before.
          { index: true, element: <DashboardPage /> },
          { path: 'incidents', element: <IncidentListPage /> },
          { path: 'incidents/:id', element: <IncidentDetailPage /> },
          { path: 'signals', element: <SignalsPage /> },
          { path: 'evidence', element: <EvidencePage /> },
          // The three aggregate screens. New paths at the top level, so nothing that already
          // resolves moves: every existing URL, bookmark and shared filtered link is untouched.
          { path: 'funnel', element: <FunnelPage /> },
          { path: 'services', element: <ServicesPage /> },
          { path: 'deliveries', element: <DeliveriesPage /> },
          {
            // A layout route, so the sub-navigation is mounted once and does not re-enter on every
            // move between its pages.
            path: 'settings',
            element: <SettingsLayout />,
            children: [
              { index: true, element: <Navigate to="/settings/profile" replace /> },
              { path: 'profile', element: <ProfilePage /> },
              { path: 'organization', element: <OrganizationPage /> },
              // Members became a section of the Organization tab (Adım 25).
              { path: 'members', element: <Navigate to="/settings/organization" replace /> },
              { path: 'integrations', element: <IntegrationsPage /> },
              // Telemetry and the AI sources were tabs of their own until they became groups of
              // the Integrations page. Their addresses keep resolving — to the group, not the top.
              {
                path: 'telemetry',
                element: <Navigate to={{ pathname: '/settings/integrations', hash: '#observability' }} replace />,
              },
              {
                path: 'ai-sources',
                element: <Navigate to={{ pathname: '/settings/integrations', hash: '#analysis' }} replace />,
              },
            ],
          },
          // Telemetry moved under Settings. A bookmark or a shared link is the only record some
          // operators keep of where a thing lives, so the old URL keeps resolving rather than
          // becoming a blank screen. Outside the layout so the redirect does not paint a frame of
          // the settings chrome with no tab selected; `replace` so Back does not bounce off it.
          {
            path: 'settings/telemetry-sources',
            element: <Navigate to={{ pathname: '/settings/integrations', hash: '#observability' }} replace />,
          },
        ],
      },
    ],
  },
])
