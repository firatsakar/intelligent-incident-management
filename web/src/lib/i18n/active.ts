import { en, type Dictionary } from './en'
import { intlLocale, type Language } from './locale'
import { tr } from './tr'

/**
 * The active language, held in a module rather than only in React state.
 *
 * It exists for the callers that are not components. `lib/format.ts` renders dates, counts,
 * durations and relative times from plain functions used in something like a hundred places;
 * threading a locale through every one of those call sites would be a larger change than the
 * feature that needs it, and would put the locale in the signature of functions whose callers
 * have no opinion about it.
 *
 * `LanguageProvider` is the only writer, and it writes during render before anything reads — so
 * a component and a formatter in the same frame always agree. The rule that keeps that true is
 * in `LocaleBoundary`, which is where the reasoning is written out.
 */

const dictionaries: Record<Language, Dictionary> = { en, tr }

let current: Language = 'en'

export function setActiveLanguage(language: Language): void {
  current = language
}

export function activeLanguage(): Language {
  return current
}

export function dictionaryFor(language: Language): Dictionary {
  return dictionaries[language]
}

/** The dictionary, for the non-component callers. Components use `useT()`. */
export function activeDictionary(): Dictionary {
  return dictionaries[current]
}

/** The BCP 47 locale the active language formats with — what `Intl` wants, not the language tag. */
export function activeIntlLocale(): string {
  return intlLocale[current]
}
