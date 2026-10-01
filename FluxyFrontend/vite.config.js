import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import basicSsl from '@vitejs/plugin-basic-ssl'

export default defineConfig(({ mode }) => {
  // Read without the VITE_ prefix restriction only for clarity: the variable is dev-only
  // plumbing for the proxy below and never reaches the bundle.
  const env = loadEnv(mode, process.cwd(), '')
  const target = env.VITE_API_PROXY_TARGET || 'http://localhost:5159'

  // `/auth` is proxied so that the page and the API share an origin in development.
  //
  // This is not a convenience. The antiforgery cookies are `SameSite=Lax`, and a site is
  // scheme + registrable domain, so an `http://localhost:5173` page talking to
  // `https://localhost:7221` is a cross-*site* request: the browser withholds both cookies
  // from the POST and hides them from `document.cookie`, so `getCsrfToken` yields no token
  // and every submit is refused with `csrf_invalid`. Behind one origin the cookies are
  // first-party, SameSite does not apply, and CORS is not involved at all.
  //
  // The target must be the backend's **http** port (launch profile `http`). The `https`
  // profile would put `secure` on the cookies, and a browser receiving a `secure` cookie
  // over the plain http connection to :5173 discards it. The same profile also 307-redirects
  // the http port, which a proxied response must not do.
  // The dev server speaks https, so this header carries `https` and must not be left saying
  // `http`: the backend uses it purely as a marker that a terminator already chose the scheme,
  // and a marker that states the wrong scheme is worse than none. It is also what stops the
  // `https` launch profile from answering with a 307 - the browser would follow the redirect to
  // a different scheme, the call becomes cross-origin, and the antiforgery cookies come back
  // `secure` over a plain http connection and get discarded, which reads as `csrf_invalid` with
  // nothing wrong in the response body.
  const proxy = {
    '/auth': {
      target,
      headers: { 'X-Forwarded-Proto': 'https' },
    },
  }

  return {
    // `basicSsl` supplies a self-signed certificate so the dev server speaks https. That is what
  // makes the page and the backend the same scheme, which is the difference between a same-site
  // and a cross-site request: a site is scheme plus registrable domain, and ports play no part.
  // It also matches how this API is deployed, so the dev loop stops being a special case.
  //
  // Expect one browser warning about the certificate until you accept it once; Chrome remembers
  // the decision per host. Nothing to install into the machine's trust store.
  plugins: [react(), basicSsl()],
    server: {
      port: 5173,
      // Fail loudly instead of quietly taking the next free port. A silent move from 5173 to
      // 5174 changes the page origin, and a different origin means a different CORS decision
      // and a different rate-limit bucket - while the console still shows the dev server
      // "ready" on a port nothing is expecting. That drift is what made one CORS failure look
      // like an unfixable mystery for far too long.
      strictPort: true,
      open: true,
      proxy,
    },
    // `vite preview` does not read `server.proxy`, and without it an empty
    // `VITE_API_BASE_URL` resolves `/auth/*` against the preview server, which answers the
    // SPA shell with a 200. That is the silent wrong-response failure, so it is proxied too.
    preview: {
      port: 4173,
      strictPort: true,
      proxy,
    },
  }
})