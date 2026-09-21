import { cn } from '@/lib/utils'

import { useRealtimeStatus } from './RealtimeProvider'

type Status = ReturnType<typeof useRealtimeStatus>

const label: Record<Status, string> = {
  connecting: 'connecting',
  live: 'live',
  reconnecting: 'reconnecting',
  offline: 'offline',
}

const hint: Record<Status, string> = {
  connecting: 'Opening the update channel.',
  live: 'Updates are pushed as they happen.',
  reconnecting: 'The update channel dropped and is being re-established.',
  offline: 'Screens still work, but they will not update on their own.',
}

// Healthy is quiet: a dot and a word in the chrome. Degraded states escalate by gaining a tinted
// chip, not by moving — a pulsing dot in the corner is ambient motion competing with the data,
// and it is the one animation an operator cannot dismiss.
const chip: Record<Status, string> = {
  connecting: 'text-muted-foreground',
  live: 'text-muted-foreground',
  reconnecting: 'bg-caution text-caution-foreground border-caution-border border px-1.5',
  offline: 'bg-alarm text-alarm-foreground border-alarm-border border px-1.5',
}

const dot: Record<Status, string> = {
  connecting: 'bg-muted-foreground',
  live: 'bg-nominal-foreground',
  reconnecting: 'bg-caution-foreground',
  offline: 'bg-alarm-foreground',
}

/**
 * Small, but it earns its place: without it a socket that quietly died looks exactly like a
 * system where nothing is happening, and on this screen those are very different things.
 */
export function RealtimeIndicator() {
  const status = useRealtimeStatus()

  return (
    <span
      className={cn(
        'flex h-6 shrink-0 items-center gap-1.5 rounded-full text-xs',
        chip[status],
      )}
      title={hint[status]}
    >
      <span className={cn('size-1.5 shrink-0 rounded-full', dot[status])} />
      {label[status]}
      <span className="sr-only">— realtime connection</span>
    </span>
  )
}
