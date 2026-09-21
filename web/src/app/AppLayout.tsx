import {
  ActivityIcon,
  DatabaseIcon,
  MenuIcon,
  PlugIcon,
  ScrollTextIcon,
  SirenIcon,
} from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { useState } from 'react'
import { NavLink, Outlet } from 'react-router-dom'

import { BrandMark } from '@/components/BrandMark'
import { Button } from '@/components/ui/button'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet'
import { useAuth } from '@/features/auth/AuthProvider'
import { cn } from '@/lib/utils'

import { RealtimeIndicator } from './RealtimeIndicator'
import { ThemeToggle } from './ThemeToggle'
import { UserMenu } from './UserMenu'

interface NavItem {
  to: string
  label: string
  icon: LucideIcon
}

interface NavGroup {
  label: string
  items: NavItem[]
}

// Grouped, not flat. The two Settings entries are due to collapse into one destination that holds
// them as sub-navigation; with the grouping already here that becomes an edit to this array rather
// than a re-layout of the rail, and the routes do not move either way.
const navigation: NavGroup[] = [
  {
    label: 'Operations',
    items: [
      { to: '/incidents', label: 'Incidents', icon: SirenIcon },
      { to: '/signals', label: 'Signals', icon: ActivityIcon },
      { to: '/evidence', label: 'Evidence', icon: ScrollTextIcon },
    ],
  },
  {
    label: 'Settings',
    items: [
      { to: '/settings/integrations', label: 'Integrations', icon: PlugIcon },
      { to: '/settings/telemetry-sources', label: 'Telemetry', icon: DatabaseIcon },
    ],
  },
]

/**
 * The organisation rides under the product name wherever there is a second line for it — the rail
 * and the drawer, not the phone header. It is the scope of every number on every screen, so it
 * belongs somewhere permanently visible rather than behind a click in the account menu; and it
 * sits here, in the one block that is about identity rather than navigation, so that the day there
 * is more than one organisation the switch has an obvious home.
 */
function Brand({ className, organization }: { className?: string; organization?: string }) {
  return (
    <span className={cn('flex items-center gap-2.5', className)}>
      <BrandMark />
      <span className="min-w-0">
        <span className="block truncate text-sm font-semibold tracking-tight">
          Incident Management
        </span>
        {organization && (
          <span className="text-muted-foreground block truncate text-xs">{organization}</span>
        )}
      </span>
    </span>
  )
}

function NavItems({ onNavigate, touch }: { onNavigate?: () => void; touch?: boolean }) {
  return (
    <>
      {navigation.map((group) => (
        <div key={group.label} className="mb-5 last:mb-0">
          <p className="text-muted-foreground mb-1 px-2.5 text-[0.6875rem] font-medium tracking-wider uppercase">
            {group.label}
          </p>

          <ul className="space-y-0.5">
            {group.items.map((item) => (
              <li key={item.to}>
                <NavLink
                  to={item.to}
                  onClick={onNavigate}
                  className={({ isActive }) =>
                    cn(
                      // The 2px inset bar on the left is the active marker. It is the accent's
                      // whole job in the shell: where you are, never what the system found.
                      'relative flex items-center gap-2.5 rounded-md pr-2.5 pl-3 text-sm transition-colors',
                      // The drawer is the touch surface, so its rows get a 44px target; the rail
                      // is pointer-only and stays at console density.
                      touch ? 'h-11' : 'h-8',
                      'before:absolute before:inset-y-1.5 before:left-0 before:w-0.5 before:rounded-full before:transition-colors',
                      'focus-visible:ring-ring/50 outline-none focus-visible:ring-[3px]',
                      isActive
                        ? 'bg-sidebar-accent text-sidebar-accent-foreground before:bg-primary font-medium'
                        : 'text-muted-foreground hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground before:bg-transparent',
                    )
                  }
                >
                  <item.icon className="size-4 shrink-0" />
                  {item.label}
                </NavLink>
              </li>
            ))}
          </ul>
        </div>
      ))}
    </>
  )
}

export function AppLayout() {
  const [menuOpen, setMenuOpen] = useState(false)
  // Never null in practice — RequireAuth is the only route that renders this — but the context is
  // typed for the login screen too, where it is.
  const { organization } = useAuth()

  return (
    <div className="bg-background text-foreground min-h-svh">
      <a
        href="#content"
        className="bg-primary text-primary-foreground focus:ring-ring sr-only rounded-md px-3 py-2 text-sm focus:not-sr-only focus:absolute focus:top-2 focus:left-2 focus:z-50 focus:ring-2"
      >
        Skip to content
      </a>

      {/* A rail rather than a top bar: the section list grows (Settings gains sub-navigation next
          chunk), and a vertical list of five is muscle memory for someone who keeps this open all
          day. The tables keep their width because the content column is capped anyway. */}
      <aside className="bg-sidebar border-sidebar-border fixed inset-y-0 left-0 z-30 hidden w-56 flex-col border-r lg:flex">
        {/* h-14 rather than h-12 now that the block carries two lines. The rail no longer lines up
            with the header bar, which was never a visible edge — there is no rule under it. */}
        <div className="flex h-14 shrink-0 items-center px-4">
          <NavLink
            to="/incidents"
            className="focus-visible:ring-ring/50 min-w-0 rounded-md outline-none focus-visible:ring-[3px]"
          >
            <Brand organization={organization?.name} />
          </NavLink>
        </div>

        <nav aria-label="Sections" className="flex-1 overflow-y-auto px-3 py-3">
          <NavItems />
        </nav>
      </aside>

      <div className="lg:pl-56">
        <header className="bg-background/85 sticky top-0 z-20 border-b backdrop-blur-sm">
          <div className="flex h-12 items-center gap-2 px-3 lg:px-6">
            <Sheet open={menuOpen} onOpenChange={setMenuOpen}>
              <SheetTrigger
                render={
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    className="lg:hidden"
                    aria-label="Open navigation"
                  />
                }
              >
                <MenuIcon />
              </SheetTrigger>

              <SheetContent side="left" className="w-72 sm:max-w-none">
                <SheetHeader className="h-14 justify-center px-4 py-0">
                  <SheetTitle>
                    <Brand organization={organization?.name} />
                  </SheetTitle>
                  <SheetDescription className="sr-only">
                    Move between the console's sections.
                  </SheetDescription>
                </SheetHeader>

                <nav aria-label="Sections" className="flex-1 overflow-y-auto px-3 pb-4">
                  <NavItems onNavigate={() => setMenuOpen(false)} touch />
                </nav>
              </SheetContent>
            </Sheet>

            <Brand className="lg:hidden" />

            <div className="ml-auto flex items-center gap-1.5">
              <RealtimeIndicator />
              <ThemeToggle />
              <UserMenu />
            </div>
          </div>
        </header>

        <main id="content" className="mx-auto w-full max-w-[1400px] px-4 py-6 lg:px-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
