import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { deliveryClass, formatDateTime, formatDuration } from '@/lib/format'
import { useT } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import type { NotificationDelivery } from '@/types/api'

/**
 * Who was told, through what, and why it failed. The last link in the chain and the one that is
 * invisible everywhere else — a delivery row is the only record that a notification happened.
 *
 * Each row carries the integration's name and channel as they were when it went out (Adım 16.5):
 * the integration list is the organisation's configuration and only its Admins may read it, while
 * this panel is on a screen every role opens. The channel is shown because two integrations on one
 * channel is the intended setup, so "Dev email (Mailpit)" alone does not say whether the email path
 * works.
 */
export function DeliveryStrip({ deliveries }: { deliveries: NotificationDelivery[] }) {
  const t = useT().incidents.notifications

  const failed = deliveries.filter((delivery) => delivery.status === 'Failed').length
  const sent = deliveries.filter((delivery) => delivery.status === 'Sent').length

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t.title}</CardTitle>

        {deliveries.length > 0 && (
          <CardDescription className="tabular-nums">
            {t.summary(sent, deliveries.length)}
            {failed > 0 && ` · ${t.failed(failed)}`}
          </CardDescription>
        )}
      </CardHeader>

      <CardContent>
        {deliveries.length === 0 ? (
          <p className="text-muted-foreground text-sm">{t.empty}</p>
        ) : (
          <ul className="divide-border -my-2 divide-y">
            {deliveries.map((delivery) => (
              <DeliveryRow key={delivery.id} delivery={delivery} />
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}

function DeliveryRow({ delivery }: { delivery: NotificationDelivery }) {
  const { incidents, labels } = useT()
  const t = incidents.notifications

  // An integration deleted after the fact leaves its deliveries behind, which is correct: the
  // notification did happen, and the row is the only proof of it.
  const name = delivery.integrationName ?? t.deletedIntegration

  return (
    <li className="py-2.5">
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
        <Badge variant="outline" className={cn('border', deliveryClass[delivery.status])}>
          {labels.deliveryStatus[delivery.status]}
        </Badge>

        <span className="min-w-0 flex-1 truncate text-sm font-medium" title={name}>
          {name}
        </span>

        {delivery.channel && (
          <span className="text-dim-foreground shrink-0 text-xs">
            {labels.channel[delivery.channel]}
          </span>
        )}
      </div>

      <p className="text-muted-foreground mt-1 text-xs tabular-nums">
        {delivery.sentAt ? (
          <>
            {formatDateTime(delivery.sentAt)}
            {/* How long the send itself took, from the row being written to the channel accepting
                it. Both timestamps are already on the row, and it is the only place the dispatch
                cost is visible anywhere in the console. */}
            <span className="text-dim-foreground">
              {' '}
              · {t.took(formatDuration(delivery.createdAt, delivery.sentAt))}
            </span>
          </>
        ) : (
          // Queued rather than sent. The created time is all there is, and calling it "sent" is
          // the one thing this panel must never do.
          <>{t.queued(formatDateTime(delivery.createdAt))}</>
        )}

        {delivery.attemptCount > 1 && (
          <span className="text-dim-foreground"> · {t.attempts(delivery.attemptCount)}</span>
        )}
      </p>

      {delivery.lastError && (
        // Not the solid alarm fill: this is somebody else's SMTP or HTTP error, it can run to
        // several lines, and it is a report rather than a verdict the gate reached. Same treatment
        // the settings screens give a failed connection test.
        <p className="border-alarm-border/70 bg-alarm/10 text-alarm-ink mt-1.5 rounded-md border px-2 py-1 text-xs break-words">
          {delivery.lastError}
        </p>
      )}
    </li>
  )
}
