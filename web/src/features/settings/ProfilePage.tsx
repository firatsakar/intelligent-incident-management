import { Building2Icon, LogOutIcon, MonitorIcon, MoonIcon, ShieldAlertIcon, SunIcon } from 'lucide-react'
import { useTheme } from 'next-themes'

import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useAuth } from '@/features/auth/AuthProvider'
import { cn } from '@/lib/utils'

/**
 * Who this session says you are, and the one thing on this console that is genuinely yours.
 *
 * The two halves of the screen are honest in opposite directions, which is the whole reason they
 * sit together. The identity is a label this browser made up and nothing checked; the appearance
 * setting is a real preference that really persists. Saying so on each of them, once, is what
 * stops the page reading as a settings screen where nothing works — the theme *does* work, and
 * putting it next to the qualified half is what proves the qualification is specific rather than a
 * blanket disclaimer over the product.
 *
 * This is also the screen where a user would most reasonably expect the identity to be real, so it
 * is the screen that has to say plainly that it is not. Louder than the account menu's line, which
 * is a reminder; quieter than the login card, which is said at the moment it is acted on.
 */

const themeOptions = [
  { value: 'light', label: 'Light', icon: SunIcon, detail: 'Always the light palette.' },
  { value: 'dark', label: 'Dark', icon: MoonIcon, detail: 'Always the dark palette.' },
  {
    value: 'system',
    label: 'System',
    icon: MonitorIcon,
    detail: 'Follows your operating system.',
  },
] as const

export function ProfilePage() {
  const { user, organization, signOut } = useAuth()

  // RequireAuth is the only route that renders this, so a null user is unreachable — but the
  // context is typed for the login screen too, where it is genuinely null.
  if (!user) return null

  return (
    <div className="max-w-3xl space-y-4">
      <div>
        <h2 className="text-lg font-medium tracking-tight">Profile</h2>
        <p className="text-muted-foreground mt-1 text-sm">
          The name this session runs under, the organisation everything on screen belongs to, and
          how the console looks while you read it.
        </p>
      </div>

      <Identity
        name={user.name}
        initials={user.initials}
        organizationName={organization?.name}
        onSignOut={signOut}
      />

      <Appearance />
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
  return (
    <Card>
      <CardHeader>
        <CardTitle>Identity</CardTitle>
        <CardDescription>Who this console thinks you are, and what that is worth.</CardDescription>
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
            Sign out
          </Button>
        </div>

        {/* caution, not alarm: nothing has gone wrong and no verdict was reached — a faculty is
            missing. Icon and words carry it as well as the tint does. */}
        <div className="bg-caution text-caution-foreground border-caution-border rounded-lg border px-3 py-2.5">
          <p className="flex items-center gap-2 text-sm font-medium">
            <ShieldAlertIcon className="size-4 shrink-0" aria-hidden />
            This is not an account
          </p>
          <p className="mt-1 text-xs leading-relaxed">
            The name above is stored in this browser and nothing verified it. There is no password,
            no profile on any server, and no permission attached to it — the services behind this
            console answer anyone who can reach them, whatever name a session carries. To run under
            a different one, sign out and enter it. Real sign-in arrives with the gateway, and this
            page is where it will land.
          </p>
        </div>

        {/* The same sentence the login card uses, because it is the same fact and the product
            should not have two wordings for it. Not repeated with the organisation's name, which
            is already on the row above — what is missing there is what membership means. */}
        <p className="text-muted-foreground text-xs leading-relaxed">
          Incidents, signals, sources and integrations belong to the organisation rather than to the
          person who opened them. This build has one.
        </p>
      </CardContent>
    </Card>
  )
}

function Appearance() {
  const { theme, setTheme, resolvedTheme } = useTheme()

  return (
    <Card>
      <CardHeader>
        <CardTitle>Appearance</CardTitle>
        <CardDescription>
          Stored in this browser, not against your name — a second machine starts on System again.
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-2">
        {/* Real radios, hidden but focusable, with the tile as their label. A group of divs with
            onClick would take the keyboard away from the one setting on this page that works, and
            arrow-key movement between options comes free from the radio group. */}
        <fieldset>
          <legend className="sr-only">Theme</legend>

          <div className="grid gap-2 sm:grid-cols-3">
            {themeOptions.map((option) => {
              const selected = theme === option.value

              return (
                <label
                  key={option.value}
                  className={cn(
                    'has-focus-visible:ring-ring/50 relative flex cursor-pointer flex-col gap-1 rounded-lg border p-3 transition-colors has-focus-visible:ring-[3px]',
                    selected
                      ? 'border-primary bg-primary/5'
                      : 'border-border hover:bg-muted/60',
                  )}
                >
                  <input
                    type="radio"
                    name="theme"
                    value={option.value}
                    checked={selected}
                    onChange={() => setTheme(option.value)}
                    className="sr-only"
                  />

                  <span className="flex items-center gap-2 text-sm font-medium">
                    <option.icon className="size-4 shrink-0" aria-hidden />
                    {option.label}
                    {/* Selection is not left to the border colour alone. */}
                    {selected && (
                      <span className="text-primary ml-auto text-xs font-medium">Selected</span>
                    )}
                  </span>

                  <span className="text-muted-foreground text-xs">{option.detail}</span>
                </label>
              )
            })}
          </div>
        </fieldset>

        {/* "System" is the default, and on its own it does not tell the operator which palette
            they are actually going to get. resolvedTheme does, and is only read once it exists. */}
        {theme === 'system' && resolvedTheme && (
          <p className="text-muted-foreground text-xs" aria-live="polite">
            Your system is currently asking for the {resolvedTheme} palette.
          </p>
        )}
      </CardContent>
    </Card>
  )
}
