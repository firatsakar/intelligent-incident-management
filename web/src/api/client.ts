// One place where HTTP turns into either data or an Error.
//
// All four services are reached through the Vite proxy, so every path here is relative and the
// browser only ever talks to its own origin. Nothing in the app knows there are four services;
// the proxy table in vite.config.ts is the only place that mapping exists.

/** Carries the status so a caller can tell a 404 from a 502 without parsing a message. */
export class ApiError extends Error {
  // Written out rather than declared as constructor parameters: erasableSyntaxOnly is on, so
  // parameter properties are not available.
  readonly status: number
  readonly body: unknown

  constructor(message: string, status: number, body?: unknown) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.body = body
  }
}

interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

/** Turns an RFC 9457 body into one readable line, falling back to the status text. */
function describe(status: number, statusText: string, body: unknown): string {
  if (body && typeof body === 'object') {
    const problem = body as ProblemDetails & { error?: string }

    // The validation pipeline returns field errors; those are the useful part.
    if (problem.errors) {
      const fields = Object.entries(problem.errors)
        .map(([field, messages]) => `${field}: ${messages.join(', ')}`)
        .join(' · ')

      if (fields) return fields
    }

    const single = problem.detail ?? problem.title ?? problem.error
    if (single) return single
  }

  return statusText || `Request failed with ${status}`
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    ...init,
    headers: {
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...init?.headers,
    },
  })

  // 204 from the PATCH endpoints, and any empty body.
  const text = await response.text()
  const parsed: unknown = text ? JSON.parse(text) : null

  if (!response.ok) {
    throw new ApiError(describe(response.status, response.statusText, parsed), response.status, parsed)
  }

  return parsed as T
}

/** Drops undefined and null so they do not end up in the query string as "undefined". */
function query(params: Record<string, string | number | boolean | undefined | null>): string {
  const search = new URLSearchParams()

  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') {
      search.set(key, String(value))
    }
  }

  const rendered = search.toString()

  return rendered ? `?${rendered}` : ''
}

export const api = {
  get: <T>(path: string, params?: Record<string, string | number | boolean | undefined | null>) =>
    request<T>(`${path}${params ? query(params) : ''}`),

  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) }),

  put: <T>(path: string, body: unknown) =>
    request<T>(path, { method: 'PUT', body: JSON.stringify(body) }),

  patch: <T>(path: string, body: unknown) =>
    request<T>(path, { method: 'PATCH', body: JSON.stringify(body) }),

  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
}
