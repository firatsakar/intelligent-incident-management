import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AlertTriangleIcon, CircleCheckIcon, PlusIcon, ShieldCheckIcon, TrashIcon } from 'lucide-react'
import { useEffect, useState, type FormEvent } from 'react'
import { toast } from 'sonner'

import { ApiError } from '@/api/client'
import { aiSourcesApi } from '@/api/endpoints'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
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
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Switch } from '@/components/ui/switch'
import { useT } from '@/lib/i18n'
import type { GitHubConnection, RepositoryCheck, RepositoryMapping } from '@/types/api'

import { GitHubMark } from './BrandIcons'

/**
 * What the analysis may read besides the incident itself (Adım 17.5). One source today: the
 * organisation's GitHub, so the agent can ask "what changed in this service just before it
 * broke?" — the question our own data cannot answer.
 *
 * The page says plainly what the token is used for and what it is never used for, because an
 * Admin is being asked to hand over a credential to their code, and the reasonable response to
 * that is suspicion.
 */

const githubKey = ['ai-sources', 'github'] as const

/** One editable row. `repository` is "owner/name", the way GitHub writes it everywhere. */
interface Row {
  key: number
  service: string
  repository: string
  branch: string
}

let nextKey = 0

const toRows = (mappings: RepositoryMapping[]): Row[] =>
  mappings.map((m) => ({
    key: nextKey++,
    service: m.service,
    repository: `${m.owner}/${m.repository}`,
    branch: m.branch ?? '',
  }))

const repositoryPattern = /^[A-Za-z0-9-]+\/[A-Za-z0-9._-]+$/

export function AiSourcesPage() {
  const t = useT().settings.aiSources
  const queryClient = useQueryClient()

  const query = useQuery({ queryKey: githubKey, queryFn: aiSourcesApi.github })

  return (
    <div className="max-w-3xl space-y-4">
      <div className="max-w-2xl">
        <h2 className="text-lg font-medium tracking-tight">{t.title}</h2>
        <p className="text-muted-foreground mt-1 text-sm">{t.description}</p>
      </div>

      {query.isPending && <Skeleton className="h-72 w-full" />}

      {query.isError && (
        <p className="text-alarm-ink text-sm">
          {query.error instanceof Error ? query.error.message : t.loadError}
        </p>
      )}

      {query.data && (
        <GitHubCard
          connection={query.data}
          onSaved={(saved) => queryClient.setQueryData(githubKey, saved)}
          onRemoved={() => void queryClient.invalidateQueries({ queryKey: githubKey })}
        />
      )}
    </div>
  )
}

