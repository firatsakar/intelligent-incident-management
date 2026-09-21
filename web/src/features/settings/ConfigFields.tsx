import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { maskedValue } from '@/types/api'

import type { ConfigField } from './configSchema'

/**
 * The config editor, and the one place the masking rule shows up in the UI.
 *
 * A secret reads back as "***" and is never shown. The field renders empty with a note saying
 * blank keeps the stored value, and the form submits the mask unchanged for it — the server then
 * restores what it has. That keeps the round trip lossless without the browser ever holding a
 * credential it has no business seeing.
 */
export function ConfigFields({
  fields,
  values,
  stored,
  onChange,
}: {
  fields: ConfigField[]
  values: Record<string, string>
  /** What the API returned, so a secret can be recognised as already set. */
  stored: Record<string, string> | null
  onChange: (key: string, value: string) => void
}) {
  return (
    <div className="space-y-3">
      {fields.map((field) => {
        const alreadySet = field.secret && stored?.[field.key] === maskedValue

        return (
          <div key={field.key} className="space-y-1.5">
            <Label htmlFor={field.key}>
              {field.label}
              {field.required && <span className="text-destructive ml-1">*</span>}
            </Label>

            <Input
              id={field.key}
              type={field.secret ? 'password' : 'text'}
              value={values[field.key] ?? ''}
              placeholder={alreadySet ? 'Leave blank to keep the current value' : field.placeholder}
              onChange={(event) => onChange(field.key, event.target.value)}
              autoComplete="off"
            />

            {field.hint && <p className="text-muted-foreground text-xs">{field.hint}</p>}
          </div>
        )
      })}
    </div>
  )
}

/**
 * Builds the payload. A secret the operator left blank is sent back as the mask, which the
 * server reads as "keep what you have"; a secret they typed into is sent for real. Anything
 * blank and not a secret is dropped, because the config is replaced wholesale and an empty
 * string is not a setting.
 */
export function buildConfig(
  fields: ConfigField[],
  values: Record<string, string>,
  stored: Record<string, string> | null,
): Record<string, string> {
  const config: Record<string, string> = {}

  for (const field of fields) {
    const typed = values[field.key]?.trim() ?? ''

    if (typed) {
      config[field.key] = typed
      continue
    }

    if (field.secret && stored?.[field.key] === maskedValue) {
      config[field.key] = maskedValue
    }
  }

  return config
}
