import {
  ActivityIcon,
  FunnelIcon,
  LayoutDashboardIcon,
  MenuIcon,
  ScrollTextIcon,
  SendIcon,
  ServerIcon,
  SettingsIcon,
  SirenIcon,
} from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { useState } from 'react'
import { NavLink, Outlet } from 'react-router-dom'

import { BrandMark, productName } from '@/components/BrandMark'
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
import { organizationName } from '@/features/auth/session'
import { useT, type Dictionary } from '@/lib/i18n'
import { cn } from '@/lib/utils'

import { RealtimeIndicator } from './RealtimeIndicator'
import { LanguageToggle } from './LanguageToggle'
import { ThemeToggle } from './ThemeToggle'
import { UserMenu } from './UserMenu'

interface NavItem {
  to: string
  /** Keyed into the dictionary rather than spelled here: a destination cannot be added to the
   *  rail without a label existing in both languages, and the compiler is what says so. */
  id: keyof Dictionary['nav']['items']
  icon: LucideIcon
  /**
   * Match this path exactly. Only the dashboard needs it: it sits at `/`, which is a prefix of
   * every other URL in the console, so without it the rail marks Dashboard as the current section
   * from every screen at once. The others want the prefix match — `/incidents/:id` is still
   * Incidents.
   */
  end?: boolean
}

interface NavGroup {
  /** Omitted for a group whose only entry already says what the heading would have said. */
  id?: keyof Dictionary['nav']['groups']
  items: NavItem[]
}

// The collapse this array was grouped in anticipation of: Settings is now one destination that
// holds Profile, Telemetry and Integrations as sub-navigation, so the rail carries one entry for
// it instead of a heading over two.
//
// That entry keeps its own group rather than joining Operations. The gap is what separates "the
// work" from "the setup", which is the distinction the headings were drawing; the heading itself
// is gone because a heading reading "Settings" over a single row reading "Settings" is the same
// hierarchy printed twice, and printing it twice is precisely what this change removes.
//
// The three aggregate screens are a group of their own rather than three more rows under
// Operations. Operations answers "what is broken and what do I do about it", one incident at a
// time; Pipeline answers "is the machine doing its job" — what the gate filtered, which service is
// producing it, and whether anybody was actually told. Different question, asked at a different
// hour, by a reader in a different posture. Seven rows under one heading would have made every one
// of them look equally likely to be the one you want at 3am, which is the opposite of what a rail
// is for.
//
// Inside the group, Funnel leads because it is the product's own claim stated as a number; the
// other two are where you go when it raises a question — which service, and did anyone hear.
const navigation: NavGroup[] = [
  {
    id: 'operations',
    items: [
      // First, and in this group rather than above it: it is a view of the same work, not a
      // different kind of destination, and a heading over one row is hierarchy printed twice.
      { to: '/', id: 'dashboard', icon: LayoutDashboardIcon, end: true },
      { to: '/incidents', id: 'incidents', icon: SirenIcon },
      { to: '/signals', id: 'signals', icon: ActivityIcon },
      { to: '/evidence', id: 'evidence', icon: ScrollTextIcon },
    ],
  },
  {
    id: 'pipeline',
    items: [
      { to: '/funnel', id: 'funnel', icon: FunnelIcon },
      { to: '/services', id: 'services', icon: ServerIcon },
      { to: '/deliveries', id: 'deliveries', icon: SendIcon },
    ],
  },
  {
    items: [{ to: '/settings', id: 'settings', icon: SettingsIcon }],
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
    // min-w-0 on the outer span too, not only on the inner one. A flex item's default minimum is
    // its content, so without it the inner `truncate` has nothing to truncate against and the
    // whole name pushes the header wider than the viewport. Found at 320px in Turkish, where the
    // realtime chip is a word longer than "offline" and spent the slack that hid it.
    <span className={cn('flex min-w-0 items-center gap-2.5', className)}>
      <BrandMark />
      <span className="min-w-0">
        <span className="block truncate text-sm font-semibold tracking-tight">
          {productName}
        </span>
        {organization && (
          <span className="text-muted-foreground block truncate text-xs">{organization}</span>
        )}
      </span>
    </span>
  )
}

function NavItems({ onNavigate, touch }: { onNavigate?: () => void; touch?: boolean }) {
  const { nav } = useT()

  return (
    <>
      {navigation.map((group) => (
        <div key={group.id ?? group.items[0].to} className="mb-5 last:mb-0">
          {group.id && (
            <p className="text-muted-foreground mb-1 px-2.5 text-[0.6875rem] font-medium tracking-wider uppercase">
              {nav.groups[group.id]}
            </p>
          )}

          <ul className="space-y-0.5">
            {group.items.map((item) => (
              <li key={item.to}>
                <NavLink
                  to={item.to}
                  end={item.end}
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
                  {nav.items[item.id]}
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
  const { nav } = useT()

  return (
    <div className="bg-background text-foreground min-h-svh">
      <a
        href="#content"
        className="bg-primary text-primary-foreground focus:ring-ring sr-only rounded-md px-3 py-2 text-sm focus:not-sr-only focus:absolute focus:top-2 focus:left-2 focus:z-50 focus:ring-2"
      >
        {nav.skip}
      </a>

      {/* A rail rather than a top bar: the section list grows (Settings gains sub-navigation next
          chunk), and a vertical list of five is muscle memory for someone who keeps this open all
          day. The tables keep their width because the content column is capped anyway. */}
      <aside className="bg-sidebar border-sidebar-border fixed inset-y-0 left-0 z-30 hidden w-56 flex-col border-r lg:flex">
        {/* h-14 rather than h-12 now that the block carries two lines. The rail no longer lines up
            with the header bar, which was never a visible edge — there is no rule under it. */}
        <div className="flex h-14 shrink-0 items-center px-4">
          <NavLink
            to="/"
            className="focus-visible:ring-ring/50 min-w-0 rounded-md outline-none focus-visible:ring-[3px]"
          >
            <Brand organization={organization ? organizationName(organization) : undefined} />
          </NavLink>
        </div>

        <nav aria-label={nav.sections} className="flex-1 overflow-y-auto px-3 py-3">
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
                    aria-label={nav.open}
                  />
                }
              >
                <MenuIcon />
              </SheetTrigger>

              <SheetContent side="left" className="w-72 sm:max-w-none">
                <SheetHeader className="h-14 justify-center px-4 py-0">
                  <SheetTitle>
                    <Brand
                      organization={organization ? organizationName(organization) : undefined}
                    />
                  </SheetTitle>
                  <SheetDescription className="sr-only">{nav.drawer}</SheetDescription>
                </SheetHeader>

                <nav aria-label={nav.sections} className="flex-1 overflow-y-auto px-3 pb-4">
                  <NavItems onNavigate={() => setMenuOpen(false)} touch />
                </nav>
              </SheetContent>
            </Sheet>

            <Brand className="lg:hidden" />

            <div className="ml-auto flex shrink-0 items-center gap-1.5">
              <RealtimeIndicator />
              <LanguageToggle />
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
