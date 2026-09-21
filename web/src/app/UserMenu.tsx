import { LogInIcon, UserRoundIcon } from 'lucide-react'

import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'

/**
 * A placeholder on purpose, and it says so rather than pretending. Authentication and the
 * organisation scoping behind it are a later step; an avatar showing an invented name here would
 * be the kind of decoration that makes an operator trust the rest of the screen less.
 *
 * When identity arrives, this is the only file in the shell that has to change.
 */
export function UserMenu() {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={<Button variant="ghost" size="icon-sm" aria-label="Account" />}
      >
        <Avatar size="sm">
          <AvatarFallback className="bg-transparent">
            <UserRoundIcon className="size-3.5" />
          </AvatarFallback>
        </Avatar>
      </DropdownMenuTrigger>

      <DropdownMenuContent align="end" className="w-64">
        <div className="px-1.5 py-1">
          <p className="text-sm font-medium">Not signed in</p>
          <p className="text-muted-foreground text-xs">
            Authentication is not enabled yet, so this console shows every organisation's data.
          </p>
        </div>

        <DropdownMenuSeparator />

        <DropdownMenuItem disabled>
          <LogInIcon />
          Sign in
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
