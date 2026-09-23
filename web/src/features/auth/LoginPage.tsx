import { Building2Icon, ListChecksIcon, RouteIcon, ScaleIcon, ShieldAlertIcon } from 'lucide-react'
import { useRef, useState, type CSSProperties, type FormEvent } from 'react'
import { Navigate, useLocation } from 'react-router-dom'

import { LanguageToggle } from '@/app/LanguageToggle'
import { ThemeToggle } from '@/app/ThemeToggle'
import { BrandMark, productName } from '@/components/BrandMark'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardFooter, CardHeader } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useT, type Dictionary } from '@/lib/i18n'

import { useAuth } from './AuthProvider'
import { defaultOrganization, organizationName, rememberedName } from './session'

/**
 * The first screen anyone sees, and the only one that is nothing but design — everywhere else the
 * data is the design. Two jobs, and they pull against each other:
 *
 *   it has to look like the product it fronts — calm, dense, cool ground, accent for identity
 *   only — because this is where the visual identity gets established;
 *   and it has to admit, in the plainest words on the page, that it checks nothing.
 *
 * So there is no password field. A field that accepts anything is worse than no field: it teaches
 * the operator that a credential was verified, and the next thing they assume is that the data
 * behind the screen is theirs alone. The only question asked is the only one that has an honest
 * answer here — what name should this session carry.
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
  const { isAuthenticated, signIn } = useAuth()
  const location = useLocation()
  const { login } = useT()

  const [name, setName] = useState(rememberedName)
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const field = useRef<HTMLInputElement>(null)

  // Where the guard turned them away from, with its query string intact.
  const from = (location.state as { from?: string } | null)?.from ?? '/'

  // One redirect for two cases — a session that was already there, and the one just created — so
  // signing in has a single exit and cannot race a second navigate.
  if (isAuthenticated) return <Navigate to={from} replace />

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const trimmed = name.trim()

    if (!trimmed) {
      // A required field, not a rejected credential. The message says what is missing and why it
      // is wanted, and never implies that something was checked.
      setError(login.nameRequired)
      field.current?.focus()

      return
    }

    setPending(true)
    await signIn(trimmed)
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
                {/* Shown, not chosen. A select holding one option is a control that cannot do
                    anything, and a list of invented organisations would be a second lie on a
                    screen whose whole point is not telling the first one. */}
                <div className="bg-muted/40 flex items-center gap-2.5 rounded-lg border px-3 py-2">
                  <Building2Icon className="text-muted-foreground size-4 shrink-0" aria-hidden />
                  <div className="min-w-0">
                    <p className="text-muted-foreground text-[0.6875rem] font-medium tracking-wider uppercase">
                      {login.organisation}
                    </p>
                    <p className="truncate text-sm font-medium">
                      {organizationName(defaultOrganization)}
                    </p>
                  </div>
                </div>

                <p className="text-muted-foreground text-xs leading-relaxed">
                  {login.ownership}
                </p>

                <div className="space-y-1.5">
                  <Label htmlFor="session-name">{login.nameLabel}</Label>

                  <Input
                    id="session-name"
                    ref={field}
                    value={name}
                    autoFocus
                    autoComplete="name"
                    spellCheck={false}
                    className="h-10"
                    aria-invalid={error ? true : undefined}
                    aria-describedby={error ? 'session-name-hint session-name-error' : 'session-name-hint'}
                    onChange={(event) => {
                      setName(event.target.value)
                      if (error) setError(null)
                    }}
                  />

                  <p id="session-name-hint" className="text-muted-foreground text-xs">
                    {login.nameHint}
                  </p>

                  {error && (
                    <p id="session-name-error" role="alert" className="text-alarm-ink text-xs">
                      {error}
                    </p>
                  )}
                </div>

                {/* caution, not alarm: the system has not reached a verdict here, it is missing a
                    whole faculty — and this is the one thing on the screen the reader must not
                    skip. Icon and words carry it as well as the tint does. */}
                <div className="bg-caution text-caution-foreground border-caution-border rounded-lg border px-3 py-2.5">
                  <p className="flex items-center gap-2 text-sm font-medium">
                    <ShieldAlertIcon className="size-4 shrink-0" aria-hidden />
                    {login.noPasswordTitle}
                  </p>
                  <p className="mt-1 text-xs leading-relaxed">{login.noPassword}</p>
                </div>
              </CardContent>

              <CardFooter>
                <Button type="submit" size="lg" className="h-11 w-full" disabled={pending}>
                  {login.submit}
                </Button>
              </CardFooter>
            </Card>
          </form>
        </div>
      </main>
    </div>
  )
}
