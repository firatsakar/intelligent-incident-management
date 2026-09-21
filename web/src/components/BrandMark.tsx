import { cn } from '@/lib/utils'

/**
 * The mark, in one file now that the shell is no longer the only thing that draws it. The stroke
 * is painted with --primary-foreground rather than white, so it stays legible wherever the accent
 * token lands in either theme.
 */
export function BrandMark({ className }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 24 24"
      className={cn('text-primary size-6 shrink-0', className)}
      fill="none"
      aria-hidden="true"
    >
      <rect width="24" height="24" rx="6" fill="currentColor" />
      <path
        d="M4.5 14.5h3.2l2.1-6.2 3 11 2.2-4.8h4.5"
        stroke="var(--primary-foreground)"
        strokeWidth="1.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  )
}
