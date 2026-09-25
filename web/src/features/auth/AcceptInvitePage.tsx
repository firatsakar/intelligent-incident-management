import { useQuery } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'

import { authApi } from '@/api/endpoints'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardFooter, CardHeader } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { formatDateTime } from '@/lib/format'
import { useT } from '@/lib/i18n'
import type { UserRole } from '@/types/api'

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
 * Where an invitation email lands. The organisation, the address and the role come from the
 * invitation and are shown rather than asked: the person holding the link chooses only what to be
 * called and a password, and accepting signs them in.
 */
export function AcceptInvitePage() {
  const { token = '' } = useParams()
  const text = useT().common

  const preview = useQuery({
    queryKey: ['invitation-preview', token],
    queryFn: () => authApi.invitation(token),
    // A dead link stays dead, and a retry would spend the sign-in rate limit on it.
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
        <AcceptForm token={token} {...preview.data} />
      )}
    </PublicShell>
  )
}

function AcceptForm({
  token,
  organizationName,
  email,
  role,
  expiresAt,
}: {
  token: string
  organizationName: string
  email: string
  role: UserRole
  expiresAt: string
}) {
  const { invite, common, labels } = useT()
  const { user, adopt } = useAuth()
  const navigate = useNavigate()

  const [name, setName] = useState('')
  const [password, setPassword] = useState('')
  const [repeat, setRepeat] = useState('')
  const [submitted, setSubmitted] = useState(false)
  const [pending, setPending] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const [dead, setDead] = useState(false)

  const nameMissing = submitted && !name.trim()
  const problem: PasswordProblem | null = submitted ? checkNewPassword(password, repeat) : null

  if (dead) return <DeadLink />

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitted(true)
    setFailure(null)

    if (!name.trim() || checkNewPassword(password, repeat)) return

    setPending(true)

    try {
      adopt(await authApi.acceptInvitation(token, name.trim(), password))
      navigate('/', { replace: true })
    } catch (cause) {
      setPending(false)

      // Used or revoked between opening the page and pressing the button.
      if (isDeadLink(cause)) setDead(true)
      else setFailure(describeLinkFailure(cause, common))
    }
  }

  return (
    <form onSubmit={onSubmit} noValidate>
      <Card>
        <CardHeader>
          <h1 className="font-heading text-lg leading-snug font-medium">
            {invite.title(organizationName)}
          </h1>
          <p className="text-muted-foreground text-sm">{invite.subtitle}</p>
        </CardHeader>

        <CardContent className="space-y-4">
          {/* What the invitation decided, as facts rather than as fields: none of it is the
              reader's to change here. */}
          <dl className="bg-muted/60 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 rounded-lg px-3 py-2.5 text-sm">
            <dt className="text-muted-foreground">{invite.email}</dt>
            <dd className="min-w-0 truncate font-medium" title={email}>
              {email}
            </dd>
            <dt className="text-muted-foreground">{invite.role}</dt>
            <dd className="font-medium">{labels.role[role]}</dd>
          </dl>

          {/* For password managers, which otherwise file the new password against nothing. */}
          <input type="email" value={email} autoComplete="username" readOnly hidden />

          <div className="space-y-1.5">
            <Label htmlFor="invite-name">{invite.name}</Label>
            <Input
              id="invite-name"
              value={name}
              autoFocus
              autoComplete="name"
              maxLength={128}
              className="h-10"
              aria-invalid={nameMissing ? true : undefined}
              aria-describedby={nameMissing ? 'invite-name-error' : 'invite-name-hint'}
              onChange={(event) => setName(event.target.value)}
            />
            {nameMissing ? (
              <p id="invite-name-error" className="text-alarm-ink text-xs">
                {invite.nameRequired}
              </p>
            ) : (
              <p id="invite-name-hint" className="text-muted-foreground text-xs">
                {invite.nameHint}
              </p>
            )}
          </div>

          <NewPasswordFields
            id="invite"
            password={password}
            repeat={repeat}
            problem={problem}
            passwordLabel={invite.password}
            repeatLabel={invite.repeat}
            onPassword={setPassword}
            onRepeat={setRepeat}
          />

          {failure && (
            <p role="alert" className="text-alarm-ink text-xs">
              {failure}
            </p>
          )}

          {/* Accepting replaces whatever session this browser holds; said before, not after. */}
          {user && (
            <p className="text-muted-foreground text-xs leading-relaxed">
              {invite.signedInAs(user.name)}
            </p>
          )}

          <p className="text-muted-foreground text-xs">
            {invite.expires(formatDateTime(expiresAt))}
          </p>
        </CardContent>

        <CardFooter>
          <Button type="submit" size="lg" className="h-11 w-full" disabled={pending}>
            {pending ? invite.submitting : invite.submit}
          </Button>
        </CardFooter>
      </Card>
    </form>
  )
}
