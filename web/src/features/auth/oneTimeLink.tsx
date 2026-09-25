import { LinkIcon } from 'lucide-react'
import { Link } from 'react-router-dom'

import { ApiError } from '@/api/client'
import { Button, buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardFooter, CardHeader } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useT, type Dictionary } from '@/lib/i18n'

/**
 * What the two link pages share: how a failure reads, and the card for a link that does not work.
 *
 * The server answers an unknown, expired, replaced and used link with the same 404 on purpose, so
 * the page cannot tell them apart either — and says the one thing that helps with all of them.
 */

export function isDeadLink(cause: unknown): boolean {
  return cause instanceof ApiError && cause.status === 404
}

/**
 * One sentence per failure that is not a dead link. A 400 carries the server's own words about a
 * field, which the form's own checks should have caught first.
 */
export function describeLinkFailure(cause: unknown, text: Dictionary['common']): string {
  if (!(cause instanceof ApiError)) return text.unreachable
  if (cause.status === 429) return text.tooMany
  if (cause.status === 400) return cause.message

  return text.serverError
}

export function CheckingLink() {
  const text = useT().oneTimeLink

  return (
    <Card aria-busy="true" aria-label={text.checking}>
      <CardHeader>
        <Skeleton className="h-6 w-2/3" />
        <Skeleton className="h-4 w-full" />
      </CardHeader>
      <CardContent className="space-y-3">
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-10 w-full" />
      </CardContent>
    </Card>
  )
}

export function DeadLink() {
  const text = useT().oneTimeLink

  return (
    <Card>
      <CardHeader>
        <span className="bg-muted text-muted-foreground mb-1 grid size-9 place-items-center rounded-lg">
          <LinkIcon className="size-5" aria-hidden />
        </span>
        <h1 className="font-heading text-lg leading-snug font-medium">{text.deadTitle}</h1>
        <p className="text-muted-foreground text-sm leading-relaxed">{text.dead}</p>
      </CardHeader>

      <CardFooter>
        <Link
          to="/login"
          replace
          className={buttonVariants({ variant: 'outline', className: 'w-full' })}
        >
          {text.toSignIn}
        </Link>
      </CardFooter>
    </Card>
  )
}

/** A failure worth trying again, as opposed to a link that will never work. */
export function LinkUnavailable({ message, onRetry }: { message: string; onRetry: () => void }) {
  const text = useT().oneTimeLink

  return (
    <Card>
      <CardHeader>
        <h1 className="font-heading text-lg leading-snug font-medium">{text.unavailableTitle}</h1>
        <p role="alert" className="text-muted-foreground text-sm leading-relaxed">
          {message}
        </p>
      </CardHeader>

      <CardFooter>
        <Button variant="outline" className="w-full" onClick={onRetry}>
          {text.retry}
        </Button>
      </CardFooter>
    </Card>
  )
}
