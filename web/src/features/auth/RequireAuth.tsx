import { Navigate, Outlet, useLocation } from 'react-router-dom'

import { useAuth } from './AuthProvider'

/**
 * A guard over the router, not over the data. It decides which screen renders; what a screen is
 * allowed to see is decided again by the server on every request, which is what actually keeps one
 * organisation out of another's rows.
 */
export function RequireAuth() {
  const { status } = useAuth()
  const location = useLocation()

  // Nothing at all while the session is being restored. This is one round trip on a page load, and
  // the alternatives are both worse: a spinner that flashes for 40ms is noise, and rendering the
  // login screen would put it in front of somebody who turns out to be signed in.
  if (status === 'restoring') return null

  if (status === 'anonymous') {
    // Search and hash too, not just the path. Filters, paging and selection live in the URL, so
    // returning someone to the path alone returns them to a different screen than the one they
    // asked for — which is the whole reason that state is in the URL.
    const from = location.pathname + location.search + location.hash

    // replace, so Back from the login screen does not walk into the guard again.
    return <Navigate to="/login" replace state={{ from }} />
  }

  return <Outlet />
}
