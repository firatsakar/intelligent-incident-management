import { cn } from '@/lib/utils'

import { useRealtimeStatus } from './RealtimeProvider'

const label: Record<ReturnType<typeof useRealtimeStatus>, string> = {
  connecting: 'connecting',
  live: 'live',
  reconnecting: 'reconnecting',
  offline: 'offline',
}

const dot: Record<ReturnType<typeof useRealtimeStatus>, string> = {
  connecting: 'bg-muted-foreground animate-pulse',
  live: 'bg-emerald-500',
  reconnecting: 'bg-amber-500 animate-pulse',
  offline: 'bg-muted-foreground',
}

/**
 * Small, but it earns its place: without it a socket that quietly died looks exactly like a
 * system where nothing is happening, and on this screen those are very different things.
 */
export function RealtimeIndicator() {
  const status = useRealtimeStatus()

  return (
    <span
      className="text-muted-foreground flex items-center gap-1.5 text-xs"
      title={
        status === 'live'
          ? 'Updates are pushed as they happen.'
          : 'Screens still work, but they will not update on their own.'
      }
    >
      <span className={cn('size-1.5 rounded-full', dot[status])} />
      {label[status]}
    </span>
  )
}
