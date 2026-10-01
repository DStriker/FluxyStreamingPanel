const DEFAULTS = {
  REGISTER_ROUTE: 'register',
  CLIENT_LOGIN_ROUTE: 'login',
  RESELLER_LOGIN_ROUTE: 'reseller/login',
  ADMIN_LOGIN_ROUTE: 'admin/login',
  // Empty means "same origin as the page", which is what a production deployment behind one
  // host wants. The dev server needs an explicit base because the API is a separate process on
  // another port, and nothing here proxies for it.
  API_BASE_URL: '',
}

const env = import.meta.env ?? {}

const pick = (key) => {
  const value = env[`VITE_${key}`]
  return typeof value === 'string' && value.trim() !== '' ? value.trim() : DEFAULTS[key]
}

export const config = {
  REGISTER_ROUTE: pick('REGISTER_ROUTE'),
  CLIENT_LOGIN_ROUTE: pick('CLIENT_LOGIN_ROUTE'),
  RESELLER_LOGIN_ROUTE: pick('RESELLER_LOGIN_ROUTE'),
  ADMIN_LOGIN_ROUTE: pick('ADMIN_LOGIN_ROUTE'),
  RECAPTCHA_SITE_KEY: (env.VITE_RECAPTCHA_SITE_KEY ?? '').trim(),
  // Trailing slashes are trimmed so that joining a path never yields a doubled separator.
  API_BASE_URL: (env.VITE_API_BASE_URL ?? '').trim().replace(/\/+$/, ''),
}

export const routePath = (route) => (route.startsWith('/') ? route : `/${route}`)

export default config
