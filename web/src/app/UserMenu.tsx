import { LogOutIcon } from 'lucide-react'

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
 * Identity in the chrome.
 *
 * The avatar is the single place in the shell that answers "signed in as whom", so it takes the
 * accent — which in this system means interaction and identity and never means status. At 24px in
 * the corner it states the fact without competing with a screen full of data.
 *
 * The menu used to carry a line saying nothing had verified the session. Something does now, so
 * that line became the role: the one fact about the session that decides what the console lets
 * the reader do.
 */
export function UserMenu() {
  const { user, organization, signOut } = useAuth()
  const { account, common, labels } = useT()

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
            <p className="text-muted-foreground truncate text-xs">
              {organization
                ? `${organization.name} · ${labels.role[user.role]}`
                : labels.role[user.role]}
            </p>
          </div>
        </div>

        <DropdownMenuSeparator />

        <DropdownMenuItem onClick={() => void signOut()}>
          <LogOutIcon />
          {common.signOut}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
