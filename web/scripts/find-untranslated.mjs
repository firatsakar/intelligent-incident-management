// Finds user-facing text that never reached the dictionary.
//
// The compiler already guarantees the other half of the problem: `tr.ts` is typed as the shape of
// `en.ts`, so a Turkish entry cannot go missing. What it cannot see is a sentence that was never
// made an entry at all — a heading still written straight into JSX renders perfectly in English
// and is simply never translated.
//
// So this is deliberately a text scan rather than a type check, and it is deliberately crude: it
// reports anything that *looks* like prose in a place the user reads, and the exceptions below are
// written out one by one rather than being swept up by a pattern. A quiet allow-list is how a scan
// like this stops finding anything.
//
//   node scripts/find-untranslated.mjs        exits 1 if anything is found
//
// Run from `web/`.

import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, sep } from 'node:path'

const root = 'src'

/**
 * Files whose strings are not user-facing text.
 *
 * `plannedIds.ts` holds ids. `BrandMark.tsx` holds the product's name, which is a proper noun and
 * therefore not a translation — it is named here rather than silently skipped, because "it is a
 * brand" is an argument somebody should be able to disagree with.
 */
const allowedFiles = new Set(['src/components/BrandMark.tsx', 'src/features/settings/plannedIds.ts'])

/**
 * Exact strings that are data rather than prose.
 *
 * Example values a customer types over, and the two literals the server sends that this console
 * recognises by name. Each is matched whole, so a longer sentence containing one still reports.
 */
const allowedStrings = new Set([
  // configSchema placeholders — a hostname, a port, an address, a filter expression.
  'localhost',
  'incidents@example.com',
  'oncall@example.com',
  'https://example.com/hooks/incidents',
  'https://acme.atlassian.net',
  'http://localhost:8082',
  "@Level in ['Error','Fatal']",
  'Service',
  'Task',
  'OPS',
  // Trademarks on the "coming soon" rows.
  'Slack',
  'Microsoft Teams',
  'PagerDuty',
  'Discord',
  // The server's own sentinel, kept as the comparison key; its display is translated.
  '(signature gone)',
  // `ApiError.name`, which is the Error's own class name and is never rendered.
  'ApiError',
  // The stored value of the default organisation, which is a key rather than a name — its
])

function* walk(dir) {
  for (const entry of readdirSync(dir)) {
    const path = join(dir, entry)

    if (statSync(path).isDirectory()) yield* walk(path)
    else if (path.endsWith('.tsx') || path.endsWith('.ts')) yield path
  }
}

/** Comments are prose too, and they are supposed to be English. */
function stripComments(source) {
  return source
    .replace(/\{\/\*[\s\S]*?\*\/\}/g, '')
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/^\s*\/\/.*$/gm, '')
}

const findings = []

for (const path of walk(root)) {
  const key = relative('.', path).split(sep).join('/')

  if (allowedFiles.has(key) || key.startsWith('src/lib/i18n/')) continue

  const source = stripComments(readFileSync(path, 'utf8'))

  // A JSX text run of two or more words. Anything holding `=` or `=>` is code that happened to
  // sit between a `>` and a `<`.
  for (const match of source.matchAll(
    />\s*([A-Za-z][A-Za-z0-9'’,.\-—…:%/()]*(?:\s+[A-Za-z0-9'’,.\-—…:%/()]+)+)\s*</g,
  )) {
    const text = match[1].split(/\s+/).join(' ')

    if (!/[A-Za-z]{3}/.test(text)) continue
    if (allowedStrings.has(text)) continue
    // A generic argument list spans a `>` and a `<` too — `Record<string, string>` reads to this
    // regex exactly as a two-word sentence does. Rejecting anything that carries a keyword or a
    // call is what tells the two apart without needing a parser.
    if (text.includes('=')) continue
    if (/\b(const|function|return|type|interface|import|export|await|new|typeof|keyof)\b/.test(text))
      continue
    if (/[A-Za-z]\(/.test(text)) continue
    if (/\b(Record|Map|Set|Array|Promise|Partial|Omit|Pick|Event|Props|Ref)\b/.test(text)) continue

    findings.push({ key, kind: 'jsx', text })
  }

  // A string literal handed to a prop the user reads.
  for (const match of source.matchAll(
    /\b(title|label|placeholder|aria-label|okLabel|failLabel|detail|summary|hint|name)\s*[=:]\s*(["'])([^"'\n]{3,})\2/g,
  )) {
    const text = match[3].trim()

    if (!/[A-Za-z]{3}/.test(text)) continue
    if (allowedStrings.has(text)) continue
    // A single lower-case token is an identifier — a query key, a CSS name, an HTML attribute.
    if (!/\s/.test(text) && !/^[A-Z]/.test(text)) continue

    findings.push({ key, kind: match[1], text })
  }
}

if (findings.length === 0) {
  console.log('No untranslated user-facing text found.')
  process.exit(0)
}

for (const finding of findings) {
  console.log(`${finding.key}  [${finding.kind}]  ${finding.text}`)
}

console.log(`\n${findings.length} untranslated string(s).`)
process.exit(1)