function GitHubCard({
  connection,
  onSaved,
  onRemoved,
}: {
  connection: GitHubConnection
  onSaved: (saved: GitHubConnection) => void
  onRemoved: () => void
}) {
  const t = useT().settings.aiSources.github

  const [token, setToken] = useState('')
  const [enabled, setEnabled] = useState(connection.isConfigured ? connection.isEnabled : true)
  const [rows, setRows] = useState<Row[]>(() =>
    connection.repositories.length > 0 ? toRows(connection.repositories) : [emptyRow()],
  )
  const [problem, setProblem] = useState<string | null>(null)
  const [removing, setRemoving] = useState(false)
  const [checks, setChecks] = useState<RepositoryCheck[] | null>(null)

  // A save or a removal elsewhere — another tab, another Admin — replaces what the form started
  // from. The token field is never filled from it: there is nothing to fill it with.
  useEffect(() => {
    setEnabled(connection.isConfigured ? connection.isEnabled : true)
    setRows(connection.repositories.length > 0 ? toRows(connection.repositories) : [emptyRow()])
  }, [connection])

  const save = useMutation({
    mutationFn: (repositories: RepositoryMapping[]) =>
      aiSourcesApi.saveGitHub({ token: token.trim() || undefined, isEnabled: enabled, repositories }),
    onSuccess: (saved) => {
      setToken('')
      onSaved(saved)
      toast.success(t.saved)
    },
    onError: (error: Error) => setProblem(describe(error)),
  })

  // Tests what is saved, not what is typed: the token in the field has not reached the server.
  const test = useMutation({
    mutationFn: aiSourcesApi.testGitHub,
    onSuccess: (result) => setChecks(result.repositories),
    onError: (error: Error) => toast.error(error.message),
  })

  const remove = useMutation({
    mutationFn: aiSourcesApi.removeGitHub,
    onSuccess: () => {
      setRemoving(false)
      setToken('')
      onRemoved()
      toast.success(t.removed)
    },
    onError: (error: Error) => toast.error(error.message),
  })

  function describe(error: Error): string {
    const errors = error instanceof ApiError
      ? (error.body as { errors?: Record<string, string[]> } | null)?.errors
      : undefined

    if (errors?.Token) return t.tokenRequired

    return error.message
  }

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setProblem(null)

    const filled = rows.filter((row) => row.service.trim() || row.repository.trim() || row.branch.trim())

    if (!connection.hasToken && !token.trim()) return setProblem(t.tokenRequired)
    if (filled.some((row) => !row.service.trim())) return setProblem(t.serviceRequired)
    if (filled.some((row) => !repositoryPattern.test(row.repository.trim())))
      return setProblem(t.repositoryInvalid)

    const services = filled.map((row) => row.service.trim().toLowerCase())
    if (new Set(services).size !== services.length) return setProblem(t.serviceTwice)

    save.mutate(
      filled.map((row) => {
        const [owner, repository] = row.repository.trim().split('/')

        return {
          service: row.service.trim(),
          owner,
          repository,
          branch: row.branch.trim() || null,
        }
      }),
    )
  }

  const update = (key: number, change: Partial<Row>) =>
    setRows((current) => current.map((row) => (row.key === key ? { ...row, ...change } : row)))

  const status = !connection.isConfigured
    ? t.status.notConnected
    : connection.isEnabled
      ? t.status.connected
      : t.status.paused

  return (
    <Card>
      <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
        <CardHeader>
          <div className="flex items-center gap-3">
            <span className="bg-muted text-foreground grid size-9 shrink-0 place-items-center rounded-lg">
              <GitHubMark className="size-5" />
            </span>
            <div className="min-w-0 flex-1">
              <CardTitle className="flex items-center gap-2">
                {t.name}
                <Badge variant={connection.isConfigured && connection.isEnabled ? 'secondary' : 'outline'}>
                  {status}
                </Badge>
              </CardTitle>
              <CardDescription>{t.description}</CardDescription>
            </div>
          </div>
        </CardHeader>

        <CardContent className="space-y-5">
          <div className="space-y-1.5">
            <Label htmlFor="github-token">{t.token}</Label>
            <Input
              id="github-token"
              type="password"
              value={token}
              autoComplete="off"
              spellCheck={false}
              placeholder={connection.hasToken ? t.tokenKept : t.tokenPlaceholder}
              onChange={(event) => setToken(event.target.value)}
              className="max-w-md"
            />
            <p className="text-muted-foreground text-xs leading-relaxed">{t.tokenHint}</p>
          </div>

          <fieldset className="space-y-2">
            <legend className="text-sm font-medium">{t.repositories}</legend>
            <p className="text-muted-foreground text-xs leading-relaxed">{t.repositoriesHint}</p>

            <div className="space-y-2">
              {rows.map((row, index) => (
                <div
                  key={row.key}
                  className="grid gap-2 sm:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)_minmax(0,0.9fr)_auto]"
                >
                  <Input
                    value={row.service}
                    placeholder={t.servicePlaceholder}
                    aria-label={t.serviceLabel(index + 1)}
                    spellCheck={false}
                    onChange={(event) => update(row.key, { service: event.target.value })}
                  />
                  <Input
                    value={row.repository}
                    placeholder={t.repositoryPlaceholder}
                    aria-label={t.repositoryLabel(index + 1)}
                    spellCheck={false}
                    onChange={(event) => update(row.key, { repository: event.target.value })}
                  />
                  <Input
                    value={row.branch}
                    placeholder={t.branchPlaceholder}
                    aria-label={t.branchLabel(index + 1)}
                    spellCheck={false}
                    onChange={(event) => update(row.key, { branch: event.target.value })}
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    aria-label={t.removeRow(index + 1)}
                    onClick={() =>
                      setRows((current) =>
                        current.length > 1 ? current.filter((r) => r.key !== row.key) : [emptyRow()],
                      )
                    }
                  >
                    <TrashIcon aria-hidden />
                  </Button>
                </div>
              ))}
            </div>

            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setRows((current) => [...current, emptyRow()])}
            >
              <PlusIcon aria-hidden />
              {t.addRow}
            </Button>
          </fieldset>

          <div className="flex items-center gap-3">
            <Switch
              id="github-enabled"
              checked={enabled}
              onCheckedChange={(checked) => setEnabled(Boolean(checked))}
            />
            <Label htmlFor="github-enabled" className="font-normal">
              {t.enabled}
            </Label>
          </div>

          {/* What the credential is for, and what it is never for — said next to the field that
              asks for it, not in a help page nobody opens before pasting a token. */}
          <p className="bg-muted/60 text-muted-foreground flex items-start gap-2 rounded-lg px-3 py-2.5 text-xs leading-relaxed">
            <ShieldCheckIcon className="text-foreground mt-px size-4 shrink-0" aria-hidden />
            <span>{t.readOnly}</span>
          </p>

          {problem && (
            <p role="alert" className="text-alarm-ink text-xs">
              {problem}
            </p>
          )}

          {checks && (
            <ul className="space-y-1.5" aria-label={t.testResults} role="status">
              {checks.map((check) => (
                <li key={check.service} className="flex items-start gap-2 text-xs">
                  {check.ok ? (
                    <CircleCheckIcon className="text-nominal-foreground mt-px size-3.5 shrink-0" aria-hidden />
                  ) : (
                    <AlertTriangleIcon className="text-alarm-ink mt-px size-3.5 shrink-0" aria-hidden />
                  )}
                  <span className="min-w-0 break-words">
                    <span className="font-medium">{check.repository}</span>
                    {check.branch && <span className="text-muted-foreground"> @ {check.branch}</span>}
                    <span className="text-muted-foreground"> · {check.service} — </span>
                    {check.ok ? t.testOk(check.recentChanges ?? 0) : t.testFailed(check.error ?? '')}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </CardContent>

        <CardFooter className="flex flex-wrap justify-between gap-2">
          <div className="flex flex-wrap gap-2">
            <Button type="submit" disabled={save.isPending}>
              {save.isPending ? t.saving : t.save}
            </Button>
            {connection.isConfigured && (
              <Button type="button" variant="outline" disabled={test.isPending} onClick={() => test.mutate()}>
                {test.isPending ? t.testing : t.test}
              </Button>
            )}
          </div>

          {connection.isConfigured && (
            <Button type="button" variant="ghost" onClick={() => setRemoving(true)}>
              {t.disconnect}
            </Button>
          )}
        </CardFooter>
      </form>

      {removing && (
        <Dialog open onOpenChange={(open) => !open && setRemoving(false)}>
          <DialogContent className="sm:max-w-md">
            <DialogHeader>
              <DialogTitle>{t.disconnectTitle}</DialogTitle>
              <DialogDescription>{t.disconnectBody}</DialogDescription>
            </DialogHeader>
            <DialogFooter>
              {/* Cancel first, so the destructive control is never what focus lands on. */}
              <Button variant="outline" onClick={() => setRemoving(false)}>
                {t.cancel}
              </Button>
              <Button variant="destructive" disabled={remove.isPending} onClick={() => remove.mutate()}>
                {t.disconnect}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      )}
    </Card>
  )
}

function emptyRow(): Row {
  return { key: nextKey++, service: '', repository: '', branch: '' }
}
