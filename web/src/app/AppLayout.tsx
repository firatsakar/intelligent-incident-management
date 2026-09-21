import { NavLink, Outlet } from 'react-router-dom'

import { cn } from '@/lib/utils'

const navigation = [
  { to: '/incidents', label: 'Incidents' },
  { to: '/signals', label: 'Signals' },
  { to: '/evidence', label: 'Evidence' },
  { to: '/settings/integrations', label: 'Integrations' },
  { to: '/settings/telemetry-sources', label: 'Sources' },
]

export function AppLayout() {
  return (
    <div className="min-h-screen bg-background text-foreground">
      <header className="border-b">
        <div className="mx-auto flex h-14 max-w-[1400px] items-center gap-6 px-4">
          <NavLink to="/incidents" className="font-semibold tracking-tight">
            Incident Management
          </NavLink>

          <nav className="flex items-center gap-1 text-sm">
            {navigation.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) =>
                  cn(
                    'rounded-md px-3 py-1.5 transition-colors',
                    isActive
                      ? 'bg-secondary text-secondary-foreground'
                      : 'text-muted-foreground hover:text-foreground',
                  )
                }
              >
                {item.label}
              </NavLink>
            ))}
          </nav>
        </div>
      </header>

      <main className="mx-auto max-w-[1400px] px-4 py-6">
        <Outlet />
      </main>
    </div>
  )
}
