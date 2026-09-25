import { useQuery } from '@tanstack/react-query'
import { useState, type FormEvent, type ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'

import { ApiError } from '@/api/client'
import { authApi } from '@/api/endpoints'
import { Button, buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardFooter, CardHeader } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useT } from '@/lib/i18n'

import { useAuth } from './AuthProvider'
import { NewPasswordFields } from './NewPasswordFields'
import { CheckingLink } from './oneTimeLink'
import { checkNewPassword, type PasswordProblem } from './passwordRules'
import { PublicShell } from './PublicShell'

/**
 * The first screen of a fresh installation (Adım 25): nobody has signed in yet, so whoever set the
 * server up creates the organisation and its first Admin here.
 *
 * It asks for the one-time code the identity service printed in its log when it started. That is
 * the whole of the security: reaching this page proves nothing on a server exposed to the internet,
 * reading its log does. There is no default password anywhere to change afterwards.
 */
export function SetupPage() {
  const status = useQuery({
    queryKey: ['setup-status'],
    queryFn: authApi.setupStatus,
    retry: false,
  })

  return (
    <PublicShell>
      {status.isPending ? <CheckingLink /> : status.data?.required ? <SetupForm /> : <AlreadySetUp />}
    </PublicShell>
  )
}

function AlreadySetUp() {
  const t = useT().setup

  return (
    <Card>
      <CardHeader>
        <h1 className="font-heading text-lg leading-snug font-medium">{t.doneTitle}</h1>
        <p className="text-muted-foreground text-sm leading-relaxed">{t.done}</p>
      </CardHeader>
      <CardFooter>
        <Link to="/login" replace className={buttonVariants({ variant: 'outline', className: 'w-full' })}>
          {t.toSignIn}
        </Link>
      </CardFooter>
    </Card>
  )
}

const looksLikeAnAddress = (value: string) => /^[^\s@]+@[^\s@]+$/.test(value)

function SetupForm() {
  const { setup: t, common } = useT()
  const { adopt } = useAuth()
  const navigate = useNavigate()

  const [code, setCode] = useState('')
  const [organization, setOrganization] = useState('')
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [repeat, setRepeat] = useState('')
  const [submitted, setSubmitted] = useState(false)
  const [pending, setPending] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)

  const missing = {
    code: submitted && !code.trim(),
    organization: submitted && !organization.trim(),
    name: submitted && !name.trim(),
    email: submitted && !looksLikeAnAddress(email.trim()),
  }
  const problem: PasswordProblem | null = submitted ? checkNewPassword(password, repeat) : null

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitted(true)
    setFailure(null)

    if (
      !code.trim() ||
      !organization.trim() ||
      !name.trim() ||
      !looksLikeAnAddress(email.trim()) ||
      checkNewPassword(password, repeat)
    )
      return

    setPending(true)

    try {
      adopt(
        await authApi.completeSetup({
          setupCode: code.trim(),
          organizationName: organization.trim(),
          displayName: name.trim(),
          email: email.trim(),
          password,
        }),
      )
      navigate('/', { replace: true })
    } catch (cause) {
      setPending(false)

      if (!(cause instanceof ApiError)) setFailure(common.unreachable)
      else if (cause.status === 404) setFailure(t.rejected)
      else if (cause.status === 429) setFailure(common.tooMany)
      else if (cause.status === 400) setFailure(cause.message)
      else setFailure(common.serverError)
    }
  }

  return (
    <form onSubmit={onSubmit} noValidate>
      <Card>
        <CardHeader>
          <h1 className="font-heading text-lg leading-snug font-medium">{t.title}</h1>
          <p className="text-muted-foreground text-sm leading-relaxed">{t.subtitle}</p>
        </CardHeader>

        <CardContent className="space-y-4">
          <Field
            id="setup-code"
            label={t.code}
            hint={t.codeHint}
            error={missing.code ? t.codeRequired : null}
          >
            <Input
              id="setup-code"
              value={code}
              autoFocus
              autoComplete="off"
              spellCheck={false}
              placeholder="XXXX-XXXX-XXXX"
              className="h-10 font-mono tracking-wider"
              aria-invalid={missing.code ? true : undefined}
              onChange={(event) => setCode(event.target.value)}
            />
          </Field>

          <Field
            id="setup-organization"
            label={t.organization}
            hint={t.organizationHint}
            error={missing.organization ? t.organizationRequired : null}
          >
            <Input
              id="setup-organization"
              value={organization}
              maxLength={128}
              autoComplete="organization"
              className="h-10"
              aria-invalid={missing.organization ? true : undefined}
              onChange={(event) => setOrganization(event.target.value)}
            />
          </Field>

          <Field id="setup-name" label={t.name} error={missing.name ? t.nameRequired : null}>
            <Input
              id="setup-name"
              value={name}
              maxLength={128}
              autoComplete="name"
              className="h-10"
              aria-invalid={missing.name ? true : undefined}
              onChange={(event) => setName(event.target.value)}
            />
          </Field>

          <Field id="setup-email" label={t.email} error={missing.email ? t.emailInvalid : null}>
            <Input
              id="setup-email"
              type="email"
              value={email}
              maxLength={256}
              autoComplete="username"
              spellCheck={false}
              className="h-10"
              aria-invalid={missing.email ? true : undefined}
              onChange={(event) => setEmail(event.target.value)}
            />
          </Field>

          <NewPasswordFields
            id="setup"
            password={password}
            repeat={repeat}
            problem={problem}
            passwordLabel={t.password}
            repeatLabel={t.repeat}
            onPassword={setPassword}
            onRepeat={setRepeat}
          />

          {failure && (
            <p role="alert" className="text-alarm-ink text-xs leading-relaxed">
              {failure}
            </p>
          )}
        </CardContent>

        <CardFooter>
          <Button type="submit" size="lg" className="h-11 w-full" disabled={pending}>
            {pending ? t.submitting : t.submit}
          </Button>
        </CardFooter>
      </Card>
    </form>
  )
}

function Field({
  id,
  label,
  hint,
  error,
  children,
}: {
  id: string
  label: string
  hint?: string
  error: string | null
  children: ReactNode
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      {children}
      {error ? (
        <p className="text-alarm-ink text-xs">{error}</p>
      ) : (
        hint && <p className="text-muted-foreground text-xs leading-relaxed">{hint}</p>
      )}
    </div>
  )
}
