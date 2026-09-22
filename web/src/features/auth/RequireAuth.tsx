import { Navigate, Outlet, useLocation } from 'react-router-dom'

import { useAuth } from './AuthProvider'

/**
 * A guard over the router, not over the data. It decides which screen renders and nothing else —
 * the APIs behind those screens answer anyone who asks, with or without this component. See
 * session.ts.
 */
export function RequireAuth() {
  const { isAuthenticated } = useAuth()
  const location = useLocation()

  if (!isAuthenticated) {
    // Search and hash too, not just the path. Filters, paging and selection live in the URL, so
    // returning someone to the path alone returns them to a different screen than the one they
    // asked for — which is the whole reason that state is in the URL.
    const from = location.pathname + location.search + location.hash

    // replace, so Back from the login screen does not walk into the guard again.
    return <Navigate to="/login" replace state={{ from }} />
  }

  return <Outlet />
}
