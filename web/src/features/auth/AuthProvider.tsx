import { useQueryClient } from '@tanstack/react-query'
import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'

import {
  endSession,
  readSession,
  startSession,
  watchSession,
  type Session,
  type SessionOrganization,
  type SessionUser,
} from './session'

interface AuthValue {
  user: SessionUser | null
  /**
   * In the type from the start, though today it is always the one organisation. The ownership
   * model was already decided — data belongs to an organisation, a user is a member of it — and
   * adding the field later would mean editing every consumer to read something they could have
   * been reading all along.
   */
  organization: SessionOrganization | null
  isAuthenticated: boolean
  signIn: (name: string) => Promise<void>
  signOut: () => void
}

const AuthContext = createContext<AuthValue | null>(null)

export function useAuth(): AuthValue {
  const value = useContext(AuthContext)

  if (!value) throw new Error('useAuth must be used inside AuthProvider')

  return value
}

/**
 * Holds the session in React state and nothing else; where a session comes from is session.ts's
 * business, and keeping it that way is what lets Adım 16 replace that one file.
 *
 * Read synchronously at mount rather than in an effect. An async bootstrap would need a third
 * state — signed in, signed out, still asking — and that third state is a frame of the login
 * screen in front of somebody who is already signed in. Adım 16 keeps the property: decoding a
 * stored token is synchronous too, and refreshing one belongs behind watchSession.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [session, setSession] = useState<Session | null>(() => readSession())

  useEffect(() => watchSession(setSession), [])

  const value = useMemo<AuthValue>(
    () => ({
      user: session?.user ?? null,
      organization: session?.organization ?? null,
      isAuthenticated: session !== null,

      signIn: async (name: string) => {
        const next = await startSession(name)

        // Drop whatever the previous session left behind, here rather than at sign-out: at
        // sign-out the app is still mounted and clearing makes every screen refetch on its way to
        // being unmounted. At sign-in nothing is observing the cache yet.
        queryClient.clear()
        setSession(next)
      },

      signOut: () => {
        endSession()
        setSession(null)
      },
    }),
    [session, queryClient],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
