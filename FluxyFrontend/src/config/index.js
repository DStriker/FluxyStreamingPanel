const DEFAULTS = {
  REGISTER_ROUTE: 'register',
  CLIENT_LOGIN_ROUTE: 'login',
  RESELLER_LOGIN_ROUTE: 'reseller/login',
  ADMIN_LOGIN_ROUTE: 'admin/login',
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
}

export const routePath = (route) => (route.startsWith('/') ? route : `/${route}`)

export default config
