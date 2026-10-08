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
  // Every path prefix this backend serves. Adding an endpoint outside this list is the
  // documented silent failure: the dev server answers an unknown path with the SPA shell
  // and a **200**, so `res.ok` is true, the JSON parse yields null and the call reads as a
  // successful empty answer rather than as a miss. Nothing reports it. When a controller
  // lands outside `/auth`, add its prefix here in the same commit.
  const backendPaths = ['/auth', '/admin']

  // `/admin` is the one prefix that names something on both sides: the backend's admin API
  // (`/admin/users`, `/admin/users/{id}`, ...) and this SPA's own area - `/admin/dashboard`,
  // `/admin/profile`, and the three `/admin/users*` pages. A proxy routes by prefix, so
  // without a rule the admin area either stops loading (everything forwarded) or the API
  // answers with the SPA shell (nothing forwarded), and in production the same ambiguity
  // would need an order-dependent special case forever.
  //
  // The `Accept` header separates the two kinds of client exactly. A browser navigation
  // always asks for `text/html`; `apiFetch` and `csrf.ts` send no `Accept` at all, which
  // makes them `*/*`. So a request that wants a document is answered by Vite's own HTML
  // fallback - a string returned from `bypass` rewrites the URL and calls `next()`, which
  // is precisely "do not proxy this" - and everything else goes to the backend.
  //
  // The test must stay this way round: `*/*` is what every `fetch` sends, so reading "no
  // `text/html` means document" would hand HTML to the API and JSON to the router, silently
  // and in the worst possible direction. A deployment serving both from one origin has to
  // make the same split in its reverse proxy: `Accept: text/html` → the SPA, else → the app.
  const wantsDocument = (req) => (req.headers.accept ?? '').includes('text/html')

  const proxy = Object.fromEntries(
    backendPaths.map((path) => [
      path,
      {
        target,
        headers: { 'X-Forwarded-Proto': 'https' },
        bypass: (req) => (wantsDocument(req) ? req.url ?? '/' : undefined),
      },
    ]),
  )

  return {
    build: {
      // How the bundle is divided, and the two options that were measured to get here.
      //
      // This build used to be one URL: 1,048.45 kB / 339.22 kB gzip, the sign-in form and
      // the profile editor in the same file. One URL means one cache entry, and one cache
      // entry can only be either entirely old or entirely new - so every edit to this
      // repository re-sent antd, React and both locales to every returning visitor.
      //
      // Two changes fix different halves of that, and neither substitutes for the other:
      //
      //   1. route-level `React.lazy` (`src/router/routes.tsx`) - the nine pages and the
      //      auth forms are fetched when their address is asked for rather than with the
      //      shell;
      //   2. this `manualChunks` - four named groups, so what an edit to *this* repository
      //      invalidates is the entry chunk and not the whole framework.
      //
      // The alternative was built and is written down because it looks better than it is.
      // Dropping `vendor-ui` and letting Rollup place antd by the graph gives a *first*
      // load of 926 kB / 302 kB gzip instead of 1,036 kB / 338, because tree-shaking runs
      // before chunking and the entry then receives only the antd the entry renders. But
      // that layout puts the application code back inside the entry chunk as well, so an
      // app-only deploy re-downloads ~177 kB gzip where this one re-downloads ~11. This is
      // a signed-in application that is deployed often and read daily: the first number is
      // paid once per visitor, the second on every deploy. The entry chunk therefore stays
      // small (33 kB / ~11 kB gzip) and antd stays in a file only a version bump can
      // invalidate. The lazy routes add 19 kB / 9 kB gzip on top of the first load, and
      // only when an address asks for them.
      //
      // What it costs: the antd only the profile and dashboard render arrives with the
      // shell. There is no way to say "only the entry's antd" here - `manualChunks` is
      // handed a module id and nothing else, and antd's barrel statically re-exports every
      // component, so all of it is reachable from `main.tsx` no matter what the entry
      // actually renders. The same reason defeated an `app` chunk (it swallowed antd, 647 kB)
      // and a reachability walk over `importedIds` (identical 613.66 kB with and without the
      // lazy routes), which is why neither is in this file.
      rollupOptions: {
        output: {
          manualChunks: (id) => {
            // Application code is not named here on purpose - see the note above. It stays
            // in the entry chunk, which is the smallest thing that can be invalidated
            // without telling a chunk where a module is reached from.
            if (!id.includes('node_modules')) return undefined
            // Checked before React: `react-i18next` matches both tests, and it belongs with
            // its own runtime rather than with the framework.
            if (id.includes('i18next')) return 'vendor-i18n'
            // antd, its icons and the `rc-*` / `@rc-component/*` primitives it renders from.
            // Splitting any of them apart would leave a chunk whose only consumer is another
            // chunk of the same group.
            if (
              id.includes('antd') ||
              id.includes('@ant-design') ||
              id.includes('@rc-component') ||
              /[\\/]rc-[^\\/]+[\\/]/.test(id)
            ) {
              return 'vendor-ui'
            }
            // React, ReactDOM, the router and the scheduler they share a release train
            // with. After antd and not before: `react-router-dom` matches `react` as well,
            // and the only thing that matters is that one test wins.
            if (id.includes('react') || id.includes('scheduler')) return 'vendor-react'
            // The remainder of third-party code (dayjs, csstype, ...) - small, and shared by
            // the groups above. This bucket must stay small: an untargeted one is how
            // `Circular chunk: vendor -> vendor-react -> vendor` appeared when antd was left
            // in it, because it collects packages that import React and antd that imports
            // them back. With antd named above, nothing here does either.
            return 'vendor'
          },
        },
      },
    },
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