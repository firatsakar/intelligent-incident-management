import { useQueryClient } from '@tanstack/react-query'
import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'

import { onUnauthorized } from '@/api/client'
import type { SessionAccount } from '@/types/api'

import {
  adoptSession,
  canOperate,
  isAdmin,
  endSession,
  readSession,
  startSession,
  watchSession,
  type Session,
  type SessionOrganization,
  type SessionUser,
} from './session'

/**
 * Signed in, signed out, or still asking.
 *
 * The third one used to be avoidable and is not any more. When a session was read synchronously
 * out of `localStorage` there was an answer before the first paint; asking the server is a round
 * trip, and rendering the login screen during it would put it in front of somebody who is already
 * signed in.
 */
export type AuthStatus = 'restoring' | 'authenticated' | 'anonymous'

interface AuthValue {
  status: AuthStatus
  user: SessionUser | null
  organization: SessionOrganization | null
  isAuthenticated: boolean
  signIn: (email: string, password: string) => Promise<void>
  /** Takes over a session the server has already opened — an accepted invitation, a reset. */
  adopt: (account: SessionAccount) => void
  /** Asks the server again who this is — after something about the account changed, like its
   *  organisation's name. */
  refresh: () => Promise<void>
  signOut: () => Promise<void>
}

const AuthContext = createContext<AuthValue | null>(null)

/**
 * Whether the signed-in person may change things. False while signed out or restoring, so a
 * control never flashes into view for a frame and disappears.
 */
export function useCanOperate(): boolean {
  const { user } = useAuth()

  return user ? canOperate(user.role) : false
}

/** Whether the signed-in person is an Admin of their organisation. Same false-while-restoring rule. */
export function useIsAdmin(): boolean {
  const { user } = useAuth()

  return user ? isAdmin(user.role) : false
}

export function useAuth(): AuthValue {
  const value = useContext(AuthContext)

  if (!value) throw new Error('useAuth must be used inside AuthProvider')

  return value
}

/**
 * The access cookie lasts fifteen minutes and this asks for a new one before then.
 *
 * Meeting a 401 and refreshing covers every HTTP request already, so this timer exists for the one
 * thing that does not go through `client.ts`: a WebSocket reads the cookie when it connects and
 * never again, so a socket that drops and reconnects after the token expired would be refused and
 * the console would go quiet without saying why.
 *
 * The number below has to stay under `Jwt:AccessMinutes`. It is a coupling, stated rather than
 * discovered — the alternative was the server reporting its own expiry, which is more surface for
 * a timer that only needs to be roughly right.
 */
const REFRESH_INTERVAL_MS = 12 * 60 * 1000

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<AuthStatus>('restoring')
  const [session, setSession] = useState<Session | null>(null)

  // One place decides what the session is, whether the question came from a page load, another
  // tab, or a refresh that failed.
  useEffect(() => {
    let cancelled = false

    async function ask() {
      const next = await readSession()

      if (cancelled) return

      setSession(next)
      setStatus(next ? 'authenticated' : 'anonymous')
    }

    void ask()

    const unwatch = watchSession(() => void ask())

    // A request that could not be saved by a refresh means the session is over. Said here rather
    // than guessed by each screen, which would otherwise each show its own error for what is one
    // fact about the whole console.
    onUnauthorized(() => {
      if (cancelled) return

      setSession(null)
      setStatus('anonymous')
    })

    return () => {
      cancelled = true
      unwatch()
    }
  }, [])

  useEffect(() => {
    if (status !== 'authenticated') return

    const timer = window.setInterval(() => {
      // Deliberately ignoring the outcome. If it failed, the next request will meet a 401 and go
      // through the path that knows what to do about it.
      void fetch('/api/auth/refresh', { method: 'POST' })
    }, REFRESH_INTERVAL_MS)

    return () => window.clearInterval(timer)
  }, [status])

  const value = useMemo<AuthValue>(
    () => ({
      status,
      user: session?.user ?? null,
      organization: session?.organization ?? null,
      isAuthenticated: session !== null,

      signIn: async (email: string, password: string) => {
        const next = await startSession(email, password)

        // Drop whatever the previous session left behind, here rather than at sign-out: at
        // sign-out the app is still mounted and clearing makes every screen refetch on its way to
        // being unmounted. At sign-in nothing is observing the cache yet. It also matters more
        // now than it did — the cache may hold another organisation's rows.
        queryClient.clear()
        setSession(next)
        setStatus('authenticated')
      },

      adopt: (account: SessionAccount) => {
        // The same clearing as signing in, for the same reason: whoever was signed in on this
        // browser before may belong to a different organisation.
        queryClient.clear()
        setSession(adoptSession(account))
        setStatus('authenticated')
      },

      refresh: async () => {
        const next = await readSession()

        setSession(next)
        setStatus(next ? 'authenticated' : 'anonymous')
      },

      signOut: async () => {
        await endSession()
        setSession(null)
        setStatus('anonymous')
      },
    }),
    [status, session, queryClient],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
