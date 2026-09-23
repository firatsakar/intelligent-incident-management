import { Fragment, type ReactNode } from 'react'

import { splitTemplate } from './translate'

/**
 * A translated sentence that has to carry something which is not text — a link, a number in its
 * own styling, a piece of emphasis.
 *
 * The dictionary entry stays a plain string with named slots (`Contact {support} to raise it`),
 * and this fills them. No markup ever goes into a translation file, so a translated string cannot
 * inject and whoever writes one is never responsible for tags.
 */
export function T({ text, values }: { text: string; values: Record<string, ReactNode> }) {
  return (
    <>
      {splitTemplate(text).map((part, index) => (
        // Index keys: the list comes from a constant template, so it has a fixed length and a
        // fixed order for as long as the string does.
        <Fragment key={index}>
          {part.kind === 'text'
            ? part.value
            : // A slot with nothing for it renders as itself rather than vanishing. A sentence
              // with a hole in it is a bug someone will see; a sentence quietly missing its
              // subject is one they will not.
              (values[part.name] ?? `{${part.name}}`)}
        </Fragment>
      ))}
    </>
  )
}
