/**
 * The two pieces of machinery a dictionary of plain objects still needs.
 *
 * Interpolation is not one of them. An entry that takes a value is a *function* in the dictionary,
 * so a template literal does the substitution and the compiler checks the arguments — which is
 * strictly better than a runtime `t(key, { count })` that cannot. What is left is choosing a
 * plural form, and splitting a sentence that has to carry something which is not text.
 *
 * No React here. `T.tsx` is the React half, and keeping this side clear of it is what lets
 * `lib/format.ts` reach for the same helpers.
 */

export type TemplatePart = { kind: 'text'; value: string } | { kind: 'slot'; name: string }

/**
 * Splits `Contact {support} to raise it` into text and named slots.
 *
 * The alternative — markup inside the dictionary, rendered as HTML — makes every translated
 * string something that can inject, and makes whoever writes the translation responsible for
 * tags. A slot can do neither: the component decides what goes into it.
 */
export function splitTemplate(template: string): TemplatePart[] {
  // The capture group means split() interleaves the slot names with the text around them, so the
  // odd indices are always names. No index arithmetic, and no way to fall out of step.
  return template
    .split(/\{(\w+)\}/)
    .map((piece, index): TemplatePart =>
      index % 2 === 1 ? { kind: 'slot', name: piece } : { kind: 'text', value: piece },
    )
    .filter((part) => part.kind === 'slot' || part.value !== '')
}

const pluralRules = new Map<string, Intl.PluralRules>()

function rulesFor(locale: string): Intl.PluralRules {
  let rules = pluralRules.get(locale)

  if (!rules) {
    rules = new Intl.PluralRules(locale)
    pluralRules.set(locale, rules)
  }

  return rules
}

/**
 * The right form for a count, under the active language's own rules.
 *
 * Turkish writes both forms identically — "3 sinyal", never "3 sinyaller" — so its dictionary
 * repeats itself here. That repetition is correct Turkish rather than a copy-paste slip, and
 * going through `Intl.PluralRules` anyway is what keeps the call site identical in both
 * languages instead of one of them growing a special case.
 */
export function plural(
  locale: string,
  count: number,
  forms: { one: string; other: string },
): string {
  return rulesFor(locale).select(count) === 'one' ? forms.one : forms.other
}
