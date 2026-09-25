/**
 * Which languages exist, how each one formats, and where the choice is kept.
 *
 * Free of React and of the dictionaries on purpose: the pre-paint script in `index.html`, the
 * provider, and `lib/format.ts` all have to agree on these rules, and this is the only file the
 * three of them can share without importing each other.
 */

export const languages = ['en', 'tr'] as const

export type Language = (typeof languages)[number]

/**
 * Each language named in itself. A picker that offers "Turkish" is no use to the person looking
 * for Türkçe — the endonym is the one word on the list they can be certain of.
 */
export const languageName: Record<Language, string> = {
  en: 'English',
  tr: 'Türkçe',
}

/**
 * The locale each language formats dates and numbers with — a region, not a bare language tag.
 *
 * `en` on its own leaves the region to the browser, and this console's copy is written in British
 * English ("organisation", "colour"). A date beside that copy arriving in American order would be
 * the page disagreeing with itself.
 */
export const intlLocale: Record<Language, string> = {
  en: 'en-GB',
  tr: 'tr-TR',
}

/** Shared with the pre-paint script in index.html. Changing it means changing both. */
export const LANGUAGE_KEY = 'iim.language'

export function isLanguage(value: unknown): value is Language {
  return typeof value === 'string' && (languages as readonly string[]).includes(value)
}

// Storage throws rather than returning null in a locked-down browser. The same guard, for the
// same reason, as features/auth/session.ts: a console that will not boot in private mode is a
// worse failure than one that forgets a preference.
function readStorage(): string | null {
  try {
    return localStorage.getItem(LANGUAGE_KEY)
  } catch {
    return null
  }
}

export function storeLanguage(language: Language): void {
  try {
    localStorage.setItem(LANGUAGE_KEY, language)
  } catch {
    // Blocked or full: the choice then lives exactly as long as the tab does.
  }
}

/**
 * The stored choice, or the closest match to what the browser asks for, or English.
 *
 * Matched on the subtag rather than the whole tag, so `tr-CY` lands on Turkish. Anything else
 * falls through to English, which is the source language and the only one every string is
 * guaranteed to exist in.
 */
export function detectLanguage(): Language {
  const stored = readStorage()

  if (isLanguage(stored)) return stored

  const requested =
    typeof navigator === 'undefined' ? [] : (navigator.languages ?? [navigator.language])

  for (const tag of requested) {
    const subtag = tag.toLowerCase().split('-')[0]

    if (isLanguage(subtag)) return subtag
  }

  return 'en'
}

/**
 * The document's own declaration.
 *
 * Read by assistive technology, by `:lang()` and by the browser's offer to translate the page —
 * none of which look at React state, which is why this is set on the element rather than only
 * held in the provider.
 */
export function applyDocumentLanguage(language: Language): void {
  document.documentElement.lang = language
}
