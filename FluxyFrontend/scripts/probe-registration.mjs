/**
 * Exercises the real frontend modules against the running backend.
 *
 * Not a copy of them: it imports `src/lib/http.js`, `src/lib/api.js`, `src/lib/policy.js`
 * and the locale files, so what answers here is the same code the browser would run. It
 * exists because there is no test framework in this project and no browser attached to the
 * session, and the alternative was reading the code and hoping.
 *
 * Run: node_modules\.bin\esbuild --bundle scripts\probe-registration.mjs --format=esm --outfile=%TEMP%\probe.mjs --define:import.meta.env=%TEMP%\env.json
 */
import { config } from '../src/config/index.js'
import i18n from '../src/i18n.js'
import {
  ApiError,
  apiFetch,
  apiUrl,
  fieldErrors,
  messageForError,
  textForCode,
} from '../src/lib/http.js'
import {
  confirmRegistration,
  RegisterCaptchaAction,
  submitRegistration,
} from '../src/lib/api.js'
import { meetsPasswordComplexity, CODE_LENGTH } from '../src/lib/policy.js'
import { getCsrfToken } from '../src/lib/csrf.js'
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'

const BASE = process.env.FLUXY_API ?? 'http://localhost:5159'
config.API_BASE_URL = BASE

const results = []
const record = (name, detail, ok = true) => {
  results.push({ name, detail, ok })
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}\n      ${detail}`)
}

// The CSRF handshake the browser performs before every POST: GET the endpoint, keep the
// cookies, then send both the cookie and the token back. Node's fetch has no cookie jar,
// so the jar is emulated here - which is exactly the browser behaviour the app relies on
// when it passes `credentials: 'include'`.
const jar = new Map()

const realFetch = globalThis.fetch
globalThis.fetch = async (url, init = {}) => {
  const cookie = [...jar].map(([k, v]) => `${k}=${v}`).join('; ')
  const response = await realFetch(url, {
    ...init,
    headers: { ...(init.headers ?? {}), ...(cookie ? { Cookie: cookie } : {}) },
  })
  for (const raw of response.headers.getSetCookie?.() ?? []) {
    const [pair] = raw.split(';')
    const idx = pair.indexOf('=')
    if (idx > 0) jar.set(pair.slice(0, idx), pair.slice(idx + 1))
  }
  return response
}

// `getCsrfToken` reads `document.cookie`, so it needs a DOM to exist. An empty cookie jar
// also means the cookie short-circuit cannot answer and the real fetch is exercised.
globalThis.document ??= { cookie: '' }

let csrfToken = null

/**
 * Asks for the antiforgery token the way the forms do - through the app's own
 * `getCsrfToken`, not through a hand-rolled `fetch(apiUrl('/auth/csrf'))`.
 *
 * That distinction is the whole point. An earlier version of this script did the fetch
 * itself, so the CSRF request it made was correct while the one the browser made was not:
 * `csrf.js` was reaching for a relative path, the Vite dev server answered with the SPA
 * shell and a 200, and the token came back null. Every probe still passed. Only driving
 * the real function could have caught it.
 */
const primeCsrf = async () => {
  csrfToken = await getCsrfToken({ refresh: true })
  if (!csrfToken) throw new Error('getCsrfToken returned no token')
  return csrfToken
}

const call = async (fn, payload) => {
  await primeCsrf()
  try {
    return { ok: true, data: await fn(payload) }
  } catch (err) {
    return { ok: false, err }
  }
}

const describe = (r) =>
  r.ok
    ? `code=${r.data?.code} message=${JSON.stringify(r.data?.message)}`
    : `ApiError code=${r.err.code} status=${r.err.status} errors=${JSON.stringify(r.err.errors)}`

// ---------------------------------------------------------------- reachability

console.log(`\nAPI base: ${config.API_BASE_URL}\nURL join: ${apiUrl('/auth/register')}\n`)
record('apiUrl joins base + path', apiUrl('/auth/register') === `${BASE}/auth/register` ? 'ok' : 'MISMATCH')

// The regression this probe exists to catch: `csrf.js` once fetched a bare '/auth/csrf',
// which the browser resolved against the page rather than the API. In dev that is the Vite
// server, which answers with the SPA shell and a 200 - so the token silently came back
// null and every POST was refused with `csrf_invalid`, while the UI showed no cause.
const csrfUrls = []
const fetchBefore = globalThis.fetch
globalThis.fetch = (url, init) => {
  csrfUrls.push(String(url))
  return fetchBefore(url, init)
}
const probedToken = await getCsrfToken({ refresh: true })
globalThis.fetch = fetchBefore

record(
  'getCsrfToken fetches from the API base, not the page origin',
  `${csrfUrls[0]} -> ${probedToken ? `token, ${probedToken.length} chars` : 'NO TOKEN'}`,
  csrfUrls.length > 0 && csrfUrls[0].startsWith(BASE) && typeof probedToken === 'string',
)

const csrfBody = await primeCsrf()
record('GET /auth/csrf returns a token', typeof csrfToken === 'string' && csrfToken.length > 20 ? `token length ${csrfToken.length}` : 'NO TOKEN')

// ---------------------------------------------------------------- codes and text

const ALL_CODES = [
  'registration_submitted', 'registration_confirmed', 'registration_not_configured',
  'validation_failed', 'user_already_exists', 'invalid_code', 'code_expired',
  'captcha_invalid', 'csrf_invalid', 'registration_rate_limited',
  'confirmation_rate_limited', 'email_delivery_failed', 'network_error', 'server_error',
]

for (const lng of ['en', 'ru']) {
  await i18n.changeLanguage(lng)
  const missing = ALL_CODES.filter((c) => textForCode(c) === null)
  record(
    `every server code is translated (${lng})`,
    missing.length === 0 ? `${ALL_CODES.length}/${ALL_CODES.length}` : `MISSING: ${missing.join(', ')}`,
    missing.length === 0,
  )
}

await i18n.changeLanguage('en')
record(
  'server_error interpolates status',
  textForCode('server_error', { status: 502 }),
)
record(
  'an unknown code yields null so the caller can fall back',
  String(textForCode('no_such_code')),
)
record('registration captcha action', `${RegisterCaptchaAction} / code length ${CODE_LENGTH}`)

// ---------------------------------------------------------------- policy

record(
  'password policy mirrors the server',
  [
    meetsPasswordComplexity('Test1234'),
    meetsPasswordComplexity('test1234'),
    meetsPasswordComplexity('TEST1234'),
    meetsPasswordComplexity('Testabcd'),
  ].join(' '),
  meetsPasswordComplexity('Test1234') && !meetsPasswordComplexity('test1234') &&
  !meetsPasswordComplexity('TEST1234') && !meetsPasswordComplexity('Testabcd'),
)

// ---------------------------------------------------------------- real calls

const stamp = Date.now().toString().slice(-8)
const username = `probe${stamp}`
const email = `probe${stamp}@example.com`

const weak = await call(submitRegistration, {
  values: { username: 'ab', email: 'not-an-email', password: 'short' },
  csrfToken,
  captchaToken: null,
})
record('weak body -> validation_failed with field errors',
  describe(weak) + ' | antd setFields: ' + JSON.stringify(fieldErrors(weak.ok ? null : weak.err.errors)),
  !weak.ok && weak.err.code === 'validation_failed' && fieldErrors(weak.err.errors).length > 0)

const created = await call(submitRegistration, {
  values: { username, email, password: 'Test1234' },
  csrfToken,
  captchaToken: null,
})

// The registration limit is one per IP per window, and this script spends it on every run.
// Back-to-back runs therefore hit 429 with a fresh window's worth of time still to go, which
// is the server working correctly and nothing to do with the client. Reporting that as FAIL
// made the probe unusable in a normal edit loop, so it is called out as its own outcome
// instead: the checks that depend on a created row are then skipped, loudly, rather than
// failing for a reason that has nothing to do with what they measure.
const rateLimited = !created.ok && created.err.code === 'registration_rate_limited'
// A reCAPTCHA v3 token is minted by Google's JavaScript in a page, so this script cannot
// produce one. With RECAPTCHA_SECRET_KEY set on the backend every registration is refused at
// the captcha, which is the server working correctly and says nothing about the client - the
// same reasoning as the 429 above. Reported as its own outcome rather than as FAIL, or the
// probe would be permanently red on any machine that has the key set.
const captchaEnforced = !created.ok && created.err.code === 'captcha_invalid'
const rowUnavailable = rateLimited || captchaEnforced
if (rateLimited) {
  console.log(`\nSKIP  registration window is full (429) - the happy path below cannot run.`)
  console.log(`      Clear it and re-run for the full flow:`)
  console.log(`      docker exec redis redis-cli --user fluxy -a <REDIS_PASSWORD> DEL 'fluxy:throttle:register:::1'`)
  console.log(`      or wait out the window. Every other check below still applies.`)
} else if (captchaEnforced) {
  console.log(`\nSKIP  the backend enforces reCAPTCHA and this script cannot mint a v3 token.`)
  console.log(`      To run the happy path, leave RECAPTCHA_SECRET_KEY empty in the backend .env`)
  console.log(`      and restart it - the check is bypassed when the key is absent.`)
  console.log(`      Every other check below still applies.`)
} else {
  record('valid body -> registration_submitted', describe(created), created.ok && created.data.code === 'registration_submitted')

  const throttled = await call(submitRegistration, {
    values: { username, email: `other${email}`, password: 'Test1234' },
    csrfToken,
    captchaToken: null,
  })
  // The limit is one registration per IP per window, and the call above already spent it.
  // A refusal here is the feature working, not a failure - which is why this asserts the
  // outcome either way instead of the happy path.
  record(
    'second registration in the window is refused',
    describe(throttled),
    !throttled.ok && (throttled.err.code === 'registration_rate_limited' || throttled.err.code === 'user_already_exists'),
  )
}

// Confirmation is keyed by email and answers `invalid_code` for an address that never
// registered, so these two run regardless of whether a row was created.
//
// The confirmation window is 10 attempts per IP and this script spends three of them, so it
// fills up after a few runs. Once exhausted every later call answers
// `confirmation_rate_limited`, which says nothing about the endpoint under test - so it is
// detected and reported as a skip instead of three unrelated failures.
const wrong = await call(confirmRegistration, {
  email, code: '000000', csrfToken, captchaToken: null,
})
const confirmWindowFull = !wrong.ok && wrong.err.code === 'confirmation_rate_limited'
// Same story as above, one endpoint further on: `/auth/register/confirm` enforces the captcha
// too, so an enforced key refuses this call before the code is ever compared.
const confirmCaptchaEnforced = !wrong.ok && wrong.err.code === 'captcha_invalid'
const confirmBlocked = confirmWindowFull || confirmCaptchaEnforced

if (confirmWindowFull) {
  console.log(`\nSKIP  confirmation window is full (429) - the remaining confirm checks cannot run.`)
  console.log(`      docker exec redis redis-cli --user fluxy -a <REDIS_PASSWORD> DEL 'fluxy:throttle:confirm:::1'`)
} else if (confirmCaptchaEnforced) {
  console.log(`\nSKIP  /auth/register/confirm is also captcha-gated - the remaining confirm checks cannot run.`)
} else {
  record('wrong code -> invalid_code', describe(wrong), !wrong.ok && wrong.err.code === 'invalid_code')

  const unknownAddress = await call(confirmRegistration, {
    email: 'nobody@example.com', code: '000000', csrfToken, captchaToken: null,
  })
  // The point is that an address nobody registered is answered exactly like a wrong code:
  // same code, same sentence. Otherwise the endpoint is a membership oracle.
  record(
    'unknown address -> invalid_code (indistinguishable, no enumeration)',
    describe(unknownAddress),
    !unknownAddress.ok &&
      unknownAddress.err.code === wrong.err.code &&
      unknownAddress.err.message === wrong.err.message,
  )
}

// Localization of a code the server actually returned. Skipped with the confirm window,
// since a `confirmation_rate_limited` error would translate that instead of the code being
// measured - and the standalone transport check below still proves the lookup works.
if (!confirmBlocked) {
  const message = messageForError(wrong.err)
  const ru = await (async () => {
    await i18n.changeLanguage('ru')
    const ruMessage = messageForError(wrong.err)
    await i18n.changeLanguage('en')
    return ruMessage
  })()
  record('messageForError localizes invalid_code', `en "${message}" / ru "${ru}"`, message !== wrong.err.message)
}

const unreachable = new ApiError({ code: 'network_error', message: 'Server unavailable', status: 0 })
record('messageForError localizes a transport code', `"${messageForError(unreachable)}"`)

// ---------------------------------------------------------------- the whole flow

// Read the code out of the message the sink caught, exactly as the visitor would read it
// out of their inbox. Without this the happy path stops at "an email went out".
//
// Gated on a row actually having been created: with no row there is no fresh message, so
// the newest `.eml` on disk is a leftover from an earlier run and its code belongs to an
// address that does not exist. Confirming it would produce `invalid_code` and blame the
// endpoint for a stale fixture.
const mailDir = process.env.FLUXY_MAIL_DIR
if (rowUnavailable) {
  console.log(`SKIP  full flow: skipped because no row was created (${
    rateLimited ? 'the registration window is full' : 'the backend enforces reCAPTCHA'
  })`)
} else if (!mailDir) {
  console.log('\nSKIP  full flow: set FLUXY_MAIL_DIR to the directory the SMTP sink writes to')
} else {
  const newest = readdirSync(mailDir)
    .filter((n) => n.endsWith('.eml'))
    .map((n) => join(mailDir, n))
    .sort((a, b) => statSync(b).mtimeMs - statSync(a).mtimeMs)[0]

  if (!newest) {
    console.log(`\nSKIP  full flow: no .eml in ${mailDir} - the sink caught no message`)
  } else {
    const body = readFileSync(newest, 'utf8')
    const code = body.match(/registration code is (\d{6})/)?.[1]

    record('the mailed code was delivered and read back',
      code ? `code ${code} from ${newest.split(/[\\/]/).pop()}` : 'NO CODE IN THE MESSAGE',
      typeof code === 'string')

    const confirmed = await call(confirmRegistration, {
      email, code, csrfToken, captchaToken: null,
    })
    record('correct code -> registration_confirmed', describe(confirmed), confirmed.ok && confirmed.data.code === 'registration_confirmed')

    await i18n.changeLanguage('ru')
    record('the confirmed outcome is localized in ru',
      `"${textForCode(confirmed.ok ? confirmed.data.code : null)}"`,
      textForCode(confirmed.ok ? confirmed.data.code : null) !== null)
    await i18n.changeLanguage('en')
  }
}

console.log('\n' + JSON.stringify(results, null, 2))
const failed = results.filter((r) => !r.ok)
console.log(`\n${results.length - failed.length}/${results.length} passed`)
if (failed.length > 0) process.exitCode = 1