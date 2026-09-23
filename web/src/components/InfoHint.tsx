import { InfoIcon } from 'lucide-react'
import { useId, type ReactNode } from 'react'

import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { cn } from '@/lib/utils'

/**
 * A quiet "what does this mean" beside a term the backend named and nobody else would recognise.
 *
 * This exists so no screen has to get Base UI's composition right a second time. The primitive is
 * Base UI, not Radix: there is no `asChild`, the trigger composes through `render`, and it needs
 * the Provider that `app/providers.tsx` mounts. Getting that wrong in Adım 19 is why the console
 * spent a chunk using the native `title` attribute instead.
 *
 * What belongs in one, and what does not: a tooltip is invisible to a touch user who never hovers
 * and to anyone reading the screen rather than driving it, so it may only ever carry the *second*
 * sentence. The term, the number and the verdict stay on the page. This carries the glossary —
 * what `burstBase` measures, which clock a timestamp came off — and nothing a decision rests on.
 *
 * It is a real `<button>`, which is what Base UI's trigger renders by default, so focus opens the
 * popup as well as hover. `label` is the accessible name, because an icon-only control without one
 * is announced as "button".
 *
 * The `aria-describedby` copy is not belt-and-braces. Base UI's tooltip popup, as shipped in 1.8,
 * carries no `role`, no `id` and `tabindex="-1"`, and the trigger gets no `aria-describedby` — it
 * was checked in the browser, not assumed. The popup is therefore decoration as far as assistive
 * tech is concerned, and without the hidden copy a screen reader user would hear the label and
 * never reach a word of the explanation. Duplicating a sentence in the DOM is the cheap half of
 * that trade.
 */
export function InfoHint({
  label,
  side = 'top',
  className,
  children,
}: {
  /** Names the control for assistive tech — "What <term> means", not "info". */
  label: string
  side?: 'top' | 'bottom' | 'left' | 'right'
  className?: string
  children: ReactNode
}) {
  const describedBy = useId()

  return (
    <>
      <Tooltip>
        <TooltipTrigger
          render={
            <button
              type="button"
              aria-label={label}
              aria-describedby={describedBy}
              // Inline-grid at 24px so it sits on the text baseline of whatever it annotates while
              // still meeting the hit target the rest of the console holds itself to. The icon is
              // smaller than its target on purpose — the affordance should be findable, not loud.
              className={cn(
                'text-muted-foreground hover:text-foreground focus-visible:ring-ring/50 inline-grid size-6 shrink-0 place-items-center rounded-full align-middle transition-colors outline-none focus-visible:ring-[3px]',
                className,
              )}
            />
          }
        >
          <InfoIcon className="size-3.5" aria-hidden />
        </TooltipTrigger>

        {/* Leading-relaxed and left-aligned: these run to a sentence or two, and the primitive's
            default centring is meant for three-word labels. */}
        <TooltipContent side={side} className="max-w-xs text-left leading-relaxed">
          {children}
        </TooltipContent>
      </Tooltip>

      {/* normal-case because this hidden copy inherits whatever the annotated line is wearing, and
          two of the places that use a hint are `uppercase` section labels — so a sentence written
          to be read was being transformed into one. Invisible either way; the transform is not
          reliably absent from what a screen reader is handed. Found on the incident detail's
          detection strip during the Adım 20.5 acceptance run, in both languages. */}
      <span id={describedBy} className="sr-only normal-case">
        {children}
      </span>
    </>
  )
}
