/**
 * The session seam.
 *
 * NOTHING IN THIS FILE IS SECURITY. It makes a session up in the browser: no credential is asked
 * for, sent or checked, and every service behind this console answers without one. Anyone who
 * skips the browser reaches every row, so a reader must not mistake any of this for a gate.
 *
 * The gate is Adım 15 (the gateway) and Adım 16 (JWT plus organisation-scoped queries), and this
 * is the one file they replace:
 *
 *   stays     the exported surface — readSession / startSession / endSession / watchSession /
 *             rememberedName — and the Session shape. Nothing outside this file knows where a
 *             session comes from, so nothing outside this file has to change.
 *   changes   the bodies. startSession posts credentials to the gateway and keeps the token it
 *             gets back; readSession decodes that token rather than a JSON blob and drops it once
 *             it has expired; watchSession also fires when a refresh fails.
 *   callers   may assume a session exists and carries a name and an organisation worth showing.
 *             They may not assume anybody verified either, and they must not make an
 *             authorisation decision from it — that decision belongs to the server, which is
 *             precisely why none is made here.
 */

export interface SessionUser {
  name: string
  /** Derived, never stored: one spelling of the name is enough to keep true. */
  initials: string
}

export interface SessionOrganization {
  id: string
  name: string
}

export interface Session {
  user: SessionUser
  organization: SessionOrganization
  startedAt: string
}

/**
 * The ownership model, as a constant.
 *
 * Data belongs to an organisation and a user is a member of it — an incident is shared by a team,
 * and one that only its author could see would be no use to anyone. There is exactly one
 * organisation here because there is exactly one database and nothing scopes it; giving it a
 * customer's name would be the kind of lie this file exists not to tell. Adım 16 reads it off the
 * token, and the id is the value that will scope the queries.
 *
 * The user has no id for the same reason: nothing in a browser can mint one that means anything.
 */
export const defaultOrganization: SessionOrganization = {
  id: 'default',
  name: 'Default organisation',
}

// Two keys on purpose. The session goes at sign-out; the name outlives it so signing back in is
// one keystroke, which matters when it is the only thing there is to type.
const SESSION_KEY = 'iim.session'
const NAME_KEY = 'iim.session.name'

// Storage throws rather than returning null in a locked-down browser, and a console that will not
// boot in private mode is a worse failure than one that forgets you on reload.
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
    // Blocked or full: the session then lives exactly as long as the tab does.
  }
}

function removeStorage(key: string): void {
  try {
    localStorage.removeItem(key)
  } catch {
    // Nothing to do, and nothing that depends on it having worked.
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

function parse(raw: string): Session | null {
  const value: unknown = JSON.parse(raw)

  if (typeof value !== 'object' || value === null) return null

  const stored = value as Partial<Session>
  const name = stored.user?.name
  const organization = stored.organization

  if (typeof name !== 'string' || name.trim() === '') return null
  if (typeof organization?.id !== 'string' || typeof organization.name !== 'string') return null

  return {
    user: { name, initials: initialsFor(name) },
    organization: { id: organization.id, name: organization.name },
    startedAt: typeof stored.startedAt === 'string' ? stored.startedAt : new Date().toISOString(),
  }
}

export function readSession(): Session | null {
  const raw = readStorage(SESSION_KEY)

  if (!raw) return null

  let session: Session | null = null

  try {
    session = parse(raw)
  } catch {
    session = null
  }

  // Unreadable means written by an older shape of this file. Dropping it beats booting into a
  // half-session nobody can sign out of.
  if (!session) removeStorage(SESSION_KEY)

  return session
}

/**
 * Async because a real sign-in is a round trip, and having the signature already be the shape the
 * gateway needs is most of what makes this file replaceable without touching its callers. It
 * cannot fail today — there is nothing here that could refuse.
 */
export async function startSession(name: string): Promise<Session> {
  const trimmed = name.trim()

  const session: Session = {
    user: { name: trimmed, initials: initialsFor(trimmed) },
    organization: defaultOrganization,
    startedAt: new Date().toISOString(),
  }

  writeStorage(SESSION_KEY, JSON.stringify(session))
  writeStorage(NAME_KEY, trimmed)

  return session
}

export function endSession(): void {
  removeStorage(SESSION_KEY)
}

/** The last name signed in with, so the login field is filled rather than blank on a return. */
export function rememberedName(): string {
  return readStorage(NAME_KEY) ?? ''
}

/**
 * Fires when another tab signs in or out. Not enforcement — nothing here enforces anything — but a
 * console that still looks signed in after you signed out next door is lying about its own state.
 * Adım 16 fires this when a token refresh fails, which is the case that will actually matter.
 */
export function watchSession(onChange: (session: Session | null) => void): () => void {
  const handler = (event: StorageEvent) => {
    // A null key means the whole store was cleared, which concerns us as much as our own key does.
    if (event.key !== null && event.key !== SESSION_KEY) return

    onChange(readSession())
  }

  window.addEventListener('storage', handler)

  return () => window.removeEventListener('storage', handler)
}
