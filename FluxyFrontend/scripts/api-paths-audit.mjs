/**
 * Every API path this application requests, checked against the prefixes the dev server
 * proxies to the backend.
 *
 * Like `probe:i18n`, this one needs no backend and no browser: it reads `src/lib/api.ts`,
 * `src/lib/csrf.ts` and `src/lib/http.ts` from disk, takes the first segment of every path
 * a request is made with, and compares that set with `backendPaths` in `vite.config.js`.
 *
 * It exists because the failure it catches is silent twice over. An endpoint outside every
 * proxied prefix is answered by Vite's own HTML fallback with a **200**: `res.ok` is true,
 * the JSON parse fails, and the call reads as a broken server rather than as a miss - and
 * when the same mistake happened once before, it surfaced much later as `csrf_invalid`,
 * because `getCsrfToken` treats any failure as "no token". Nothing in either symptom names
 * the prefix as the problem. The `backendPaths` comment in `vite.config.js` states the rule;
 * this is the part that checks it.
 *
 * Both directions are audited: a prefix a request uses but the proxy does not know (the
 * silent failure above), and a prefix the proxy knows but nothing requests (a stale entry
 * that invites the next endpoint to be added under it and fail in the same silence).
 *
 * **Run it as `npm run probe:paths`, from the package root.** The files are read relative
 * to the working directory, so a run from anywhere else finds no sources; that case is a
 * FAIL rather than an empty pass, for the reason `probe:i18n` gives about its own guard.
 */
import { readFileSync } from 'node:fs'

const API_SOURCES = ['src/lib/api.ts', 'src/lib/csrf.ts', 'src/lib/http.ts']
const CONFIG_SOURCE = 'vite.config.js'

/**
 * The request helpers whose first argument is an API path, and the constant `csrf.ts` keeps
 * its own path in.
 *
 * Matching the call rather than every string in these files is deliberate: the comments
 * around them (and `NO_RETRY_PATHS` in `http.ts`) mention the same paths in prose, and a
 * document full of path-shaped sentences is not what this audit is about. The template
 * literals stop at `$` so a `${...}` substitution is not read as part of the path.
 */
const PATH_CALL = /\b(?:apiFetch|apiGet|rawFetch)(?:<[^>(]*>)?\s*\(\s*[`'"](\/[^`'"$]*)/g
const TOKEN_URL = /TOKEN_URL\s*=\s*[`'"](\/[^`'"$]*)/g

/** The first segment of a path: `/admin/users/1` and `/admin?page=2` are both `/admin`. */
const prefixOf = (path) => {
  const match = path.match(/^\/([A-Za-z][A-Za-z0-9-]*)/)
  return match ? `/${match[1]}` : null
}

/** Where a prefix was found, so a FAIL can name the file rather than only the prefix. */
const used = new Map()

for (const file of API_SOURCES) {
  const source = readFileSync(file, 'utf8')

  for (const pattern of [PATH_CALL, TOKEN_URL]) {
    for (const [, path] of source.matchAll(pattern)) {
      const prefix = prefixOf(path)
      if (prefix === null) continue

      const where = used.get(prefix) ?? []
      if (!where.includes(file)) where.push(file)
      used.set(prefix, where)
    }
  }
}

// Guarding the guard, the same way `probe:i18n` does: the sources are read relative to the
// working directory, so a run from the wrong one would audit an empty set and pass. Zero
// prefixes is never a real answer - the application asks for dozens of paths.
if (used.size === 0) {
  console.log(`FAIL  no API paths found under ${process.cwd()}`)
  console.log('      run this through `npm run probe:paths`, from the package root')
  process.exit(1)
}

const configSource = readFileSync(CONFIG_SOURCE, 'utf8')
const arrayMatch = configSource.match(/backendPaths\s*=\s*\[([^\]]*)\]/)

if (arrayMatch === null) {
  console.log(`FAIL  backendPaths not found in ${CONFIG_SOURCE}`)
  console.log('      the proxy is built from it; every request would reach the SPA fallback')
  process.exit(1)
}

const proxied = new Set(
  [...arrayMatch[1].matchAll(/[`'"]([^`'"]+)[`'"]/g)].map(([, path]) => path),
)

let failures = 0

for (const prefix of [...used.keys()].sort()) {
  const files = used.get(prefix).join(', ')

  if (proxied.has(prefix)) {
    console.log(`PASS  ${prefix} is proxied (used by ${files})`)
    continue
  }

  failures += 1
  console.log(`FAIL  ${prefix} is requested but not proxied`)
  console.log(`      used by ${files}`)
  console.log(`      add '${prefix}' to backendPaths in ${CONFIG_SOURCE}`)
}

for (const prefix of [...proxied].sort()) {
  if (used.has(prefix)) continue

  failures += 1
  console.log(`FAIL  ${prefix} is proxied but requested by nothing`)
  console.log(`      remove it from backendPaths in ${CONFIG_SOURCE}, or a new endpoint`)
  console.log('      added under it would reach the SPA fallback with a 200')
}

console.log(
  failures === 0
    ? `\nall ${used.size} API prefix(es) agree with the proxy`
    : `\n${failures} prefix disagreement(s)`,
)
process.exit(failures === 0 ? 0 : 1)
