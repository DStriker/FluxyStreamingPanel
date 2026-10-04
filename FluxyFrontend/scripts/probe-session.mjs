/**
 * Exercises the signed-in session flow against the running backend: sign in, the session
 * itself, refresh rotation with reuse detection, and sign out.
 *
 * Not a copy of the frontend: it imports `src/lib/csrf.ts`, `src/lib/http.ts` and
 * `src/lib/api.ts`, so what answers here is the same code the browser runs. It exists
 * because the sign-out button was reported as doing nothing with `csrf_invalid`, and the
 * only way to tell whether that is the client, the transport or the server is to drive
 * the real modules end to end and look at the answer.
 *
 * Node has no cookie jar and no `document.cookie`, so both are emulated here with the
 * browser's own rules: HttpOnly cookies are kept out of `document.cookie`, everything the
 * server sets is replayed on the next request. Without that emulation the cookie
 * short-circuit in `getCsrfToken` could never fire and the probe would measure a path no
 * browser takes.
 *
 * Needs credentials, because the seeded administrator's password is random and printed
 * once: set FLUXY_ADMIN_PASSWORD (and FLUXY_ADMIN_USER if it is not `admin`).
 *
 * Run: npm run probe:session
 */
import { config, routePath } from '../src/config/index'
import i18n from '../src/i18n'
import { apiFetch, textForCode } from '../src/lib/http'
import { currentSession, signOut, submitAuth } from '../src/lib/api'
import { getCsrfToken } from '../src/lib/csrf'
import { homeForRole } from '../src/lib/session'

const BASE = process.env.FLUXY_API ?? 'http://localhost:5159'
config.API_BASE_URL = BASE

const ADMIN_USER = process.env.FLUXY_ADMIN_USER ?? 'admin'
const ADMIN_PASSWORD = process.env.FLUXY_ADMIN_PASSWORD ?? null
const TEST_LOGIN_LIMIT = process.env.FLUXY_TEST_LOGIN_LIMIT === '1'

