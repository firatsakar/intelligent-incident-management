import { BellIcon, DatabaseIcon, SparklesIcon, type LucideIcon } from 'lucide-react'
import { useEffect, type ReactNode } from 'react'
import { useLocation } from 'react-router-dom'

import { useT, type Dictionary } from '@/lib/i18n'

import { AnalysisSection } from './AnalysisSection'
import { NotificationsSection } from './NotificationsSection'
import { ObservabilitySection } from './ObservabilitySection'

/**
 * Everything the organisation connects the platform to, on one page, grouped by what each
 * connection is for — in the order data moves through the platform: read, analysed, routed.
 *
 * These were three tabs — Telemetry, AI sources, Integrations — which drew the product's plumbing
 * as navigation and made "where do I connect GitHub?" a question with three candidate answers.
 * Fırat's call (2026-09-25): one Integrations page, a heading per purpose. Each group keeps the
 * catalogue it had; only the page around them changed.
 */

type GroupId = keyof Dictionary['settings']['hub']['groups']

const groups: { id: GroupId; icon: LucideIcon; content: () => ReactNode }[] = [
  { id: 'observability', icon: DatabaseIcon, content: () => <ObservabilitySection /> },
  { id: 'analysis', icon: SparklesIcon, content: () => <AnalysisSection /> },
  { id: 'notifications', icon: BellIcon, content: () => <NotificationsSection /> },
]

export function IntegrationsPage() {
  const { settings } = useT()
  const t = settings.hub
  const { hash } = useLocation()

  // An old address (/settings/telemetry, /settings/ai-sources) arrives here with the group in the
  // hash. The browser does not scroll to it on a client-side navigation, so the page does.
  useEffect(() => {
    if (!hash) return

    document.getElementById(hash.slice(1))?.scrollIntoView({ block: 'start' })
  }, [hash])

  return (
    <div className="space-y-8">
      <div className="max-w-2xl space-y-3">
        <p className="text-muted-foreground text-sm">{t.intro}</p>

        {/* Plain anchors: the groups are parts of one page, not destinations of their own. */}
        <nav aria-label={t.jumpTo} className="flex flex-wrap gap-2">
          {groups.map((group) => (
            <a
              key={group.id}
              href={`#${group.id}`}
              className="border-border hover:bg-muted/60 focus-visible:ring-ring/50 inline-flex h-8 items-center gap-1.5 rounded-full border px-3 text-sm outline-none focus-visible:ring-[3px]"
            >
              <group.icon className="text-muted-foreground size-3.5" aria-hidden />
              {t.groups[group.id].title}
            </a>
          ))}
        </nav>
      </div>

      {groups.map((group) => (
        <section
          key={group.id}
          id={group.id}
          aria-labelledby={`${group.id}-title`}
          // Clears the sticky header when jumped to.
          className="border-border scroll-mt-20 space-y-4 border-t pt-6"
        >
          <div className="max-w-2xl">
            <h2 id={`${group.id}-title`} className="flex items-center gap-2 text-lg font-medium tracking-tight">
              <group.icon className="text-muted-foreground size-4.5" aria-hidden />
              {t.groups[group.id].title}
            </h2>
            <p className="text-muted-foreground mt-1 text-sm">{t.groups[group.id].description}</p>
          </div>

          {group.content()}
        </section>
      ))}
    </div>
  )
}
