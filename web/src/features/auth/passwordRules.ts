/**
 * The server's rule for a new password (`PasswordRules.NewPassword`), checked here first so a form
 * can say what is wrong next to the field instead of after a round trip. The server checks it
 * again; this only decides when to say so.
 */
export const passwordMinLength = 12

/** BCrypt reads at most 72 bytes. Anything longer would be silently cut, so it is refused. */
export const passwordMaxBytes = 72

export type PasswordProblem = 'tooShort' | 'tooLong' | 'mismatch'

export function checkNewPassword(password: string, repeat: string): PasswordProblem | null {
  // .length counts UTF-16 units, which is what the server's MinimumLength counts too.
  if (password.length < passwordMinLength) return 'tooShort'
  if (new TextEncoder().encode(password).length > passwordMaxBytes) return 'tooLong'
  if (password !== repeat) return 'mismatch'

  return null
}
