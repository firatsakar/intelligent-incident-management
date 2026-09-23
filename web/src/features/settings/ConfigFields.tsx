import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useT } from '@/lib/i18n'
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
  const t = useT().settings.config

  return (
    <div className="space-y-3">
      {fields.map((field) => {
        const alreadySet = field.secret && stored?.[field.key] === maskedValue
        // A declared field's hint is in the dictionary; a stray carries its own, because what
        // there is to say about it is that the form does not know its shape.
        const hint = field.id ? t.hints[field.id] : field.hint

        return (
          <div key={field.key} className="space-y-1.5">
            <Label htmlFor={field.key}>
              {/* A stray has no id, so it renders under its own config key — there is nothing
                  else to call a setting this form was never taught about. */}
              {field.id ? t.fields[field.id] : field.key}
              {field.required && <span className="text-destructive ml-1">*</span>}
            </Label>

            <Input
              id={field.key}
              type={field.secret ? 'password' : 'text'}
              value={values[field.key] ?? ''}
              placeholder={alreadySet ? t.keepCurrent : field.placeholder}
              onChange={(event) => onChange(field.key, event.target.value)}
              autoComplete="off"
            />

            {hint && <p className="text-muted-foreground text-xs">{hint}</p>}
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

/**
 * The schema, plus whatever else is actually stored on the record being edited.
 *
 * `buildConfig` only emits keys it was given a field for, and the config bag is free-form on both
 * services — `Header:<name>` is how a customer passes a signing token, `InitialLookbackMinutes` is
 * read by the Seq connector — so no static list can enumerate it. A stored key with no field is
 * therefore a key the form deletes on save, and for a masked one that means deleting a credential
 * nobody can retype, because nobody is allowed to read it back.
 *
 * Appending the strays as real fields fixes that without touching the masking rule: they go through
 * exactly the same audited path as every declared field, and a stray that reads back as the mask is
 * marked secret, so `buildConfig` sends the mask and the server restores the value.
 *
 * Pure, so callers keep it inside their own `useMemo` rather than this file owning a hook.
 */
export function withStrayFields(
  declared: ConfigField[],
  stored: Record<string, string> | null,
  hint: string,
): ConfigField[] {
  if (!stored) return declared

  const strays = Object.keys(stored)
    .filter((key) => !declared.some((field) => field.key === key))
    .map<ConfigField>((key) => ({
      key,
      secret: stored[key] === maskedValue,
      hint,
    }))

  return strays.length > 0 ? [...declared, ...strays] : declared
}
