import { LinkIcon, MailCheckIcon, MailXIcon } from 'lucide-react'

import { Copyable } from '@/components/Copyable'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { formatDateTime } from '@/lib/format'
import { useT } from '@/lib/i18n'
import type { IssuedLink } from '@/types/api'

/**
 * The one moment an invitation or a reset link exists in the open, as the ingest key dialog is for
 * a key: only the hash is stored, so this cannot be reopened. Whether the email went is said next to
 * the link, because when it did not, handing the link over is now the Admin's job.
 */
export function OneTimeLinkDialog({
  title,
  email,
  issued,
  onClose,
}: {
  title: string
  email: string
  issued: IssuedLink
  onClose: () => void
}) {
  const t = useT().settings.members.link

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <div className="flex items-center gap-3">
            <span className="bg-muted text-foreground grid size-9 shrink-0 place-items-center rounded-lg">
              <LinkIcon className="size-5" aria-hidden />
            </span>

            <div className="min-w-0">
              <DialogTitle>{title}</DialogTitle>
              <DialogDescription>{t.once}</DialogDescription>
            </div>
          </div>
        </DialogHeader>

        <div className="min-w-0 space-y-4">
          <Copyable label={t.label} value={issued.link} hint={t.expires(formatDateTime(issued.expiresAt))} />

          {issued.emailSent ? (
            <p className="text-muted-foreground flex items-start gap-2 text-sm">
              <MailCheckIcon className="mt-0.5 size-4 shrink-0" aria-hidden />
              <span className="min-w-0 break-words">{t.emailed(email)}</span>
            </p>
          ) : (
            // caution rather than alarm: nothing is broken, but the next step moved to the reader.
            <p className="bg-caution text-caution-foreground border-caution-border flex items-start gap-2 rounded-lg border px-3 py-2.5 text-sm">
              <MailXIcon className="mt-0.5 size-4 shrink-0" aria-hidden />
              <span className="min-w-0 break-words">{t.notEmailed(email)}</span>
            </p>
          )}
        </div>

        <DialogFooter>
          <Button onClick={onClose}>{t.done}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
