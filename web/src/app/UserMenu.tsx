import { LogOutIcon, ShieldAlertIcon } from 'lucide-react'

import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useAuth } from '@/features/auth/AuthProvider'
import { useT } from '@/lib/i18n'

/**
 * Identity in the chrome, and the one honest qualifier on it.
 *
 * The avatar is the single place in the shell that answers "signed in as whom", so it takes the
 * accent — which in this system means interaction and identity and never means status. At 24px in
 * the corner it states the fact without competing with a screen full of data.
 *
 * The unverified line stays permanently in the menu rather than becoming a banner over every
 * screen. The operator needs to be able to find out what this session is worth; they do not need
 * to be told, all day, on a console they cannot do anything about it from. The login screen is
 * where it is said loudly, once, at the moment it is acted on.
 */
export function UserMenu() {
  const { user, organization, signOut } = useAuth()
  const { account, common } = useT()

  if (!user) return null

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={<Button variant="ghost" size="icon-sm" aria-label={account.menu(user.name)} />}
      >
        <Avatar size="sm">
          <AvatarFallback className="bg-primary text-primary-foreground text-[0.6875rem] font-medium">
            {user.initials}
          </AvatarFallback>
        </Avatar>
      </DropdownMenuTrigger>

      <DropdownMenuContent align="end" className="w-72">
        <div className="flex items-center gap-2.5 px-1.5 py-1.5">
          <Avatar>
            <AvatarFallback className="bg-primary text-primary-foreground text-xs font-medium">
              {user.initials}
            </AvatarFallback>
          </Avatar>

          <div className="min-w-0">
            <p className="truncate text-sm font-medium">{user.name}</p>
            {organization && (
              <p className="text-muted-foreground truncate text-xs">{organization.name}</p>
            )}
          </div>
        </div>

        <DropdownMenuSeparator />

        <div className="px-1.5 py-1">
          <p className="text-caution-foreground flex items-center gap-1.5 text-xs font-medium">
            <ShieldAlertIcon className="size-3.5 shrink-0" aria-hidden />
            {account.unverifiedTitle}
          </p>
          <p className="text-muted-foreground mt-0.5 text-xs leading-relaxed">
            {account.unverified}
          </p>
        </div>

        <DropdownMenuSeparator />

        <DropdownMenuItem onClick={signOut}>
          <LogOutIcon />
          {common.signOut}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
