import { HubConnectionState } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'

import { useAuth } from '@/features/auth/AuthProvider'

import { connect, hubs } from './realtime'

/** Exported so the dictionary can be forced to be total over it: a status with no word
 *  for it would be announced as an empty chip. */
export type RealtimeStatus = 'connecting' | 'live' | 'reconnecting' | 'offline'

type Status = RealtimeStatus

const RealtimeContext = createContext<Status>('connecting')

export const useRealtimeStatus = () => useContext(RealtimeContext)

/** The one genuinely client-owned piece of state in the app, so it lives in a context. */
function summarise(states: Record<string, HubConnectionState>): Status {
  const values = Object.values(states)

  if (values.length === 0) return 'connecting'
  if (values.some((state) => state === HubConnectionState.Reconnecting)) return 'reconnecting'
  if (values.every((state) => state === HubConnectionState.Connected)) return 'live'
  if (values.some((state) => state === HubConnectionState.Connected)) return 'reconnecting'

  return 'offline'
}

export function RealtimeProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const { isAuthenticated } = useAuth()
  const [states, setStates] = useState<Record<string, HubConnectionState>>({})

  useEffect(() => {
    // Mounted above the router and connected on a flag, rather than mounted inside the guard.
    // Three sockets held open behind a login screen serve nothing the screen shows; three sockets
    // rebuilt on every navigation is worse. This way the connections open once when a session
    // starts, survive every route change, and close when it ends.
    if (!isAuthenticated) return

    // StrictMode mounts, unmounts and mounts again in development, which aborts the first
    // start(). Without this flag that abort is reported as a connection failure, and a console
    // full of expected errors is where real ones go to hide.
    let cancelled = false

    const record = (name: string, state: HubConnectionState) => {
      if (!cancelled) setStates((current) => ({ ...current, [name]: state }))
    }

    const connections = hubs.map((hub) => {
      const connection = connect(hub, queryClient, record)

      connection
        .start()
        .then(() => record(hub.name, HubConnectionState.Connected))
        .catch(() => {
          // A hub that will not start is a service that is down, not a reason to break the app —
          // every screen still works from its own reads, just without live updates.
          record(hub.name, HubConnectionState.Disconnected)
        })

      return connection
    })

    return () => {
      cancelled = true

      for (const connection of connections) void connection.stop()

      // These states described connections that no longer exist. Left behind, the next session
      // would open on "live" before anything had connected.
      setStates({})
    }
  }, [queryClient, isAuthenticated])

  return (
    <RealtimeContext.Provider value={summarise(states)}>{children}</RealtimeContext.Provider>
  )
}
