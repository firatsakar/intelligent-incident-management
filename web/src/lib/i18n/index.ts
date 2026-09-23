// The public surface. `lib/format.ts` reaches past it to `./active` on purpose: that module is
// free of React, and the formatters have no business pulling a provider into their import graph.

export { LanguageProvider, LocaleBoundary, useLanguage, useT } from './LanguageProvider'
export { T } from './T'
export type { Dictionary } from './en'
export { languageName, languages, type Language } from './locale'
export { plural } from './translate'
