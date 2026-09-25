import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  KeyRoundIcon,
  MailIcon,
  MoreHorizontalIcon,
  UserCheckIcon,
  UserPlusIcon,
  UserXIcon,
} from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'

import { organizationApi } from '@/api/endpoints'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/features/auth/AuthProvider'
import { initialsFor } from '@/features/auth/session'
import { formatDateTime } from '@/lib/format'
import { useT } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { userRoles, type Invitation, type IssuedLink, type Member, type UserRole } from '@/types/api'

import { InviteDialog } from './InviteDialog'
import { OneTimeLinkDialog } from './OneTimeLinkDialog'

/**
 * Who can sign in to this organisation and what each of them may do. Admins only, like everything
 * else that belongs to the organisation rather than to a person (Adım 16.5).
 *
 * The server refuses the two changes that would lock an organisation out — anyone acting on their
 * own account, and the last active Admin being demoted or switched off. This page knows both rules
 * too, so the controls that would be refused are not offered, and the row says why instead.
 */

const membersKey = ['organization', 'members'] as const
const invitationsKey = ['organization', 'invitations'] as const

interface ShownLink {
  title: string
  email: string
  issued: IssuedLink
}

export function MembersPage() {
  const queryClient = useQueryClient()
  const { settings, labels } = useT()
  const t = settings.members
  const { user } = useAuth()

  const [inviting, setInviting] = useState(false)
  const [shown, setShown] = useState<ShownLink | null>(null)
  const [deactivating, setDeactivating] = useState<Member | null>(null)

  const members = useQuery({ queryKey: membersKey, queryFn: organizationApi.members })
  const invitations = useQuery({ queryKey: invitationsKey, queryFn: organizationApi.invitations })

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: membersKey })
    void queryClient.invalidateQueries({ queryKey: invitationsKey })
  }

  const changeRole = useMutation({
    mutationFn: ({ member, role }: { member: Member; role: UserRole }) =>
      organizationApi.changeRole(member.id, role),
    onSuccess: (updated) => {
      refresh()
      toast.success(t.roleChanged(updated.displayName, labels.role[updated.role]))
    },
    onError: (error: Error) => toast.error(error.message),
  })

  const setActive = useMutation({
    mutationFn: ({ member, active }: { member: Member; active: boolean }) =>
      active ? organizationApi.activate(member.id) : organizationApi.deactivate(member.id),
    onSuccess: (updated) => {
      setDeactivating(null)
      refresh()
      toast.success(
        updated.isActive ? t.activated(updated.displayName) : t.deactivated(updated.displayName),
      )
    },
    onError: (error: Error) => toast.error(error.message),
  })

  const issueReset = useMutation({
    mutationFn: (member: Member) => organizationApi.issuePasswordReset(member.id),
    onSuccess: (issued, member) =>
      setShown({ title: t.link.resetTitle(member.displayName), email: member.email, issued }),
    onError: (error: Error) => toast.error(error.message),
  })

  const revoke = useMutation({
    mutationFn: (invitation: Invitation) => organizationApi.revokeInvitation(invitation.id),
    onSuccess: (_result, invitation) => {
      refresh()
      toast.success(t.pending.revoked(invitation.email))
    },
    onError: (error: Error) => toast.error(error.message),
  })

  const activeAdmins = (members.data ?? []).filter((m) => m.role === 'Admin' && m.isActive).length

  return (
    <div className="max-w-3xl space-y-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="max-w-xl">
          <h2 className="text-lg font-medium tracking-tight">{t.title}</h2>
          <p className="text-muted-foreground mt-1 text-sm">{t.description}</p>
        </div>

        <Button onClick={() => setInviting(true)}>
          <UserPlusIcon aria-hidden />
          {t.invite}
        </Button>
      </div>

      <Card>
        <CardContent className="px-0">
          {members.isPending && (
            <div className="space-y-3 px-4">
              <Skeleton className="h-12 w-full" />
              <Skeleton className="h-12 w-full" />
            </div>
          )}

          {members.isError && (
            <p className="text-alarm-ink px-4 text-sm">
              {members.error instanceof Error ? members.error.message : t.loadError}
            </p>
          )}

          {members.data && (
            <ul className="divide-border divide-y" aria-label={t.title}>
              {members.data.map((member) => (
                <MemberRow
                  key={member.id}
                  member={member}
                  self={member.id === user?.id}
                  lastAdmin={member.role === 'Admin' && member.isActive && activeAdmins <= 1}
                  busy={
                    (changeRole.isPending && changeRole.variables?.member.id === member.id) ||
                    (setActive.isPending && setActive.variables?.member.id === member.id)
                  }
                  onRole={(role) => changeRole.mutate({ member, role })}
                  onReset={() => issueReset.mutate(member)}
                  onActivate={() => setActive.mutate({ member, active: true })}
                  onDeactivate={() => setDeactivating(member)}
                />
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t.pending.title}</CardTitle>
          <CardDescription>{t.pending.description}</CardDescription>
        </CardHeader>

        <CardContent>
          {invitations.isPending && <Skeleton className="h-10 w-full" />}

          {invitations.isError && (
            <p className="text-alarm-ink text-sm">
              {invitations.error instanceof Error ? invitations.error.message : t.loadError}
            </p>
          )}

          {invitations.data?.length === 0 && (
            <p className="text-muted-foreground text-sm">{t.pending.empty}</p>
          )}

          {invitations.data && invitations.data.length > 0 && (
            <ul className="divide-border -my-2 divide-y">
              {invitations.data.map((invitation) => (
                <li key={invitation.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 py-2">
                  <MailIcon className="text-muted-foreground size-4 shrink-0" aria-hidden />

                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium" title={invitation.email}>
                      {invitation.email}
                    </p>
                    <p className="text-muted-foreground text-xs">
                      {labels.role[invitation.role]} ·{' '}
                      {t.pending.expires(formatDateTime(invitation.expiresAt))}
                    </p>
                  </div>

                  <Button
                    variant="ghost"
                    size="sm"
                    disabled={revoke.isPending && revoke.variables?.id === invitation.id}
                    aria-label={t.pending.revokeAria(invitation.email)}
                    onClick={() => revoke.mutate(invitation)}
                  >
                    {t.pending.revoke}
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      {inviting && (
        <InviteDialog
          onCancel={() => setInviting(false)}
          onIssued={(result) => {
            setInviting(false)
            refresh()
            setShown({
              title: t.link.invitationTitle(result.invitation.email),
              email: result.invitation.email,
              issued: result.issued,
            })
          }}
        />
      )}

      {shown && <OneTimeLinkDialog {...shown} onClose={() => setShown(null)} />}

      {deactivating && (
        <Dialog open onOpenChange={(open) => !open && setDeactivating(null)}>
          <DialogContent className="sm:max-w-md">
            <DialogHeader>
              <DialogTitle>{t.deactivateDialog.title(deactivating.displayName)}</DialogTitle>
              <DialogDescription>{t.deactivateDialog.body}</DialogDescription>
            </DialogHeader>

            <DialogFooter>
              {/* Cancel first, so the destructive control is never what focus lands on. */}
              <Button variant="outline" onClick={() => setDeactivating(null)}>
                {t.deactivateDialog.cancel}
              </Button>
              <Button
                variant="destructive"
                disabled={setActive.isPending}
                onClick={() => setActive.mutate({ member: deactivating, active: false })}
              >
                {t.deactivateDialog.confirm}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      )}
    </div>
  )
}

function MemberRow({
  member,
  self,
  lastAdmin,
  busy,
  onRole,
  onReset,
  onActivate,
  onDeactivate,
}: {
  member: Member
  self: boolean
  lastAdmin: boolean
  busy: boolean
  onRole: (role: UserRole) => void
  onReset: () => void
  onActivate: () => void
  onDeactivate: () => void
}) {
  const { settings, labels } = useT()
  const t = settings.members

  // The two rules the server enforces, read off the row so the refused control is never offered.
  const locked = self || lastAdmin
  const why = self ? t.selfHint : lastAdmin ? t.lastAdminHint : null

  return (
    <li className="flex flex-wrap items-center gap-x-3 gap-y-2 px-4 py-3">
      <Avatar className={cn('shrink-0', !member.isActive && 'opacity-50')}>
        <AvatarFallback className="bg-muted text-foreground text-xs font-medium">
          {initialsFor(member.displayName)}
        </AvatarFallback>
      </Avatar>

      <div className="min-w-0 flex-1 basis-48">
        <p className="flex items-center gap-2 text-sm font-medium">
          <span className={cn('truncate', !member.isActive && 'text-muted-foreground')}>
            {member.displayName}
          </span>
          {self && <Badge variant="secondary">{t.you}</Badge>}
          {!member.isActive && <Badge variant="outline">{t.deactivatedBadge}</Badge>}
        </p>
        <p className="text-muted-foreground truncate text-xs" title={member.email}>
          {member.email}
        </p>
        {why && <p className="text-muted-foreground mt-1 text-xs">{why}</p>}
      </div>

      <div className="flex items-center gap-1">
        <Select
          value={member.role}
          disabled={locked || busy}
          onValueChange={(value) => value && value !== member.role && onRole(value as UserRole)}
        >
          <SelectTrigger className="w-36" aria-label={t.roleOf(member.displayName)}>
            <SelectValue>{labels.role[member.role]}</SelectValue>
          </SelectTrigger>
          <SelectContent>
            {userRoles.map((role) => (
              <SelectItem key={role} value={role}>
                {labels.role[role]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        {/* No menu at all on one's own row: every action in it is refused there. */}
        {self ? (
          <span className="size-8" aria-hidden />
        ) : (
          <DropdownMenu>
            <DropdownMenuTrigger
              render={
                <Button
                  variant="ghost"
                  size="icon-sm"
                  disabled={busy}
                  aria-label={t.actionsFor(member.displayName)}
                />
              }
            >
              <MoreHorizontalIcon />
            </DropdownMenuTrigger>

            <DropdownMenuContent align="end" className="w-64">
              {member.isActive ? (
                <>
                  <DropdownMenuItem onClick={onReset}>
                    <KeyRoundIcon />
                    {t.issueReset}
                  </DropdownMenuItem>
                  <DropdownMenuItem variant="destructive" disabled={lastAdmin} onClick={onDeactivate}>
                    <UserXIcon />
                    {t.deactivate}
                  </DropdownMenuItem>
                </>
              ) : (
                <DropdownMenuItem onClick={onActivate}>
                  <UserCheckIcon />
                  {t.activate}
                </DropdownMenuItem>
              )}
            </DropdownMenuContent>
          </DropdownMenu>
        )}
      </div>
    </li>
  )
}
