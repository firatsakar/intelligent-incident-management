import { useQuery } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'

import { authApi } from '@/api/endpoints'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardFooter, CardHeader } from '@/components/ui/card'
import { formatDateTime } from '@/lib/format'
import { useT } from '@/lib/i18n'

import { useAuth } from './AuthProvider'
import { NewPasswordFields } from './NewPasswordFields'
import {
  CheckingLink,
  DeadLink,
  LinkUnavailable,
  describeLinkFailure,
  isDeadLink,
} from './oneTimeLink'
import { checkNewPassword, type PasswordProblem } from './passwordRules'
import { PublicShell } from './PublicShell'

/**
 * Where a reset link lands. An Admin issued it; the account's owner sets the password, which ends
 * every session the account had and signs this browser in with the new one.
 */
export function ResetPasswordPage() {
  const { token = '' } = useParams()
  const text = useT().common

  const preview = useQuery({
    queryKey: ['password-reset-preview', token],
    queryFn: () => authApi.passwordReset(token),
    retry: false,
    staleTime: Infinity,
  })

  return (
    <PublicShell>
      {preview.isPending ? (
        <CheckingLink />
      ) : preview.isError ? (
        isDeadLink(preview.error) ? (
          <DeadLink />
        ) : (
          <LinkUnavailable
            message={describeLinkFailure(preview.error, text)}
            onRetry={() => void preview.refetch()}
          />
        )
      ) : (
        <ResetForm token={token} {...preview.data} />
      )}
    </PublicShell>
  )
}

function ResetForm({
  token,
  email,
  displayName,
  expiresAt,
}: {
  token: string
  email: string
  displayName: string
  expiresAt: string
}) {
  const { reset, common } = useT()
  const { adopt } = useAuth()
  const navigate = useNavigate()

  const [password, setPassword] = useState('')
  const [repeat, setRepeat] = useState('')
  const [submitted, setSubmitted] = useState(false)
  const [pending, setPending] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const [dead, setDead] = useState(false)

  const problem: PasswordProblem | null = submitted ? checkNewPassword(password, repeat) : null

  if (dead) return <DeadLink />

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitted(true)
    setFailure(null)

    if (checkNewPassword(password, repeat)) return

    setPending(true)

    try {
      adopt(await authApi.completePasswordReset(token, password))
      navigate('/', { replace: true })
    } catch (cause) {
      setPending(false)

      if (isDeadLink(cause)) setDead(true)
      else setFailure(describeLinkFailure(cause, common))
    }
  }

  return (
    <form onSubmit={onSubmit} noValidate>
      <Card>
        <CardHeader>
          <h1 className="font-heading text-lg leading-snug font-medium">{reset.title}</h1>
          <p className="text-muted-foreground min-w-0 truncate text-sm" title={email}>
            {reset.subtitle(displayName, email)}
          </p>
        </CardHeader>

        <CardContent className="space-y-4">
          {/* The username field is for password managers: without it they store the new password
              against nothing, and the next sign-in finds no match. */}
          <input type="email" value={email} autoComplete="username" readOnly hidden />

          <NewPasswordFields
            id="reset"
            password={password}
            repeat={repeat}
            problem={problem}
            passwordLabel={reset.password}
            repeatLabel={reset.repeat}
            onPassword={setPassword}
            onRepeat={setRepeat}
          />

          {failure && (
            <p role="alert" className="text-alarm-ink text-xs">
              {failure}
            </p>
          )}

          <p className="text-muted-foreground text-xs leading-relaxed">{reset.sessions}</p>
          <p className="text-muted-foreground text-xs">
            {reset.expires(formatDateTime(expiresAt))}
          </p>
        </CardContent>

        <CardFooter>
          <Button type="submit" size="lg" className="h-11 w-full" disabled={pending}>
            {pending ? reset.submitting : reset.submit}
          </Button>
        </CardFooter>
      </Card>
    </form>
  )
}
