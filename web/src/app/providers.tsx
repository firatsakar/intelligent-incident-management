import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ThemeProvider } from 'next-themes'
import { useState, type ReactNode } from 'react'

import { Toaster } from '@/components/ui/sonner'
import { AuthProvider } from '@/features/auth/AuthProvider'

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
    // System default rather than light: whoever opens this at 3am has already told their OS what
    // they want, and asking them again with a bright screen is the wrong first impression.
    //
    // disableTransitionOnChange matters more here than on a marketing page — the token change
    // touches every surface at once, and without it switching themes animates several hundred
    // elements through an intermediate colour for 150ms, which looks like a fault.
    <ThemeProvider
      attribute="class"
      defaultTheme="system"
      enableSystem
      disableTransitionOnChange
      storageKey="iim-theme"
    >
      <QueryClientProvider client={client}>
        {/* Auth above the router, not inside it: the login screen and the guard are both routes,
            and a session that lived under one of them would be re-read on every navigation. Under
            the query client, because signing in empties the cache. */}
        <AuthProvider>
          <RealtimeProvider>{children}</RealtimeProvider>
        </AuthProvider>
        <Toaster position="bottom-right" />
      </QueryClientProvider>
    </ThemeProvider>
  )
}
