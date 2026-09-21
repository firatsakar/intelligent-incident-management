import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useState, type ReactNode } from 'react'

import { Toaster } from '@/components/ui/sonner'

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
    <QueryClientProvider client={client}>
      <RealtimeProvider>{children}</RealtimeProvider>
      <Toaster position="bottom-right" />
    </QueryClientProvider>
  )
}
