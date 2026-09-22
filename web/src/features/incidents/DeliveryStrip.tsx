import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { deliveryClass, formatDateTime, formatDuration } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { Integration, NotificationDelivery } from '@/types/api'

/**
 * Who was told, through what, and why it failed. The last link in the chain and the one that is
 * invisible everywhere else — a delivery row is the only record that a notification happened.
 *
 * The rows carry an integration id and nothing else, so both the name and the channel are looked up
 * here. The channel is worth the lookup: two integrations on one channel is the intended setup, so
 * "Dev email (Mailpit)" alone does not tell an operator whether the email path works.
 */
export function DeliveryStrip({
  deliveries,
  integrations,
}: {
  deliveries: NotificationDelivery[]
  integrations: Integration[]
}) {
  const lookup = (integrationId: string) =>
    integrations.find((integration) => integration.id === integrationId)

  const failed = deliveries.filter((delivery) => delivery.status === 'Failed').length
  const sent = deliveries.filter((delivery) => delivery.status === 'Sent').length

  return (
    <Card>
      <CardHeader>
        <CardTitle>Notifications</CardTitle>

        {deliveries.length > 0 && (
          <CardDescription className="tabular-nums">
            {sent} of {deliveries.length} delivered
            {failed > 0 && ` · ${failed} failed`}
          </CardDescription>
        )}
      </CardHeader>

      <CardContent>
        {deliveries.length === 0 ? (
          <p className="text-muted-foreground text-sm">
            Nothing sent yet. Notifications go out once the analysis completes, to every enabled
            integration whose filters match.
          </p>
        ) : (
          <ul className="divide-border -my-2 divide-y">
            {deliveries.map((delivery) => (
              <DeliveryRow
                key={delivery.id}
                delivery={delivery}
                integration={lookup(delivery.integrationId)}
              />
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}

function DeliveryRow({
  delivery,
  integration,
}: {
  delivery: NotificationDelivery
  integration: Integration | undefined
}) {
  // An integration deleted after the fact leaves its deliveries behind, which is correct: the
  // notification did happen, and the row is the only proof of it.
  const name = integration?.name ?? 'deleted integration'

  return (
    <li className="py-2.5">
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
        <Badge variant="outline" className={cn('border', deliveryClass[delivery.status])}>
          {delivery.status}
        </Badge>

        <span className="min-w-0 flex-1 truncate text-sm font-medium" title={name}>
          {name}
        </span>

        {integration && (
          <span className="text-dim-foreground shrink-0 text-xs">{integration.channel}</span>
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
              · took {formatDuration(delivery.createdAt, delivery.sentAt)}
            </span>
          </>
        ) : (
          // Queued rather than sent. The created time is all there is, and calling it "sent" is
          // the one thing this panel must never do.
          <>queued {formatDateTime(delivery.createdAt)}</>
        )}

        {delivery.attemptCount > 1 && (
          <span className="text-dim-foreground"> · {delivery.attemptCount} attempts</span>
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
