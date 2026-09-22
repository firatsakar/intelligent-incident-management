import { DatabaseIcon, PlugIcon, UserRoundIcon } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { NavLink, Outlet } from 'react-router-dom'

import { cn } from '@/lib/utils'

/**
 * Settings is one destination with three pages, not three destinations that happen to share a
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
  label: string
  icon: LucideIcon
}

// Profile first: it is the one page about the reader rather than about the platform's plumbing,
// and it is the one that is reached by wanting "my settings" rather than by wanting a connector.
// Then the two halves of the pipeline in the order data moves through them — read, then routed.
const pages: SettingsPage[] = [
  { to: '/settings/profile', label: 'Profile', icon: UserRoundIcon },
  { to: '/settings/telemetry', label: 'Telemetry', icon: DatabaseIcon },
  { to: '/settings/integrations', label: 'Integrations', icon: PlugIcon },
]

export function SettingsLayout() {
  return (
    <div className="space-y-6">
      <div className="space-y-4">
        <div className="max-w-2xl">
          <h1 className="text-2xl font-semibold tracking-tight">Settings</h1>
          <p className="text-muted-foreground mt-1 text-sm">
            Your profile, the log stores this platform reads from, and the destinations it sends
            what it finds to.
          </p>
        </div>

        {/* overflow-x-auto is insurance rather than an expected state: three labels fit a phone.
            The rule is drawn on the list so it runs the full width of the content column even when
            the row itself scrolls. */}
        <nav aria-label="Settings sections" className="overflow-x-auto">
          <ul className="border-border flex min-w-max gap-1 border-b">
            {pages.map((page) => (
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
                  {page.label}
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
