/**
 * The session seam.
 *
 * It used to open by saying that nothing in it was security, because nothing in it was: the
 * session was made up in the browser and every service behind this console answered without one.
 * That is over. A session now comes from `/api/auth`, the credential is checked, and the two
 * tokens live in cookies the browser sends and script cannot read — which is why there is nothing
 * for this module to store any more.
 *
 * What is kept in the browser is one address, so the form is filled on a return, and one signal
 * key, so a sign-out in another tab is noticed here. Neither is a credential and neither decides
 * anything.
 *
 * Callers may assume a session carries a name, an organisation and a role worth showing. They must
 * still not make an authorisation decision from it: the role here is what to render, and what to
 * allow is decided by the server on every request.
 */

import { authApi } from '@/api/endpoints'
import type { SessionAccount } from '@/types/api'

/** The three the platform has. The wire values are the enum names, never translated. */
export type UserRole = 'Admin' | 'Engineer' | 'Viewer'

export interface SessionUser {
  id: string
  name: string
  /** Derived, never stored: one spelling of the name is enough to keep true. */
  initials: string
  email: string
  role: UserRole
}

export interface SessionOrganization {
  id: string
  /** The customer's own name for themselves. Never translated, and never a sentinel. */
  name: string
}

export interface Session {
  user: SessionUser
  organization: SessionOrganization
}

function toSession(response: SessionAccount): Session {
  return {
    user: {
      id: response.id,
      name: response.displayName,
      initials: initialsFor(response.displayName),
      email: response.email,
      role: response.role,
    },
    organization: { id: response.organizationId, name: response.organizationName },
  }
}

// Not the session — that lives in a cookie. The address, because typing it again on every return
// is friction with nothing behind it, and a timestamp that changes on every sign-in and sign-out,
// which is how another tab hears about this one.
const EMAIL_KEY = 'iim.session.email'
const SIGNAL_KEY = 'iim.session.signal'

// Storage throws rather than returning null in a locked-down browser, and a console that will not
// boot in private mode is a worse failure than one that forgets your address.
function readStorage(key: string): string | null {
  try {
    return localStorage.getItem(key)
  } catch {
    return null
  }
}

function writeStorage(key: string, value: string): void {
  try {
    localStorage.setItem(key, value)
  } catch {
    // Blocked or full: the convenience is lost and nothing else is.
  }
}

/** Two letters at most: a three-part name inside a 24px circle is a smudge, not an identity. */
export function initialsFor(name: string): string {
  const words = name.trim().split(/\s+/).filter(Boolean)

  if (words.length === 0) return '?'

  // Array.from, not [0] — a name can start with a character that does not fit in one code unit.
  const first = Array.from(words[0])[0] ?? ''
  const last = words.length > 1 ? (Array.from(words[words.length - 1])[0] ?? '') : ''

  return (first + last).toLocaleUpperCase()
}

/**
 * Who the cookie belongs to, asked of the server rather than decoded here.
 *
 * Null means no usable session, which covers a first visit, an expired pair and an account that
 * has been deactivated since the token was minted. The client has already tried refreshing by the
 * time this returns null — that is `client.ts`'s job, on every request.
 */
export async function readSession(): Promise<Session | null> {
  try {
    return toSession(await authApi.me())
  } catch {
    return null
  }
}

/**
 * Signs in, or throws with the server's own sentence.
 *
 * The three ways this fails — wrong password, unknown address, deactivated account — are one
 * answer from the server on purpose, so there is one message to show and no branch here that could
 * reveal which it was.
 */
export async function startSession(email: string, password: string): Promise<Session> {
  const session = toSession(await authApi.signIn(email, password))

  writeStorage(EMAIL_KEY, email.trim())
  writeStorage(SIGNAL_KEY, Date.now().toString())

  return session
}

/**
 * Ends this session and not the account's others. Signing out of a laptop should not sign the
 * phone out, which is why the server revokes the one refresh token presented rather than all of
 * them.
 */
export async function endSession(): Promise<void> {
  try {
    await authApi.signOut()
  } catch {
    // The server always answers 204 and the cookies are cleared either way. A sign-out that can
    // fail is a sign-out the reader cannot trust, so there is nothing to report and nothing to do.
  }

  writeStorage(SIGNAL_KEY, Date.now().toString())
}

/** The last address signed in with, so the form is filled rather than blank on a return. */
export function rememberedEmail(): string {
  return readStorage(EMAIL_KEY) ?? ''
}

/**
 * Fires when another tab signs in or out.
 *
 * The cookies are shared across tabs, so signing out next door really does end this tab's session
 * — but nothing tells this tab, and a console that still looks signed in after you signed out is
 * lying about its own state. The signal key carries no session data; it only says "ask again".
 */
export function watchSession(onChange: () => void): () => void {
  const handler = (event: StorageEvent) => {
    // A null key means the whole store was cleared, which concerns us as much as our own key does.
    if (event.key !== null && event.key !== SIGNAL_KEY) return

    onChange()
  }

  window.addEventListener('storage', handler)

  return () => window.removeEventListener('storage', handler)
}
