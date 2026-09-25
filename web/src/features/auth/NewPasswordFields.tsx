import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useT } from '@/lib/i18n'

import type { PasswordProblem } from './passwordRules'

/**
 * A new password and its repeat, with the rule written under the first field before anything is
 * typed. Used by the three places a password is chosen — accepting an invitation, completing a
 * reset, changing one's own — so the rule reads the same in all of them.
 */
export function NewPasswordFields({
  id,
  password,
  repeat,
  problem,
  passwordLabel,
  repeatLabel,
  onPassword,
  onRepeat,
}: {
  id: string
  password: string
  repeat: string
  /** Shown only once the form has been submitted, so nobody is corrected mid-word. */
  problem: PasswordProblem | null
  passwordLabel: string
  repeatLabel: string
  onPassword: (value: string) => void
  onRepeat: (value: string) => void
}) {
  const { passwordRules } = useT()
  const onFirst = problem === 'tooShort' || problem === 'tooLong'

  return (
    <>
      <div className="space-y-1.5">
        <Label htmlFor={`${id}-password`}>{passwordLabel}</Label>
        <Input
          id={`${id}-password`}
          type="password"
          value={password}
          autoComplete="new-password"
          className="h-10"
          aria-invalid={onFirst ? true : undefined}
          aria-describedby={`${id}-password-hint`}
          onChange={(event) => onPassword(event.target.value)}
        />
        <p
          id={`${id}-password-hint`}
          className={onFirst ? 'text-alarm-ink text-xs' : 'text-muted-foreground text-xs'}
        >
          {onFirst ? passwordRules[problem] : passwordRules.hint}
        </p>
      </div>

      <div className="space-y-1.5">
        <Label htmlFor={`${id}-repeat`}>{repeatLabel}</Label>
        <Input
          id={`${id}-repeat`}
          type="password"
          value={repeat}
          autoComplete="new-password"
          className="h-10"
          aria-invalid={problem === 'mismatch' ? true : undefined}
          aria-describedby={problem === 'mismatch' ? `${id}-repeat-error` : undefined}
          onChange={(event) => onRepeat(event.target.value)}
        />
        {problem === 'mismatch' && (
          <p id={`${id}-repeat-error`} className="text-alarm-ink text-xs">
            {passwordRules.mismatch}
          </p>
        )}
      </div>
    </>
  )
}
