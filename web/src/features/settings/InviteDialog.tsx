import { useMutation } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'

import { ApiError } from '@/api/client'
import { organizationApi } from '@/api/endpoints'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useT } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { userRoles, type InvitationIssued, type UserRole } from '@/types/api'

/**
 * An address and a role — everything else about the account is the invitee's to choose.
 *
 * The role is three tiles with their meaning written on them rather than a dropdown of three bare
 * words: what an Engineer may do is exactly the question an Admin has at this moment.
 */
export function InviteDialog({
  onIssued,
  onCancel,
}: {
  onIssued: (issued: InvitationIssued) => void
  onCancel: () => void
}) {
  const { settings, labels } = useT()
  const t = settings.members.inviteDialog

  const [email, setEmail] = useState('')
  // The least a new account can do. Raising it is one click here or later on the member's row.
  const [role, setRole] = useState<UserRole>('Viewer')
  const [error, setError] = useState<string | null>(null)

  const invite = useMutation({
    mutationFn: () => organizationApi.invite(email.trim(), role),
    onSuccess: onIssued,
    // The address's shape is checked before sending, so a 400 left over is the one the server
    // alone can know: the address already has an account, here or in another organisation — and
    // which one is not this Admin's to learn.
    onError: (cause: Error) =>
      setError(cause instanceof ApiError && cause.status === 400 ? t.hasAccount : cause.message),
  })

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!email.trim()) {
      setError(t.emailRequired)

      return
    }

    if (!looksLikeAnAddress(email.trim())) {
      setError(t.emailInvalid)

      return
    }

    setError(null)
    invite.mutate()
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onCancel()}>
      <DialogContent className="sm:max-w-md">
        <form onSubmit={onSubmit} noValidate className="grid gap-4">
          <DialogHeader>
            <DialogTitle>{t.title}</DialogTitle>
            <DialogDescription>{t.description}</DialogDescription>
          </DialogHeader>

          <div className="space-y-1.5">
            <Label htmlFor="invite-email">{t.email}</Label>
            <Input
              id="invite-email"
              type="email"
              value={email}
              autoFocus
              autoComplete="off"
              spellCheck={false}
              maxLength={256}
              aria-invalid={error ? true : undefined}
              aria-describedby={error ? 'invite-email-error' : undefined}
              onChange={(event) => {
                setEmail(event.target.value)
                if (error) setError(null)
              }}
            />
            {error && (
              <p id="invite-email-error" role="alert" className="text-alarm-ink text-xs">
                {error}
              </p>
            )}
          </div>

          <fieldset className="space-y-1.5">
            <legend className="mb-1.5 text-sm font-medium">{t.role}</legend>

            <div className="grid gap-2">
              {userRoles.map((option) => {
                const selected = role === option

                return (
                  <label
                    key={option}
                    className={cn(
                      'has-focus-visible:ring-ring/50 flex cursor-pointer flex-col gap-0.5 rounded-lg border px-3 py-2 transition-colors has-focus-visible:ring-[3px]',
                      selected ? 'border-primary bg-primary/5' : 'border-border hover:bg-muted/60',
                    )}
                  >
                    <input
                      type="radio"
                      name="invite-role"
                      value={option}
                      checked={selected}
                      onChange={() => setRole(option)}
                      className="sr-only"
                    />
                    <span className="text-sm font-medium">{labels.role[option]}</span>
                    <span className="text-muted-foreground text-xs">{labels.roleDetail[option]}</span>
                  </label>
                )
              })}
            </div>
          </fieldset>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onCancel}>
              {t.cancel}
            </Button>
            <Button type="submit" disabled={invite.isPending}>
              {invite.isPending ? t.submitting : t.submit}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

/** The server's own test, roughly: one @ with something on either side and no spaces. */
function looksLikeAnAddress(value: string): boolean {
  return /^[^\s@]+@[^\s@]+$/.test(value)
}
