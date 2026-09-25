import { useMutation } from '@tanstack/react-query'
import { useEffect, useState, type FormEvent } from 'react'
import { toast } from 'sonner'

import { organizationApi } from '@/api/endpoints'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAuth } from '@/features/auth/AuthProvider'
import { useT } from '@/lib/i18n'

import { MembersPage } from './MembersPage'

/**
 * The organisation itself: its name, then its people (Adım 25). It replaced the Members tab —
 * one installation holds one organisation, and what belongs to it (its name, its members, and
 * later the language its analyses are written in) is its Admins' to set, in one place.
 */
export function OrganizationPage() {
  return (
    <div className="max-w-3xl space-y-8">
      <NameCard />
      <MembersPage />
    </div>
  )
}

function NameCard() {
  const t = useT().settings.organization
  const { organization, refresh } = useAuth()

  const [name, setName] = useState(organization?.name ?? '')
  const [problem, setProblem] = useState<string | null>(null)

  // Another tab or another Admin may rename it; the session is what everything else shows.
  useEffect(() => setName(organization?.name ?? ''), [organization?.name])

  const rename = useMutation({
    mutationFn: (value: string) => organizationApi.rename(value),
    onSuccess: async () => {
      // The header, the account menu and the profile all read the session, so it is asked again
      // rather than patched: the server's spelling of the name is the one that stuck.
      await refresh()
      toast.success(t.saved)
    },
    onError: (error: Error) => setProblem(error.message),
  })

  const trimmed = name.trim()
  const unchanged = trimmed === (organization?.name ?? '')

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setProblem(null)

    if (!trimmed) return setProblem(t.required)

    rename.mutate(trimmed)
  }

  return (
    <Card>
      <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
        <CardHeader>
          <CardTitle>{t.title}</CardTitle>
          <CardDescription>{t.description}</CardDescription>
        </CardHeader>

        <CardContent className="space-y-1.5">
          <Label htmlFor="organization-name">{t.label}</Label>
          <Input
            id="organization-name"
            value={name}
            maxLength={128}
            className="max-w-md"
            aria-invalid={problem ? true : undefined}
            aria-describedby={problem ? 'organization-name-error' : undefined}
            onChange={(event) => {
              setName(event.target.value)
              setProblem(null)
            }}
          />
          {problem && (
            <p id="organization-name-error" role="alert" className="text-alarm-ink text-xs">
              {problem}
            </p>
          )}
        </CardContent>

        <CardFooter>
          <Button type="submit" disabled={rename.isPending || unchanged}>
            {rename.isPending ? t.saving : t.save}
          </Button>
        </CardFooter>
      </form>
    </Card>
  )
}
