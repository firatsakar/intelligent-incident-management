import { Building2Icon, PlugIcon, UserRoundIcon } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { Navigate, NavLink, Outlet, useLocation } from 'react-router-dom'

import { useIsAdmin } from '@/features/auth/AuthProvider'

import { useT, type Dictionary } from '@/lib/i18n'
import { cn } from '@/lib/utils'

/**
 * Settings is one destination with several pages, not three destinations that happen to share a
 * prefix.
 *
 * The rail used to carry a "Settings" heading over two entries, which said the same thing this
 * does and said it in a second place: one hierarchy drawn twice, competing over which one the
 * operator should read. The rail now holds a single Settings entry and the second level lives
 * here, inside the thing it belongs to — so the rail answers "which part of the product" and this
 * row answers "which part of settings", and neither repeats the other.
 *
 * Underline rather than the rail's inset bar, and horizontal rather than vertical: a second level
 * that copied the first level's marker would read as the rail having escaped into the page. The
 * icons are the ones the rail already used for these pages, which is what ties the two rows
 * together without either imitating the other.
 */

interface SettingsPage {
  to: string
  /** Keyed into the dictionary, so a page cannot be listed here without a label in both
   *  languages — the compiler checks the key, not a comment. */
  id: keyof Dictionary['settingsNav']['pages']
  icon: LucideIcon
  /** The organisation's configuration, which only its Admins may see (Adım 16.5). */
  adminOnly?: boolean
}

// Profile first: it is the one page about the reader rather than about the platform's plumbing,
// and it is the one that is reached by wanting "my settings" rather than by wanting a connector.
// The organisation next — its name and its people, before its plumbing. Then one page for
// everything the platform connects to,
// grouped inside by what each connection is for (Fırat, 2026-09-25): a tab per connector type was
// the product's plumbing drawn as navigation.
const pages: SettingsPage[] = [
  { to: '/settings/profile', id: 'profile', icon: UserRoundIcon },
  { to: '/settings/organization', id: 'organization', icon: Building2Icon, adminOnly: true },
  { to: '/settings/integrations', id: 'integrations', icon: PlugIcon, adminOnly: true },
]

export function SettingsLayout() {
  const { settingsNav } = useT()
  const admin = useIsAdmin()
  const { pathname } = useLocation()

  const visible = pages.filter((page) => admin || !page.adminOnly)

  // A non-admin who arrives at an admin page by address — a bookmark, a link someone pasted —
  // lands on their own profile rather than on a page whose every request would answer 403.
  const blocked = pages.find((page) => page.adminOnly && !admin && pathname.startsWith(page.to))

  if (blocked) return <Navigate to="/settings/profile" replace />

  return (
    <div className="space-y-6">
      <div className="space-y-4">
        <div className="max-w-2xl">
          <h1 className="text-2xl font-semibold tracking-tight">{settingsNav.title}</h1>
        </div>

        {/* overflow-x-auto is insurance rather than an expected state: three labels fit a phone.
            The rule is drawn on the list so it runs the full width of the content column even when
            the row itself scrolls. */}
        <nav aria-label={settingsNav.sections} className="overflow-x-auto">
          <ul className="border-border flex min-w-max gap-1 border-b">
            {visible.map((page) => (
              <li key={page.to}>
                <NavLink
                  to={page.to}
                  className={({ isActive }) =>
                    cn(
                      // -mb-px pulls the link's own bottom border onto the list's rule, so the
                      // active marker replaces that segment of it rather than sitting under it.
                      'relative -mb-px flex items-center gap-2 border-b-2 px-3 text-sm whitespace-nowrap transition-colors',
                      // 44px where a finger is likely, console density where a pointer is.
                      'h-11 sm:h-9',
                      'focus-visible:ring-ring/50 rounded-t-sm outline-none focus-visible:ring-[3px]',
                      isActive
                        ? 'border-primary text-foreground font-medium'
                        : 'text-muted-foreground hover:border-border hover:text-foreground border-transparent',
                    )
                  }
                >
                  <page.icon className="size-4 shrink-0" aria-hidden />
                  {settingsNav.pages[page.id]}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
      </div>

      <Outlet />
    </div>
  )
}
