import {
  Building2Icon,
  LogOutIcon,
  MonitorIcon,
  MoonIcon,
  ShieldAlertIcon,
  SunIcon,
} from 'lucide-react'
import { useTheme } from 'next-themes'
import type { ReactNode } from 'react'

import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useAuth } from '@/features/auth/AuthProvider'
import { languageName, languages, useLanguage, useT } from '@/lib/i18n'
import { cn } from '@/lib/utils'

/**
 * Who this session says you are, and the two things on this console that are genuinely yours.
 *
 * The halves of the screen are honest in opposite directions, which is the whole reason they sit
 * together. The identity is a label this browser made up and nothing checked; the appearance and
 * language settings are real preferences that really persist. Saying so on each of them, once, is
 * what stops the page reading as a settings screen where nothing works — two of the three *do*
 * work, and putting them next to the qualified one is what proves the qualification is specific
 * rather than a blanket disclaimer over the product.
 *
 * This is also the screen where a user would most reasonably expect the identity to be real, so it
 * is the screen that has to say plainly that it is not. Louder than the account menu's line, which
 * is a reminder; quieter than the login card, which is said at the moment it is acted on.
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
        <p className="text-muted-foreground mt-1 text-sm">{profile.intro}</p>
      </div>

      <Identity
        name={user.name}
        initials={user.initials}
        organizationName={organization?.name}
        onSignOut={signOut}
      />

      <Appearance />
      <LanguageCard />
    </div>
  )
}

function Identity({
  name,
  initials,
  organizationName,
  onSignOut,
}: {
  name: string
  initials: string
  organizationName: string | undefined
  onSignOut: () => void
}) {
  const { identity } = useT().profile

  return (
    <Card>
      <CardHeader>
        <CardTitle>{identity.title}</CardTitle>
        <CardDescription>{identity.description}</CardDescription>
      </CardHeader>

      <CardContent className="space-y-4">
        <div className="flex flex-wrap items-center gap-4">
          <Avatar size="lg" className="shrink-0">
            <AvatarFallback className="bg-primary text-primary-foreground text-sm font-medium">
              {initials}
            </AvatarFallback>
          </Avatar>

          <div className="min-w-0 flex-1">
            <p className="truncate text-base font-medium" title={name}>
              {name}
            </p>
            {organizationName && (
              <p className="text-muted-foreground mt-0.5 flex items-center gap-1.5 text-sm">
                <Building2Icon className="size-3.5 shrink-0" aria-hidden />
                <span className="truncate">{organizationName}</span>
              </p>
            )}
          </div>

          {/* Sign out is the only control here because it is the only one that does anything: the
              name is not editable in place, it is what you typed on the way in, and changing it
              means starting a session under a different one. */}
          <Button variant="outline" className="shrink-0" onClick={onSignOut}>
            <LogOutIcon aria-hidden />
            {identity.signOut}
          </Button>
        </div>

        {/* caution, not alarm: nothing has gone wrong and no verdict was reached — a faculty is
            missing. Icon and words carry it as well as the tint does. */}
        <div className="bg-caution text-caution-foreground border-caution-border rounded-lg border px-3 py-2.5">
          <p className="flex items-center gap-2 text-sm font-medium">
            <ShieldAlertIcon className="size-4 shrink-0" aria-hidden />
            {identity.notAnAccountTitle}
          </p>
          <p className="mt-1 text-xs leading-relaxed">{identity.notAnAccount}</p>
        </div>

        {/* The same sentence the login card uses, because it is the same fact and the product
            should not have two wordings for it. Not repeated with the organisation's name, which
            is already on the row above — what is missing there is what membership means. */}
        <p className="text-muted-foreground text-xs leading-relaxed">{identity.ownership}</p>
      </CardContent>
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
