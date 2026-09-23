import { Fragment, createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'

import { dictionaryFor, setActiveLanguage } from './active'
import type { Dictionary } from './en'
import { applyDocumentLanguage, detectLanguage, storeLanguage, type Language } from './locale'

interface LanguageValue {
  language: Language
  setLanguage: (language: Language) => void
  /** The active dictionary. Named `t` at the call site by convention, destructured in practice. */
  t: Dictionary
}

const LanguageContext = createContext<LanguageValue | null>(null)

export function useLanguage(): LanguageValue {
  const value = useContext(LanguageContext)

  if (!value) throw new Error('useLanguage must be used inside LanguageProvider')

  return value
}

/** The dictionary on its own, which is what almost every screen actually wants. */
export function useT(): Dictionary {
  return useLanguage().t
}

/**
 * Holds the chosen language, and keeps the three places that care about it in step: React state,
 * the module the formatters read, and the document's own `lang` attribute.
 *
 * Mounted at the very top of the tree so that everything, including the login screen, can consume
 * it. The remount that a language change needs is not done here — see `LocaleBoundary`.
 */
export function LanguageProvider({ children }: { children: ReactNode }) {
  const [language, setLanguageState] = useState<Language>(() => {
    const detected = detectLanguage()

    // Synchronously, in the initialiser rather than in an effect: `lib/format.ts` reads the
    // active language while components render, and an effect would paint one frame formatted
    // for the wrong locale before correcting itself.
    setActiveLanguage(detected)

    return detected
  })

  // The attribute, unlike the formatters, is not read during render — the pre-paint script in
  // index.html has already set it for the first frame, and this keeps it true afterwards.
  useEffect(() => applyDocumentLanguage(language), [language])

  const setLanguage = useCallback((next: Language) => {
    setActiveLanguage(next)
    storeLanguage(next)
    setLanguageState(next)
  }, [])

  const value = useMemo<LanguageValue>(
    () => ({ language, setLanguage, t: dictionaryFor(language) }),
    [language, setLanguage],
  )

  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>
}

/**
 * Remounts what it wraps when the language changes.
 *
 * This is load-bearing, not decoration. `lib/format.ts` reads the active locale from a module
 * rather than from context, so a component that prints a timestamp and no translated word is not
 * a context consumer — and React will not re-render it when the provider's state changes, because
 * `children` arrives as the same element from the parent's own render and the subtree bails out.
 * Without this key such a component keeps the previous locale until something else happens to
 * re-render it, which is precisely the silent wrong state this console is built not to have.
 *
 * The alternative was to make every one of those components call a hook it does not otherwise
 * need, and to trust that nobody ever forgets. This is one line and cannot be forgotten.
 *
 * The cost is real and belongs in writing: switching language remounts the page tree, so scroll
 * position, any open dialog and the heat map's flash state reset. It is a deliberate action taken
 * about once per session, and it sits below the query cache and below the hub connections —
 * neither the cached data nor the three sockets are touched.
 */
export function LocaleBoundary({ children }: { children: ReactNode }) {
  const { language } = useLanguage()

  return <Fragment key={language}>{children}</Fragment>
}
