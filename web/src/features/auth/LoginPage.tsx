import { useQuery } from '@tanstack/react-query'
import { ListChecksIcon, RouteIcon, ScaleIcon } from 'lucide-react'
import { useRef, useState, type CSSProperties, type FormEvent } from 'react'
import { Navigate, useLocation } from 'react-router-dom'

import { LanguageToggle } from '@/app/LanguageToggle'
import { ThemeToggle } from '@/app/ThemeToggle'
import { BrandMark, productName } from '@/components/BrandMark'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardFooter, CardHeader } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ApiError } from '@/api/client'
import { authApi } from '@/api/endpoints'
import { useT, type Dictionary } from '@/lib/i18n'

import { useAuth } from './AuthProvider'
import { rememberedEmail } from './session'

/**
 * The first screen anyone sees, and the only one that is nothing but design — everywhere else the
 * data is the design. It has to look like the product it fronts: calm, dense, cool ground, accent
 * for identity only, because this is where the visual identity gets established.
 *
 * It used to carry a caution saying it checked nothing, and no password field, because a field
 * that accepts anything is worse than no field — it teaches the operator that a credential was
 * verified. Both are gone: there is a password now, and something checks it.
 *
 * The organisation is not shown here any more either. It is a property of the account, so it
 * cannot be known before the account is, and naming one before anybody has signed in would be
 * guessing.
 */

// True of this product, and the reason it is not an alerting rule. Each line names something an
// operator can actually go and look at once they are through this screen. The icons stay here and
// the claims live in the dictionary — an id that has no copy behind it will not compile.
const points: { icon: typeof ScaleIcon; id: keyof Dictionary['login']['points'] }[] = [
  { icon: ScaleIcon, id: 'gate' },
  { icon: ListChecksIcon, id: 'restraint' },
  { icon: RouteIcon, id: 'routing' },
]

// A static ground: a wash of the accent off the top-left corner and a hairline grid, masked out
// before it reaches the text. Static because the register forbids ambient motion, and this screen
// is the one most likely to be sitting open on a wall display.
const ground: CSSProperties = {
  backgroundImage: [
    'radial-gradient(52rem 34rem at 10% -8%, color-mix(in oklch, var(--primary) 16%, transparent), transparent 70%)',
    'linear-gradient(to right, color-mix(in oklch, var(--sidebar-border) 65%, transparent) 1px, transparent 1px)',
    'linear-gradient(to bottom, color-mix(in oklch, var(--sidebar-border) 65%, transparent) 1px, transparent 1px)',
  ].join(', '),
  backgroundSize: '100% 100%, 3.5rem 3.5rem, 3.5rem 3.5rem',
  maskImage: 'radial-gradient(58rem 46rem at 18% 8%, black, transparent 88%)',
  WebkitMaskImage: 'radial-gradient(58rem 46rem at 18% 8%, black, transparent 88%)',
}

