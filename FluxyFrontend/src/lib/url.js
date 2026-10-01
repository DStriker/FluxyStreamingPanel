import config from '../config'

/**
 * The one place an API path becomes a URL.
 *
 * It lives in its own module because two callers need it and they must not depend on each
 * other: `http.js` posts through it and `csrf.js` fetches the antiforgery token through it,
 * while `http.js` also imports `csrfHeader` from `csrf.js`. Putting `apiUrl` in either of
 * them would make that pair circular, and the cycle resolves in an order that silently
 * yields a stale binding - the kind of failure that looks like a wrong URL.
 *
 * This module imports nothing but `config`, so both of them can import it freely.
 */
export const apiUrl = (path) => `${config.API_BASE_URL}${path}`