const results = []
const record = (name, detail, ok = true) => {
  results.push({ name, detail, ok })
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}\n      ${detail}`)
}
const skip = (name, detail) => {
  results.push({ name, detail, ok: true, skipped: true })
  console.log(`SKIP  ${name}\n      ${detail}`)
}

// ---------------------------------------------------------------- cookie jar
//
// One jar for the request side (everything, HttpOnly included) and one for the page side
// (`document.cookie`, which by definition never shows an HttpOnly cookie). The split is
// the whole point: the access, refresh and antiforgery cookies are HttpOnly, and
// `XSRF-TOKEN` is the one cookie this application deliberately lets script read.
const jar = new Map()
const visible = new Map()

globalThis.document = {
  get cookie() {
    return [...visible].map(([k, v]) => `${k}=${v}`).join('; ')
  },
}

// Cookie plumbing is invisible until it breaks, and the one symptom it produces -
// `csrf_invalid` from a pair that looks correct - says nothing about which half is wrong.
// With FLUXY_PROBE_DEBUG=1 every request prints the cookies it sent and every response the
// cookies it set, so "which response dropped the antiforgery cookie" is a question with a
// printed answer instead of an inference.
const DEBUG = process.env.FLUXY_PROBE_DEBUG === '1'
const trace = (...args) => DEBUG && console.log(...args)

const realFetch = globalThis.fetch
globalThis.fetch = async (url, init = {}) => {
  const cookie = [...jar].map(([k, v]) => `${k}=${v}`).join('; ')
  const sent = [...jar.keys()]
  trace(
    `  -> ${init.method ?? 'GET'} ${new URL(url).pathname}` +
      ` csrf=${String(init.headers?.['X-CSRF-Token'] ?? '-').slice(0, 16)}` +
      ` cookies=[${sent.join(', ') || '(none)'}]`,
  )
  const response = await realFetch(url, {
    ...init,
    headers: { ...(init.headers ?? {}), ...(cookie ? { Cookie: cookie } : {}) },
  })

  const setCookies = response.headers.getSetCookie?.() ?? []
  trace(`  <- ${response.status} ${setCookies.map((c) => c.split(';')[0]).join(' | ') || '(no cookies)'}`)

  for (const raw of setCookies) {
    const [pair, ...attrs] = raw.split(';')
    const idx = pair.indexOf('=')
    if (idx <= 0) continue

    const name = pair.slice(0, idx).trim()
    const value = pair.slice(idx + 1)
    const httpOnly = attrs.some((a) => a.trim().toLowerCase() === 'httponly')

    // An empty value is how this backend deletes a cookie. Treating it as a removal keeps
    // the jar from replaying a token the server has just cleared - which would make the
    // probe stronger than the browser and hide exactly the class of bug it is here for.
    if (value === '') {
      jar.delete(name)
      visible.delete(name)
      trace(`      removed ${name}`)
      continue
    }

    jar.set(name, value)
    if (httpOnly) visible.delete(name)
    else visible.set(name, value)
  }

  return response
}

// ---------------------------------------------------------------- helpers

const call = async (fn, payload) => {
  try {
    return { ok: true, data: await fn(payload) }
  } catch (err) {
    return { ok: false, err }
  }
}

const describe = (r) =>
  r.ok
    ? `code=${r.data?.code} redirect=${r.data?.redirect ?? '-'}`
    : `ApiError code=${r.err.code} status=${r.err.status}`

const hasSessionCookies = () =>
  // The cookie names are the backend's business, not the frontend's - the page cannot even
  // see them, being HttpOnly. What can be asserted is that the response left a JWT-shaped
  // secret behind in the jar, which only a real pair of tokens would.
  [...jar.values()].some((v) => v.startsWith('ey'))

/** What `AreaPlaceholderPage` does: a fresh token, then the POST. */
const signOutFresh = async () => signOut({ csrfToken: await getCsrfToken() })

// ---------------------------------------------------------------- reachability

console.log(`\nAPI base: ${config.API_BASE_URL}\nadmin: ${ADMIN_USER}\n`)

if (!ADMIN_PASSWORD) {
  console.log('SKIP  FLUXY_ADMIN_PASSWORD is not set - the session checks cannot run.')
  console.log('      The seeded administrator password is random and shown once in the')
  console.log('      backend log ("Password: ..."), or set your own for a test account.')
  process.exit(0)
}

const csrf = await getCsrfToken()
record(
  'getCsrfToken issues a token',
  typeof csrf === 'string' ? `token, ${csrf.length} chars` : String(csrf),
  typeof csrf === 'string' && csrf.length > 20,
)

// ---------------------------------------------------------------- sign in

let login = await call(submitAuth, {
  action: 'admin-login',
  values: { username: ADMIN_USER, password: ADMIN_PASSWORD },
  csrfToken: csrf,
  captchaToken: null,
})

if (!login.ok && login.err.code === 'login_rate_limited') {
  console.log(`\nSKIP  the sign-in window is full (429) - the session checks cannot run.`)
  console.log(`      Clear it and re-run:`)
  console.log(
    `      docker exec redis redis-cli --user fluxy -a <REDIS_PASSWORD> DEL 'fluxy:throttle:login:Admin:::1'`,
  )
  console.log(`      or wait out the window.`)
  process.exit(0)
}

record(
  'sign-in issues a session',
  describe(login) + (hasSessionCookies() ? ' + access/refresh cookies' : ' + NO COOKIES'),
  login.ok && login.data.code === 'authenticated' && hasSessionCookies(),
)

let session = await call(currentSession)
record(
  'GET /auth/me reports the signed-in account',
  session.ok ? JSON.stringify(session.data) : describe(session),
  session.ok && session.data?.username === ADMIN_USER,
)

// ------------------------------------------------------------- the guest pages' redirect

// The three sign-in forms and registration are wrapped in `GuestOnly`, which asks the server
// who it already is and leaves through `homeForRole`. The guard itself needs a browser - it
// is an effect that fetches and navigates - but the rule it applies does not, and the half
// worth pinning is that the two copies of the landing rule agree: the server's `redirect`,
// which named the destination for this very sign-in, and `homeForRole`, which names it for
// a session that is already open in the browser. They must be one path, or a signed-in
// visitor and a freshly signed-in one would belong on different pages.
record(
  'homeForRole agrees with the redirect this sign-in answered with',
  `sign-in: ${login.data?.redirect} | session: ${
    session.data ? homeForRole(session.data.role) : 'no session'
  }`,
  login.ok && session.data && homeForRole(session.data.role) === login.data.redirect,
)

// A role the mapping does not know has to land on a non-guest route. A guest route would put
// a session that demonstrably exists straight back through the guard that just asked - a
// loop where a fallback was meant to be - which is why the client area answers, not login.
record(
  'homeForRole falls back to the client area for a role it does not know',
  `${homeForRole('Nobody')} / ${homeForRole(undefined)}`,
  homeForRole('Nobody') === routePath(config.CLIENT_HOME_ROUTE) &&
    homeForRole(undefined) === routePath(config.CLIENT_HOME_ROUTE),
)

// ---------------------------------------------------------------- sign out (the reported bug)

// The token held from before sign-in - what the old code read back out of the readable
// XSRF-TOKEN cookie. It was minted while the request was anonymous, and the antiforgery
// service binds a token to the identity that was current when it was minted, so presenting
// it now, alongside a session, is refused. That refusal - `csrf_invalid`, with "meant for
// a different claims-based user than the current user" in the server log - is exactly what
// the sign-out button was hitting: the cookie it read had been written on the sign-in
// page, before the session existed. Kept as a check because the rule is invisible from the
// client: the token looks perfectly valid, it is merely valid for someone else.
const stale = await call(signOut, { csrfToken: csrf })
record(
  'a token minted before sign-in is refused once signed in',
  describe(stale),
  !stale.ok && stale.err.code === 'csrf_invalid',
)

// ...which is why sign-in also has to drop the readable copy. A cookie that outlives the
// identity it was minted under would hand that refused token to the next submit that reads
// it instead of fetching - the same bug, waiting for its next caller.
record(
  'sign-in drops the readable XSRF-TOKEN copy',
  `present=${visible.has('XSRF-TOKEN')}`,
  !visible.has('XSRF-TOKEN'),
)

// What the sign-out button does now: `signOut()` with no arguments, which mints the token
// at click time - under the very session it is about to end - and sends it straight away.
// The stale refusal above leaves the session intact, so this one really ends it.
const out1 = await call(signOut)
record(
  'sign-out mints its own token and ends the session',
  describe(out1),
  out1.ok && out1.data.code === 'signed_out',
)

record(
  'sign-out drops the readable XSRF-TOKEN copy too',
  `present=${visible.has('XSRF-TOKEN')}`,
  !visible.has('XSRF-TOKEN'),
)

let after = await call(currentSession)
record(
  'GET /auth/me reports no session after sign-out',
  after.ok ? JSON.stringify(after.data) : describe(after),
  after.ok && after.data === null,
)

// A sign-out with no session is refused instead of played along with. It used to answer 200
// `signed_out`, and before that - for a caller presenting no antiforgery token - `csrf_invalid`,
// which named a missing header while the real answer was "there is nothing to end here". The
// endpoint now requires a session, so the authorization layer turns an anonymous caller away
// with `auth_required` before any check inside the action runs. The refresh attempted on the
// way (the new logout path in http.js) finds nothing to refresh and the original refusal
// comes back unchanged.
const out2 = await call(signOutFresh)
record(
  'sign-out without a session is refused with auth_required',
  describe(out2),
  !out2.ok && out2.err.code === 'auth_required',
)

// ---------------------------------------------------------------- refresh rotation

login = await call(submitAuth, {
  action: 'admin-login',
  values: { username: ADMIN_USER, password: ADMIN_PASSWORD },
  csrfToken: await getCsrfToken(),
  captchaToken: null,
})
record('second sign-in issues a session', describe(login), login.ok)

if (login.ok) {
  // What `http.js` does inside `refreshSession`: a fresh antiforgery token for the POST,
  // because the refresh endpoint checks the pair before it looks at the token.
  const refreshNow = async () =>
    apiFetch('/auth/refresh', { csrfToken: await getCsrfToken() })

  // Snapshots of the whole jar rather than a named cookie: which cookie holds the refresh
  // token is the backend's business, and "restore what the jar held before" needs no
  // knowledge of the name at all.
  const spent = new Map(jar)

  const rotated = await call(refreshNow)
  const live = new Map(jar)
  const changed = [...live].some(([k, v]) => spent.get(k) !== v)
  record(
    'refresh rotates the pair',
    `${describe(rotated)} changed=${changed}`,
    rotated.ok && rotated.data.code === 'authenticated' && changed,
  )

  session = await call(currentSession)
  record(
    'GET /auth/me works after a refresh',
    session.ok ? JSON.stringify(session.data) : describe(session),
    session.ok && session.data?.username === ADMIN_USER,
  )

  // The spent token presented a second time: replay or copy, indistinguishable, so the
  // whole session goes. The frontend must see a refusal it does not retry.
  for (const [k, v] of spent) jar.set(k, v)
  const replay = await call(refreshNow)
  record(
    'a replayed refresh token is refused',
    describe(replay),
    !replay.ok && replay.err.code === 'session_expired',
  )

  // And the family is dead, so the token the replay displaced cannot be used either.
  for (const [k, v] of live) jar.set(k, v)
  const revoked = await call(refreshNow)
  record(
    'the revoked family refuses its current token too',
    describe(revoked),
    !revoked.ok && revoked.err.code === 'session_expired',
  )
}

// ---------------------------------------------------------------- refusals are one refusal

const wrongPassword = await call(submitAuth, {
  action: 'admin-login',
  values: { username: ADMIN_USER, password: `${ADMIN_PASSWORD}x` },
  csrfToken: await getCsrfToken(),
  captchaToken: null,
})
record(
  'a wrong password is refused with invalid_credentials',
  describe(wrongPassword),
  !wrongPassword.ok && wrongPassword.err.code === 'invalid_credentials',
)

const unknownUser = await call(submitAuth, {
  action: 'admin-login',
  values: { username: `nobody${Date.now().toString().slice(-6)}`, password: 'Whatever123' },
  csrfToken: await getCsrfToken(),
  captchaToken: null,
})
record(
  'an unknown account is refused the same way',
  `${describe(unknownUser)} | same code: ${
    !unknownUser.ok && unknownUser.err.code === wrongPassword.err.code
  }`,
  !unknownUser.ok &&
    unknownUser.err.code === 'invalid_credentials' &&
    unknownUser.err.message === wrongPassword.err.message,
)

// ---------------------------------------------------------------- localized codes

for (const lng of ['en', 'ru']) {
  await i18n.changeLanguage(lng)
  const codes = ['authenticated', 'signed_out', 'invalid_credentials', 'session_expired', 'auth_required']
  const missing = codes.filter((c) => textForCode(c) === null)
  record(
    `session codes are translated (${lng})`,
    missing.length === 0 ? `${codes.length}/${codes.length}` : `MISSING: ${missing.join(', ')}`,
    missing.length === 0,
  )
}
await i18n.changeLanguage('en')

// ---------------------------------------------------------------- sign-in limit (opt in)

// Spends the whole window on purpose: after this every sign-in from this address is
// refused for fifteen minutes, so it runs last and only when asked for.
if (TEST_LOGIN_LIMIT) {
  // Two refusals above have already been counted against this window: the limit is per role
  // and address, a failed comparison spends an attempt, and only a successful sign-in resets
  // the window. So the sixth attempt of the window is the fourth one made here.
  let attempts = 2
  let limited = null

  while (attempts < 6 && !limited) {
    attempts += 1
    const attempt = await call(submitAuth, {
      action: 'admin-login',
      values: { username: ADMIN_USER, password: `${ADMIN_PASSWORD}x` },
      csrfToken: await getCsrfToken(),
      captchaToken: null,
    })
    if (!attempt.ok && attempt.err.code === 'login_rate_limited') limited = attempt
  }

  record(
    `the ${attempts}th sign-in attempt in one window is refused with login_rate_limited`,
    limited ? describe(limited) : `never throttled after ${attempts} attempts`,
    limited !== null,
  )
  if (limited) {
    console.log(
      `      The window is now full for 15 minutes. Clear it with:\n` +
        `      docker exec redis redis-cli --user fluxy -a <REDIS_PASSWORD> DEL 'fluxy:throttle:login:Admin:::1'`,
    )
  }
}

// ---------------------------------------------------------------- result

const failed = results.filter((r) => !r.ok)
console.log(`\n${results.length - failed.length}/${results.length} passed`)
if (failed.length > 0) process.exitCode = 1
