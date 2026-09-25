import {
  Building2Icon,
  LogOutIcon,
  MailIcon,
  MonitorIcon,
  MoonIcon,
  SunIcon,
} from 'lucide-react'
import { useTheme } from 'next-themes'
import { useState, type FormEvent, type ReactNode } from 'react'
import { toast } from 'sonner'

import { ApiError } from '@/api/client'
import { authApi } from '@/api/endpoints'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAuth } from '@/features/auth/AuthProvider'
import { NewPasswordFields } from '@/features/auth/NewPasswordFields'
import { checkNewPassword, type PasswordProblem } from '@/features/auth/passwordRules'
import type { UserRole } from '@/features/auth/session'
import { languageName, languages, useLanguage, useT } from '@/lib/i18n'
import { cn } from '@/lib/utils'

/**
 * The account, and the things on this console that are the reader's own.
 *
 * The identity card used to say that it was not an account — a name the browser made up, with no
 * password behind it. Since Adım 16 it is one, checked by the server on every request, and since
 * Adım 16.5 its password can be changed here. What the reader cannot change here is their role:
 * that belongs to the organisation's Admins, and the card says so rather than offering a control
 * that would be refused.
 */

const themeOptions = [
  { value: 'light', icon: SunIcon },
  { value: 'dark', icon: MoonIcon },
  { value: 'system', icon: MonitorIcon },
] as const

export function ProfilePage() {
  const { user, organization, signOut } = useAuth()
  const { profile } = useT()

  // RequireAuth is the only route that renders this, so a null user is unreachable — but the
  // context is typed for the login screen too, where it is genuinely null.
  if (!user) return null

  return (
    <div className="max-w-3xl space-y-4">
      <div>
        <h2 className="text-lg font-medium tracking-tight">{profile.title}</h2>
      </div>

      <Identity
        name={user.name}
        initials={user.initials}
        email={user.email}
        role={user.role}
        organizationName={organization?.name}
        onSignOut={() => void signOut()}
      />

      <PasswordCard email={user.email} />
      <Appearance />
      <LanguageCard />
    </div>
  )
}

function Identity({
  name,
  initials,
  email,
  role,
  organizationName,
  onSignOut,
}: {
  name: string
  initials: string
  email: string
  role: UserRole
  organizationName: string | undefined
  onSignOut: () => void
}) {
  const { common, profile, labels } = useT()
  const { identity } = profile

  return (
    <Card>
      <CardHeader>
        <CardTitle>{identity.title}</CardTitle>
      </CardHeader>

      <CardContent className="space-y-4">
        <div className="flex flex-wrap items-center gap-4">
          <Avatar size="lg" className="shrink-0">
            <AvatarFallback className="bg-primary text-primary-foreground text-sm font-medium">
              {initials}
            </AvatarFallback>
          </Avatar>

          <div className="min-w-0 flex-1">
            <p className="flex items-center gap-2 text-base font-medium">
              <span className="truncate" title={name}>
                {name}
              </span>
              <Badge variant="secondary">{labels.role[role]}</Badge>
            </p>
            <p className="text-muted-foreground mt-0.5 flex items-center gap-1.5 text-sm">
              <MailIcon className="size-3.5 shrink-0" aria-hidden />
              <span className="truncate">{email}</span>
            </p>
            {organizationName && (
              <p className="text-muted-foreground mt-0.5 flex items-center gap-1.5 text-sm">
                <Building2Icon className="size-3.5 shrink-0" aria-hidden />
                <span className="truncate">{organizationName}</span>
              </p>
            )}
          </div>

          {/* Sign out is the only control here: the name, the address and the role are the
              organisation's record of this person, changed by its Admins rather than in place. */}
          <Button variant="outline" className="shrink-0" onClick={onSignOut}>
            <LogOutIcon aria-hidden />
            {common.signOut}
          </Button>
        </div>

        <p className="text-muted-foreground text-xs leading-relaxed">{labels.roleDetail[role]}</p>

        {/* Not repeated with the organisation's name, which is already on the row above — what is
            missing there is what membership means. */}
        <p className="text-muted-foreground text-xs leading-relaxed">{identity.ownership}</p>
      </CardContent>
    </Card>
  )
}

/**
 * One's own password, with the current one as proof. The server ends every other session of the
 * account and renews this one, so the reader stays signed in here and nowhere else.
 */
