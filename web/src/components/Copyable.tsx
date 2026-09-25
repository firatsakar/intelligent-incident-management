import { CheckIcon, CopyIcon } from 'lucide-react'
import { useState } from 'react'

import { Button } from '@/components/ui/button'
import { useT } from '@/lib/i18n'

/**
 * A value shown once and meant to be carried somewhere else: an ingest key, a collector config, a
 * one-time link. The value is set in mono on a muted ground so it reads as something to copy rather
 * than as prose, and the button says it worked, because a clipboard gives no sign of its own.
 *
 * Moved out of the ingest key dialog when the invitation and reset links became its second user.
 */
export function Copyable({
  label,
  value,
  hint,
  block = false,
}: {
  label: string
  value: string
  hint?: string
  /** Multi-line text, kept as written and scrolled sideways rather than wrapped. */
  block?: boolean
}) {
  const { common } = useT()
  const [copied, setCopied] = useState(false)

  const copy = async () => {
    await navigator.clipboard.writeText(value)
    setCopied(true)
    window.setTimeout(() => setCopied(false), 1500)
  }

  return (
    <div className="min-w-0 space-y-1.5">
      <div className="flex items-center justify-between gap-2">
        <span className="text-sm font-medium">{label}</span>
        <Button
          variant="ghost"
          size="sm"
          onClick={() => void copy()}
          aria-label={`${common.copy}: ${label}`}
        >
          {copied ? <CheckIcon aria-hidden /> : <CopyIcon aria-hidden />}
          <span aria-live="polite">{copied ? common.copied : common.copy}</span>
        </Button>
      </div>

      {block ? (
        <pre className="bg-muted overflow-x-auto rounded-md px-3 py-2 font-mono text-xs leading-relaxed">
          {value}
        </pre>
      ) : (
        <p className="bg-muted rounded-md px-3 py-2 font-mono text-xs break-all">{value}</p>
      )}

      {hint && <p className="text-muted-foreground text-xs">{hint}</p>}
    </div>
  )
}