export function LoginPage() {
  const { status, isAuthenticated, signIn } = useAuth()
  const location = useLocation()
  const { login } = useT()

  const [email, setEmail] = useState(rememberedEmail)
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const emailField = useRef<HTMLInputElement>(null)
  const passwordField = useRef<HTMLInputElement>(null)

  // Where the guard turned them away from, with its query string intact.
  const from = (location.state as { from?: string } | null)?.from ?? '/'

  // A fresh installation has nobody to sign in as (Adım 25). Never retried: if the answer does
  // not come, the sign-in form is still the right screen to show.
  const setup = useQuery({
    queryKey: ['setup-status'],
    queryFn: authApi.setupStatus,
    retry: false,
    staleTime: 60_000,
  })

  // Nothing until the session has been asked about, so this form does not appear for a frame in
  // front of somebody who turns out to be signed in.
  if (status === 'restoring') return null

  // One redirect for two cases — a session that was already there, and the one just created — so
  // signing in has a single exit and cannot race a second navigate.
  if (isAuthenticated) return <Navigate to={from} replace />

  if (setup.data?.required) return <Navigate to="/setup" replace />

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const address = email.trim()

    if (!address || !password) {
      setError(login.required)
      const missing = address ? passwordField : emailField
      missing.current?.focus()

      return
    }

    setPending(true)
    setError(null)

    try {
      await signIn(address, password)
    } catch (cause) {
      setError(describe(cause))
      setPassword('')
      setPending(false)
      passwordField.current?.focus()
    }
  }

  /**
   * Four outcomes, four sentences. Only one of them is about the password.
   *
   * This screen reported every failure as a refused credential, so a stopped service told the
   * reader their password had stopped working — which is both false and the kind of false that
   * sends somebody off to reset something that was never wrong.
   */
  function describe(cause: unknown): string {
    if (!(cause instanceof ApiError)) return login.unreachable
    if (cause.status === 401) return login.failed
    if (cause.status === 429) return login.tooMany

    return login.serverError
  }

  return (
    <div className="bg-background text-foreground min-h-svh lg:grid lg:grid-cols-2 xl:grid-cols-[1.15fr_1fr]">
      {/* Hidden rather than stacked below lg. It is the product introducing itself, not
          information the operator came for, and on a phone it would push the one control on the
          page off the first screen. */}
      <aside className="bg-sidebar border-sidebar-border relative hidden flex-col justify-between overflow-hidden border-r px-10 py-12 lg:flex xl:px-16">
        <div aria-hidden className="pointer-events-none absolute inset-0" style={ground} />

        <div className="relative flex items-center gap-2.5">
          <BrandMark className="size-7" />
          <span className="text-sm font-semibold tracking-tight">{productName}</span>
        </div>

        <div className="relative my-12 max-w-lg">
          <p className="text-3xl leading-tight font-semibold tracking-tight text-balance xl:text-4xl">
            {login.tagline}
          </p>
          <p className="text-muted-foreground mt-4 text-base leading-relaxed">
            {login.taglineDetail}
          </p>
        </div>

        <ul className="border-sidebar-border relative max-w-lg space-y-5 border-t pt-8">
          {points.map((point) => (
            <li key={point.id} className="flex gap-3">
              <point.icon className="text-primary mt-0.5 size-4 shrink-0" aria-hidden />
              <div>
                <p className="text-sm font-medium">{login.points[point.id].title}</p>
                <p className="text-muted-foreground mt-0.5 text-sm leading-relaxed">
                  {login.points[point.id].detail}
                </p>
              </div>
            </li>
          ))}
        </ul>
      </aside>

      <main className="relative flex items-center justify-center px-4 py-10 sm:px-6">
        {/* There is no header on this screen, so the two display preferences take a corner of
            their own. Language belongs here rather than only behind the sign-in: somebody who
            cannot read the prompt cannot reach a settings page on the other side of it. */}
        <div className="absolute top-4 right-4 flex items-center gap-1.5 sm:top-6 sm:right-6">
          <LanguageToggle />
          <ThemeToggle />
        </div>

        <div className="w-full max-w-sm">
          <div className="mb-6 flex items-center gap-2.5 lg:hidden">
            <BrandMark className="size-7" />
            <span className="text-sm font-semibold tracking-tight">{productName}</span>
          </div>

          <form onSubmit={onSubmit} noValidate>
            <Card>
              <CardHeader>
                <h1 className="font-heading text-lg leading-snug font-medium">
                  {login.heading}
                </h1>
                <p className="text-muted-foreground text-sm">{login.subheading}</p>
              </CardHeader>

              <CardContent className="space-y-4">
                <div className="space-y-1.5">
                  <Label htmlFor="session-email">{login.emailLabel}</Label>

                  <Input
                    id="session-email"
                    ref={emailField}
                    type="email"
                    value={email}
                    autoFocus={!email}
                    autoComplete="username"
                    spellCheck={false}
                    className="h-10"
                    aria-invalid={error ? true : undefined}
                    onChange={(event) => {
                      setEmail(event.target.value)
                      if (error) setError(null)
                    }}
                  />
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="session-password">{login.passwordLabel}</Label>

                  <Input
                    id="session-password"
                    ref={passwordField}
                    type="password"
                    value={password}
                    autoFocus={Boolean(email)}
                    autoComplete="current-password"
                    className="h-10"
                    aria-invalid={error ? true : undefined}
                    aria-describedby={error ? 'session-error' : undefined}
                    onChange={(event) => {
                      setPassword(event.target.value)
                      if (error) setError(null)
                    }}
                  />
                </div>

                {/* One message for both fields and for the refusal, because the server answers the
                    three ways a sign-in can fail with one sentence and saying more here would undo
                    that. */}
                {error && (
                  <p id="session-error" role="alert" className="text-alarm-ink text-xs">
                    {error}
                  </p>
                )}

                <p className="text-muted-foreground text-xs leading-relaxed">
                  {login.ownership}
                </p>

                {/* There is no self-service reset yet: an Admin issues the link. Said here because
                    this is where somebody who forgot finds out. */}
                <p className="text-muted-foreground text-xs leading-relaxed">{login.forgot}</p>
              </CardContent>

              <CardFooter>
                <Button type="submit" size="lg" className="h-11 w-full" disabled={pending}>
                  {pending ? login.submitting : login.submit}
                </Button>
              </CardFooter>
            </Card>
          </form>
        </div>
      </main>
    </div>
  )
}