function PasswordCard({ email }: { email: string }) {
  const { profile, common } = useT()
  const text = profile.password

  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [repeat, setRepeat] = useState('')
  const [submitted, setSubmitted] = useState(false)
  const [pending, setPending] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const [currentWrong, setCurrentWrong] = useState(false)

  const currentMissing = submitted && !current
  const problem: PasswordProblem | null = submitted ? checkNewPassword(next, repeat) : null
  const same = submitted && Boolean(current) && current === next

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitted(true)
    setFailure(null)
    setCurrentWrong(false)

    if (!current || checkNewPassword(next, repeat) || current === next) return

    setPending(true)

    try {
      await authApi.changePassword(current, next)

      setCurrent('')
      setNext('')
      setRepeat('')
      setSubmitted(false)
      toast.success(text.changed)
    } catch (cause) {
      // Everything the server could say about the new password was checked above, so a 400 is
      // its answer about the current one.
      if (cause instanceof ApiError && cause.status === 400) setCurrentWrong(true)
      else if (cause instanceof ApiError && cause.status === 429) setFailure(common.tooMany)
      else setFailure(cause instanceof ApiError ? common.serverError : common.unreachable)
    } finally {
      setPending(false)
    }
  }

  const currentError = currentMissing ? text.currentRequired : currentWrong ? text.wrongCurrent : null

  return (
    <Card>
      <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
        <CardHeader>
          <CardTitle>{text.title}</CardTitle>
          <CardDescription>{text.description}</CardDescription>
        </CardHeader>

        <CardContent className="max-w-sm space-y-4">
          {/* For password managers, which otherwise file the new password against nothing. */}
          <input type="email" value={email} autoComplete="username" readOnly hidden />

          <div className="space-y-1.5">
            <Label htmlFor="profile-current">{text.current}</Label>
            <Input
              id="profile-current"
              type="password"
              value={current}
              autoComplete="current-password"
              className="h-10"
              aria-invalid={currentError ? true : undefined}
              aria-describedby={currentError ? 'profile-current-error' : undefined}
              onChange={(event) => {
                setCurrent(event.target.value)
                setCurrentWrong(false)
              }}
            />
            {currentError && (
              <p id="profile-current-error" className="text-alarm-ink text-xs">
                {currentError}
              </p>
            )}
          </div>

          <NewPasswordFields
            id="profile"
            password={next}
            repeat={repeat}
            problem={problem}
            passwordLabel={text.next}
            repeatLabel={text.repeat}
            onPassword={setNext}
            onRepeat={setRepeat}
          />

          {same && !problem && <p className="text-alarm-ink text-xs">{text.same}</p>}

          {failure && (
            <p role="alert" className="text-alarm-ink text-xs">
              {failure}
            </p>
          )}
        </CardContent>

        <CardFooter>
          <Button type="submit" disabled={pending}>
            {pending ? text.submitting : text.submit}
          </Button>
        </CardFooter>
      </form>
    </Card>
  )
}

/**
 * One tile layout, two settings.
 *
 * Real radios, hidden but focusable, with the tile as their label. A group of divs with onClick
 * would take the keyboard away from the settings on this page that actually work, and arrow-key
 * movement between options comes free from the radio group.
 *
 * Extracted when the second consumer arrived rather than in anticipation of it — the appearance
 * card owned this markup alone until language needed exactly the same thing.
 */
function Choice<T extends string>({
  name,
  legend,
  value,
  options,
  onChange,
}: {
  name: string
  legend: string
  value: T | undefined
  options: { value: T; label: string; detail: string; icon?: ReactNode }[]
  onChange: (value: T) => void
}) {
  const { common } = useT()

  return (
    <fieldset>
      <legend className="sr-only">{legend}</legend>

      <div className="grid gap-2 sm:grid-cols-3">
        {options.map((option) => {
          const selected = value === option.value

          return (
            <label
              key={option.value}
              className={cn(
                'has-focus-visible:ring-ring/50 relative flex cursor-pointer flex-col gap-1 rounded-lg border p-3 transition-colors has-focus-visible:ring-[3px]',
                selected ? 'border-primary bg-primary/5' : 'border-border hover:bg-muted/60',
              )}
            >
              <input
                type="radio"
                name={name}
                value={option.value}
                checked={selected}
                onChange={() => onChange(option.value)}
                className="sr-only"
              />

              <span className="flex items-center gap-2 text-sm font-medium">
                {option.icon}
                {option.label}
                {/* Selection is not left to the border colour alone. */}
                {selected && (
                  <span className="text-primary ml-auto text-xs font-medium">
                    {common.selected}
                  </span>
                )}
              </span>

              <span className="text-muted-foreground text-xs">{option.detail}</span>
            </label>
          )
        })}
      </div>
    </fieldset>
  )
}

function Appearance() {
  const { theme, setTheme, resolvedTheme } = useTheme()
  const text = useT().theme

  return (
    <Card>
      <CardHeader>
        <CardTitle>{text.title}</CardTitle>
        <CardDescription>{text.description}</CardDescription>
      </CardHeader>

      <CardContent className="space-y-2">
        <Choice
          name="theme"
          legend={text.legend}
          value={theme as 'light' | 'dark' | 'system' | undefined}
          onChange={setTheme}
          options={themeOptions.map((option) => ({
            value: option.value,
            label: text.options[option.value].label,
            detail: text.options[option.value].detail,
            icon: <option.icon className="size-4 shrink-0" aria-hidden />,
          }))}
        />

        {/* "System" is the default, and on its own it does not tell the operator which palette
            they are actually going to get. resolvedTheme does, and is only read once it exists —
            narrowed to the two it can be, because the word it supplies has to be translated and
            a raw value would come out in English on a Turkish screen. */}
        {theme === 'system' && (resolvedTheme === 'light' || resolvedTheme === 'dark') && (
          <p className="text-muted-foreground text-xs" aria-live="polite">
            {text.resolved(text.palette[resolvedTheme])}
          </p>
        )}
      </CardContent>
    </Card>
  )
}

/**
 * The same tiles, plus one sentence the appearance card has no equivalent of.
 *
 * Where the translation stops is a fact about the product rather than a caveat to bury. An
 * analysis's reasoning and a provider's error message arrive in the language they were written
 * in, and are shown that way. Said here, once, on the screen where the choice is made — rather
 * than as a footnote beside every English paragraph on an otherwise Turkish screen.
 */
function LanguageCard() {
  const { language, setLanguage } = useLanguage()
  const text = useT().language

  return (
    <Card>
      <CardHeader>
        <CardTitle>{text.title}</CardTitle>
        <CardDescription>{text.description}</CardDescription>
      </CardHeader>

      <CardContent className="space-y-3">
        <Choice
          name="language"
          legend={text.legend}
          value={language}
          onChange={setLanguage}
          options={languages.map((option) => ({
            value: option,
            label: languageName[option],
            detail: text.options[option],
          }))}
        />

        <p className="text-muted-foreground text-xs leading-relaxed">{text.passthrough}</p>
      </CardContent>
    </Card>
  )
}
