const DEFAULTS = {
  REGISTER_ROUTE: 'register',
  CLIENT_LOGIN_ROUTE: 'login',
  RESELLER_LOGIN_ROUTE: 'reseller/login',
  ADMIN_LOGIN_ROUTE: 'admin/login',
  // The public password reset, reachable from the footers of the three sign-in forms.
  // A guest route like the ones above, so it sits in `GuestOnly` in `src/router/routes.jsx`.
  FORGOT_PASSWORD_ROUTE: 'forgot-password',
  // Where each role lands after signing in. They mirror the `Auth` section of the backend's
  // appsettings.json, which is the authority - the server sends the path back in the
  // `redirect` field of its answer, so these exist for the router to have a matching route
  // and for a person reading the config to see where the three areas are meant to begin.
  // Each is the root of a guarded area in `src/router/routes.jsx`: `RequireAuth` checks the
  // role before `AccountLayout` renders, and the section pages hang off it as children.
  CLIENT_HOME_ROUTE: 'client/index',
  RESELLER_HOME_ROUTE: 'reseller/dashboard',
  ADMIN_HOME_ROUTE: 'admin/dashboard',
  // Empty means "same origin as the page", which is what a production deployment behind one
  // host wants. The dev server needs an explicit base because the API is a separate process on
  // another port, and nothing here proxies for it.
  API_BASE_URL: '',
} satisfies Record<string, string>

/** Every key the environment may override - the keys of `DEFAULTS`, and nothing else. */
type ConfigKey = keyof typeof DEFAULTS

/**
 * The environment, as a plain map.
 *
 * `import.meta.env` is typed by Vite, but this module is also bundled into the probes,
 * which run in Node where `import.meta.env` does not exist at all - hence the `?? {}`.
 * Typed as `Record<string, unknown>` rather than Vite's `ImportMetaEnv` so that the union
 * with `{}` still indexes, and so that reading a key produces `unknown` instead of `any`:
 * every value below is a string only after it has been checked for being one.
 */
const env: Record<string, unknown> = import.meta.env ?? {}

/** The configured value for `VITE_<key>`, trimmed, or the default when it is not a string. */
const pick = (key: ConfigKey): string => {
  const value = env[`VITE_${key}`]
  return typeof value === 'string' && value.trim() !== '' ? value.trim() : DEFAULTS[key]
}

/** The configured value as written, or `fallback` when it is absent or not a string. */
const raw = (key: string, fallback: string): string => {
  const value = env[key]
  return typeof value === 'string' ? value : fallback
}

export const config = {
  REGISTER_ROUTE: pick('REGISTER_ROUTE'),
  CLIENT_LOGIN_ROUTE: pick('CLIENT_LOGIN_ROUTE'),
  RESELLER_LOGIN_ROUTE: pick('RESELLER_LOGIN_ROUTE'),
  ADMIN_LOGIN_ROUTE: pick('ADMIN_LOGIN_ROUTE'),
  FORGOT_PASSWORD_ROUTE: pick('FORGOT_PASSWORD_ROUTE'),
  CLIENT_HOME_ROUTE: pick('CLIENT_HOME_ROUTE'),
  RESELLER_HOME_ROUTE: pick('RESELLER_HOME_ROUTE'),
  ADMIN_HOME_ROUTE: pick('ADMIN_HOME_ROUTE'),
  RECAPTCHA_SITE_KEY: raw('VITE_RECAPTCHA_SITE_KEY', '').trim(),
  // Trailing slashes are trimmed so that joining a path never yields a doubled separator.
  API_BASE_URL: raw('VITE_API_BASE_URL', '').trim().replace(/\/+$/, ''),
}

export const routePath = (route: string): string => (route.startsWith('/') ? route : `/${route}`)

/**
 * The area a route sits in - `admin/dashboard` -> `admin`.
 *
 * The landing paths above name where a role *enters* its area, and that is a page with a
 * name of its own in it. The area is what every section of it hangs from, and reading the
 * two as one string is what once put `profile` under the dashboard:
 * `/admin/dashboard/profile` instead of `/admin/profile`. One configured string, two
 * readings, so the two can never move apart.
 *
 * The `?? ''` is for `noUncheckedIndexedAccess`, which types `split('/')[0]` as possibly
 * missing. It cannot be: the split of a string always yields at least one element, empty
 * string included, so the fallback states what the runtime already guarantees.
 */
export const areaOf = (route: string): string => route.split('/')[0] ?? ''

/**
 * What is left of a landing path once its area is taken off - `admin/dashboard` ->
 * `dashboard`, `client` -> `''`.
 *
 * The empty answer is not an edge case to guard against but the other legitimate shape: a
 * landing path that *is* the area has no remainder, and that role's home is then the
 * area's index route rather than a page inside it.
 */
export const insideArea = (route: string): string => route.split('/').slice(1).join('/')

export default config
