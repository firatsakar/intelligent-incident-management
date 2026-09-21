import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { deliveryClass, formatDateTime } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { Integration, NotificationDelivery } from '@/types/api'

/**
 * Who was told, through what, and why it failed. The last link in the chain and the one that is
 * invisible everywhere else — a delivery row is the only record that a notification happened.
 */
export function DeliveryStrip({
  deliveries,
  integrations,
}: {
  deliveries: NotificationDelivery[]
  integrations: Integration[]
}) {
  const nameOf = (integrationId: string) =>
    integrations.find((integration) => integration.id === integrationId)?.name ??
    // An integration deleted after the fact leaves its deliveries behind, which is correct: the
    // notification did happen.
    'deleted integration'

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Notifications</CardTitle>
      </CardHeader>

      <CardContent>
        {deliveries.length === 0 ? (
          <p className="text-muted-foreground text-sm">
            Nothing sent yet. Notifications go out once the analysis completes, to every enabled
            integration whose filters match.
          </p>
        ) : (
          <ul className="space-y-3">
            {deliveries.map((delivery) => (
              <li key={delivery.id} className="flex flex-wrap items-start justify-between gap-2">
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <Badge variant="outline" className={cn('border', deliveryClass[delivery.status])}>
                      {delivery.status}
                    </Badge>
                    <span className="font-medium">{nameOf(delivery.integrationId)}</span>
                    {delivery.attemptCount > 1 && (
                      <span className="text-muted-foreground text-xs">
                        {delivery.attemptCount} attempts
                      </span>
                    )}
                  </div>

                  {delivery.lastError && (
                    <p className="text-destructive mt-1 text-sm break-words">
                      {delivery.lastError}
                    </p>
                  )}
                </div>

                <span className="text-muted-foreground text-sm tabular-nums">
                  {formatDateTime(delivery.sentAt ?? delivery.createdAt)}
                </span>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}
