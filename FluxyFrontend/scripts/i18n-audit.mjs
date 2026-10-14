/**
 * Every literal `t('...')` key in `src/`, checked against both locale bundles.
 *
 * The `Translation` type on `ru.ts` only catches a key present in one file and missing from
 * the other - a key missing from **both** is invisible to `tsc`, because `en.ts` is the type
 * and the type is built out of what `en.ts` happens to contain. `t()` keys are deliberately
 * untyped (see AGENTS.md), so nothing else checks them either: lint does not, the build does
 * not, and `probe:render` only walks the keys the shell renders. A key written in a page and
 * forgotten in both bundles then reaches the visitor as the raw key string.
 *
 * So this is the check that has none of those blind spots: it reads the source, not the
 * type. Keys are collected as written, so a rename that leaves one call site behind shows up
 * here rather than on screen.
 *
 * **Run it as `npm run probe:i18n`, never as `node node_modules/.cache/i18n-audit.mjs`.**
 * The two bundles are *bundled in* by esbuild, so the cached file carries whatever they
 * contained at the last build: running it directly re-checks a snapshot and passes happily
 * while the source it claims to be reading has already lost a key. The npm script re-bundles
 * first, which is the whole reason it is a two-command chain rather than a plain `node`.
 */
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, sep } from 'node:path'
import en from '../src/locales/en.ts'
import ru from '../src/locales/ru.ts'

const SRC = 'src'

/** Every `.ts`/`.tsx` file under `src/`, recursively. */
const walk = (dir) =>
  readdirSync(dir).flatMap((entry) => {
    const path = join(dir, entry)
    return statSync(path).isDirectory() ? walk(path) : /\.tsx?$/.test(path) ? [path] : []
  })

const files = walk(SRC).filter((file) => !file.includes(`${sep}locales${sep}`))

// Guarding the guard. `src/` is resolved against the working directory, so running this
// from anywhere but the package root scans nothing and would report success over an empty
// set - a green check that checked nothing, which is the exact failure this file exists to
// prevent. Zero keys is never a real answer: the application has hundreds of them.
if (files.length === 0) {
  console.log(`FAIL  no sources under ${join(process.cwd(), SRC)}`)
  console.log('      run this through `npm run probe:i18n`, from the package root')
  process.exit(1)
}

/**
 * The bundles are imported rather than read: `ru.ts` is typed `: Translation`, so the two
 * agree by construction and what is under test here is the *source*, not the type - a key
 * missing from both is exactly the case the type cannot see.
 */

/** Resolves a dotted key to a string, or to nothing. */
const lookup = (bundle, key) => {
  let node = bundle
  for (const part of key.split('.')) {
    if (node === null || node === undefined || typeof node !== 'object') return undefined
    if (!(part in node)) return undefined
    node = node[part]
  }
  return typeof node === 'string' && node.trim() !== '' ? node : undefined
}

/** `t('a.b')` and `t(\`a.${x}.b\`)` are both calls; only the first is a name we can check. */
const LITERAL = /\bt\(\s*'([^']+)'/g

let missing = 0
const seen = new Set()

for (const file of files) {
  const source = readFileSync(file, 'utf8')

  for (const [, key] of source.matchAll(LITERAL)) {
    if (seen.has(key)) continue
    seen.add(key)

    const inEn = lookup(en, key)
    const inRu = lookup(ru, key)

    if (inEn && inRu) continue

    missing += 1
    const absent = [!inEn && 'en', !inRu && 'ru'].filter(Boolean).join(' and ')
    console.log(`FAIL  ${key}\n      absent from ${absent}`)
  }
}

console.log(
  missing === 0
    ? `all ${seen.size} literal keys resolve in both locales`
    : `\n${missing} key(s) unreachable in a locale`,
)
process.exit(missing === 0 ? 0 : 1)
