import type { ReactNode } from 'react'

import { LanguageToggle } from '@/app/LanguageToggle'
import { ThemeToggle } from '@/app/ThemeToggle'
import { BrandMark, productName } from '@/components/BrandMark'

/**
 * The frame for the screens reached from a link in an email — accepting an invitation, setting a
 * password after a reset. The sign-in screen's right half without its left: the product introducing
 * itself belongs on the door everybody uses, not on a one-time errand.
 */
export function PublicShell({ children }: { children: ReactNode }) {
  return (
    <div className="bg-background text-foreground relative flex min-h-svh items-center justify-center px-4 py-10 sm:px-6">
      <div className="absolute top-4 right-4 flex items-center gap-1.5 sm:top-6 sm:right-6">
        <LanguageToggle />
        <ThemeToggle />
      </div>

      <main className="w-full max-w-sm">
        <div className="mb-6 flex items-center gap-2.5">
          <BrandMark className="size-7" />
          <span className="text-sm font-semibold tracking-tight">{productName}</span>
        </div>

        {children}
      </main>
    </div>
  )
}
