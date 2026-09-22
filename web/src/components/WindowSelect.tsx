import { useSearchParams } from 'react-router-dom'

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { windowLabel, windowPresets } from '@/lib/window'

/**
 * The time window picker, reading and writing the `window` search param itself.
 *
 * In the URL rather than in state, like every other filter in this console: a stats screen someone
 * is looking at should be a link they can send, and it should survive a refresh.
 *
 * It owns the param because the three screens that use it would otherwise write the same six lines
 * three times and drift on the details — which key, whether an unknown value is an error or a
 * default, whether changing the window resets anything else. Sorting and selection stay with the
 * screen that owns them; this only ever touches `window`.
 *
 * `value` is passed in rather than read here, because each screen has its own default: the funnel
 * follows its endpoint's 24 hours and delivery health follows its endpoint's seven days, and a
 * control that defaulted for them would have to pick one and be wrong on two screens.
 */
export function WindowSelect({ value, className }: { value: string; className?: string }) {
  const [params, setParams] = useSearchParams()

  return (
    <Select
      value={value}
      onValueChange={(next) => {
        const updated = new URLSearchParams(params)
        updated.set('window', next ?? value)

        setParams(updated)
      }}
    >
      <SelectTrigger className={className ?? 'w-44'} aria-label="Time window">
        {/* Passed explicitly: left to itself the trigger prints the raw value, so the control
            reads "24h" rather than English. */}
        <SelectValue>{windowLabel(value)}</SelectValue>
      </SelectTrigger>

      <SelectContent>
        {windowPresets.map((option) => (
          <SelectItem key={option.value} value={option.value}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
