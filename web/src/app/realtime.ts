import { HubConnectionBuilder, HubConnectionState, type HubConnection } from '@microsoft/signalr'
import type { QueryClient } from '@tanstack/react-query'

import type { Incident, NotificationDelivery, PagedResult, Signal } from '@/types/api'

// Where a pushed message becomes a cache write.
//
// Payloads, not ids. A socket exists to remove HTTP round trips; pushing an id and making the
// client fetch would cost one request per change, which is worse than polling — polling at least
// coalesces. The hub sends the same DTO the endpoint returns, so there is no second contract.
//
// The exception is incidentCreated, which carries only an id. Whether a new incident belongs on
// page one of whatever filter is open is a server question — filter, sort and page boundary — and
// guessing client-side gives a list that disagrees with the server as soon as somebody pages.

export interface HubDefinition {
  name: string
  url: string
  /** Query keys to invalidate after a reconnect, to close the gap of missed messages. */
  recoverKeys: unknown[][]
  handlers: (client: QueryClient) => Record<string, (payload: never) => void>
}

/** Patches one incident into every cached list page that already contains it. */
function patchIncidentLists(client: QueryClient, incident: Incident) {
  const entries = client.getQueriesData<PagedResult<Incident>>({ queryKey: ['incidents'] })

  for (const [key, page] of entries) {
    if (!page?.items.some((item) => item.id === incident.id)) continue

    client.setQueryData<PagedResult<Incident>>(key, {
      ...page,
      items: page.items.map((item) => (item.id === incident.id ? incident : item)),
    })
  }
}

export const hubs: HubDefinition[] = [
  {
    name: 'incidents',
    url: '/hubs/incidents',
    recoverKeys: [['incidents'], ['incident']],
    handlers: (client) => ({
      incidentCreated: () => {
        void client.invalidateQueries({ queryKey: ['incidents'] })
      },
      incidentChanged: (incident: Incident) => {
        client.setQueryData(['incident', incident.id], incident)
        patchIncidentLists(client, incident)
      },
    }),
  },
  {
    name: 'notifications',
    url: '/hubs/notifications',
    recoverKeys: [['deliveries']],
    handlers: (client) => ({
      deliveryRecorded: (delivery: NotificationDelivery) => {
        client.setQueryData<NotificationDelivery[]>(
          ['deliveries', delivery.incidentId],
          (current) => {
            if (!current) return current

            // At-least-once means the same row can arrive twice; replace rather than append.
            const existing = current.findIndex((row) => row.id === delivery.id)

            if (existing >= 0) {
              const next = [...current]
              next[existing] = delivery

              return next
            }

            return [...current, delivery]
          },
        )
      },
    }),
  },
  {
    name: 'signals',
    url: '/hubs/signals',
    recoverKeys: [['signals'], ['evidence']],
    handlers: (client) => ({
      signalRecorded: (signal: Signal) => {
        // Every cached window gets it. A signal detected now belongs in any window that is still
        // open, and the heat map recomputes from this list — so the map updates with no request.
        for (const [key, current] of client.getQueriesData<Signal[]>({ queryKey: ['signals'] })) {
          if (!current || current.some((row) => row.id === signal.id)) continue

          client.setQueryData<Signal[]>(key, [signal, ...current])
        }
      },
    }),
  },
]

export function connect(
  hub: HubDefinition,
  client: QueryClient,
  onStateChange: (name: string, state: HubConnectionState) => void,
): HubConnection {
  const connection = new HubConnectionBuilder()
    .withUrl(hub.url)
    .withAutomaticReconnect()
    .build()

  for (const [method, handler] of Object.entries(hub.handlers(client))) {
    connection.on(method, handler as (...args: unknown[]) => void)
  }

  connection.onreconnecting(() => onStateChange(hub.name, HubConnectionState.Reconnecting))

  connection.onreconnected(() => {
    onStateChange(hub.name, HubConnectionState.Connected)

    // The real cost of pushing payloads: anything sent while the socket was down never arrives.
    // One invalidation per affected key closes the gap, and only runs on a reconnect.
    for (const key of hub.recoverKeys) {
      void client.invalidateQueries({ queryKey: key })
    }
  })

  connection.onclose(() => onStateChange(hub.name, HubConnectionState.Disconnected))

  return connection
}
