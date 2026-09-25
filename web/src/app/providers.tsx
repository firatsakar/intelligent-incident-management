import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ThemeProvider } from 'next-themes'
import { useState, type ReactNode } from 'react'

import { Toaster } from '@/components/ui/sonner'
import { TooltipProvider } from '@/components/ui/tooltip'
import { AuthProvider } from '@/features/auth/AuthProvider'
import { LanguageProvider, LocaleBoundary } from '@/lib/i18n'

import { RealtimeProvider } from './RealtimeProvider'

// Server state lives here and nowhere else. There is no store: this app owns no state of its own
// worth one, and a store would mean keeping a second copy of what the server already knows.
//
// No refetchInterval anywhere. Freshness arrives over the hubs, which push the changed row
// itself — polling on top of that would be the traffic the socket exists to remove. The one
// exception is a reconnect, where the client invalidates once to close the gap.
export function Providers({ children }: { children: ReactNode }) {
  const [client] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            // Pushed updates write straight into the cache, so a remount should trust what is
            // already there rather than re-asking.
            staleTime: 30_000,
            refetchOnWindowFocus: false,
            retry: 1,
          },
        },
      }),
  )

  return (
    // Language outermost, so everything below can read the dictionary — including the login
    // screen, which renders before a session exists, and the toaster, which sits beside the
    // router rather than inside it. The remount a language change needs is not here: that is
    // LocaleBoundary, further down, wrapped around the page tree alone so the query cache and
    // the three hub connections survive the switch.
    <LanguageProvider>
      {/* System default rather than light: whoever opens this at 3am has already told their OS
          what they want, and asking them again with a bright screen is the wrong first impression.

          disableTransitionOnChange matters more here than on a marketing page — the token change
          touches every surface at once, and without it switching themes animates several hundred
          elements through an intermediate colour for 150ms, which looks like a fault. */}
      <ThemeProvider
        attribute="class"
        defaultTheme="system"
        enableSystem
        disableTransitionOnChange
        storageKey="iim-theme"
      >
        <QueryClientProvider client={client}>
          {/* Base UI's tooltip needs its Provider mounted for the shared delay and the grouping to
              work at all — without it each tooltip waits out its own timer, which is what made the
              component look broken in Adım 19 and got it replaced with a native `title`. It holds
              no state worth scoping to a route, so it wraps everything once. */}
          <TooltipProvider>
            {/* Auth above the router, not inside it: the login screen and the guard are both
                routes, and a session that lived under one of them would be re-read on every
                navigation. Under the query client, because signing in empties the cache. */}
            <AuthProvider>
              <RealtimeProvider>
                <LocaleBoundary>{children}</LocaleBoundary>
              </RealtimeProvider>
            </AuthProvider>
          </TooltipProvider>
          <Toaster position="bottom-right" />
        </QueryClientProvider>
      </ThemeProvider>
    </LanguageProvider>
  )
}
