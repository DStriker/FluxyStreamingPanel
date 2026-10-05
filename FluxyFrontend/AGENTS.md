# AGENTS.md

Single-package Vite + React 19 SPA (antd 6, react-router-dom 7, react-i18next). Everything under `src/` is TypeScript — strict, checked by `tsc --noEmit`, with no test framework and no runtime type checking.

## Commands

```bash
npm run dev           # vite on https://localhost:5173, auto-opens the browser
npm run build         # tsc --noEmit, then vite build -> dist/
npm run typecheck     # tsc --noEmit only (build runs it first, so this is the fast loop)
npm run preview       # serve dist/
npm run lint          # eslint . — both blocks: **/*.{js,jsx} and **/*.{ts,tsx}; scripts/*.mjs matches neither
npm run favicon       # regenerate public/favicon.ico + favicon.svg from public/logo.svg
npm run probe         # exercise src/lib/* against a running backend (needs the backend)
npm run probe:session # sign in / refresh / sign out against a running backend (needs FLUXY_ADMIN_PASSWORD)
npm run probe:render  # render every page and assert on the HTML (no backend needed)
```

`npm run dev` opens a browser by default; for automation/debugging use `npm run dev -- --no-open` (matches the `dev (debug)` VS Code task, which the `launch.json` Chrome/Edge configs depend on).

Verify changes with `npm run typecheck`, `npm run lint` and `npm run build`, then `npm run probe:render` (see "Verifying without a test framework" — typecheck, lint and build all pass on markup that renders nothing).

## Structure

- `src/main.tsx` -> `src/App.tsx` -> `src/router/index.ts` (the history) and `src/router/routes.tsx` (the table)
- `src/App.tsx` owns the antd `ConfigProvider` (locale + dark algorithm). Components must be rendered under `<AntApp>` to use `App.useApp()`; `AuthForm` and `RegisterPage` rely on that.
- `src/components/AuthCard.tsx` is the shared frame (language switcher, logo, card, `Form`). Both forms render through it, so a second form looks like the first without copying the layout. It **must** render its `footer` prop — it is the last child inside the `<Form>`, and that is where the cross-link between the two flows has always lived. Dropping it is invisible to `typecheck`, `lint` and `build`, which is how it went missing once.
- `src/components/AuthForm.tsx` is the shared credentials form, generic in the values its page collects (see Gotchas). The three login pages in `src/pages/` are thin wrappers that pass `action` + `onSubmit`; add behavior there, not by copying the form.
- `src/components/ConfirmCodeForm.tsx` is the second step of registration. It is separate from `AuthForm` because the two share no fields but differ in endpoint, captcha action and rate limit.
- `src/components/GuestOnly.tsx` wraps the five guest routes (three sign-ins, registration, password reset) where they are declared in `src/router/routes.tsx`. A visitor who already holds a session is sent to their own area instead of being shown a form that would stack a second session over a first that nothing can end anymore. It renders its children while `GET /auth/me` is in flight — a login form must not sit behind a round trip — and the destination comes from `homeForRole` in `src/lib/session.ts`.
- `src/config/index.ts` holds all route paths, the reCAPTCHA key and `API_BASE_URL`, read from `import.meta.env`. Never hardcode route strings in pages — use `config.X_ROUTE` and `routePath()`.
- `src/lib/http.ts` is the transport: `apiFetch`, the `ApiError` shape, code→text lookup and `fieldErrors`. `src/lib/api.ts` is the endpoint list on top of it, and it exports the value shapes (`SignInValues`, `RegistrationValues`, `CallOptions`) the forms name at their call sites. `src/lib/policy.ts` mirrors the server's `RegistrationPolicy`. `src/lib/session.ts` holds `homeForRole`, the frontend's copy of the server's landing rule (`LandingPathOf`), used by the guest guard, and `areaForRole`, the prefix that landing path sits inside and that every section address is built from. `src/lib/url.ts` holds `apiUrl` — see the circular-import note in Gotchas before moving it.
- `src/types/` is the vocabulary: **one exported type per file**, with `index.ts` re-exporting all of them so a consumer writes `import type { Session } from '../types'`. Only types live there — importing a value from `src/types` is a sign the type belongs next to the code that uses it.
- `src/locales/en.ts` and `ru.ts` are plain nested objects used as i18next resources. `en.ts` exports `type Translation = typeof en` and `ru.ts` annotates itself with it, which is what makes a key present in one file and missing from the other a compile error.

## Registration is two steps on one page

`RegisterPage` holds `step` and the address carried between the steps. `AuthForm` calls `onSuccess(result, values)` once the server answers 2xx, which is what advances to `ConfirmCodeForm`. There is **no `/register/confirm` route** — the visitor stays on `/register` and the page swaps the form underneath them.

The address is mirrored into `sessionStorage` (`src/lib/pendingRegistration.ts`) so a reload between the steps resumes instead of throwing the visitor back to the first form. It is a convenience, never a source of truth: the server keys confirmation by address alone, so a stale value costs a re-typed email. Cleared on `registration_confirmed` and on `code_expired` — the latter also drops back to step one, because an expired code cannot be revived by typing a better one.

`captchaAction` is a prop of `AuthForm` separate from `action`. The server fixes one captcha action per endpoint, so the confirm form passes `ConfirmCaptchaAction` from `src/lib/api.ts`, while the login pages still let `action` serve both purposes.

## The server code is the branch, not the text

Every response is `{ code, message, errors }`. The client branches on `code` and shows `messages.api.<code>` from the locale files; `message` is used only when a code has no translation yet. `network_error` and `server_error` are the two codes the client invents for failures that never reached the server.

Adding a code to `RegistrationResponses.cs` on the backend without adding it to `messages.api` in both locales breaks nothing — it degrades to the server's English sentence. It should still be added, because the point of the code is to be translated.

`errors` is keyed by the camelCase JSON property name (`username`, `email`, `password`), which is deliberately identical to the antd `Form.Item` names, so `fieldErrors()` feeds `form.setFields` with no translation step.

## The API shares an origin with the page, and that is load-bearing

Nothing here is about convenience. The antiforgery cookies the backend sets are `SameSite=Lax` (`AntiforgeryExtensions.cs` for the framework cookie, `AuthController.GetCsrfToken` for the readable `XSRF-TOKEN`), and a *site* in the SameSite sense is scheme **plus** registrable domain. So an `http://localhost:5173` page calling `https://localhost:7221` is a **cross-site** request even though both hosts read as "localhost". A browser withholds Lax cookies from cross-site subresource requests — `credentials: 'include'` does not change that — and hides them from `document.cookie`, so `getCsrfToken` finds no cookie and returns no token either. Both halves of the pair are lost and every submit comes back `csrf_invalid`.

Switching the base URL to `https://localhost:7221` does not rescue it: the scheme still differs. It only looks like it helps because the `https` launch profile answers the http port with a **307**, and a `fetch` cannot follow that without `Access-Control-Allow-Origin` on the redirect, so the http URL fails differently and worse.

So there are three ways to serve the API in development, and only one of them keeps `SameSite=Lax`:

| Setup | SameSite | Verdict |
| --- | --- | --- |
| Vite proxy over https, `VITE_API_BASE_URL` empty | first-party, irrelevant | **What this repo does** |
| Page on `https://localhost:5173`, API on `https://localhost:7221` | same scheme, so same site | Works, but needs a Node-trusted cert |
| Page on `http`, API on `https` (or the reverse) | cross-**site** | Never works while the cookies are Lax |

Making it work without the proxy would mean either weakening the backend to `SameSite=None; Secure` — which degrades CSRF protection for production, where it is unnecessary, and leaves the app on an insecure origin — or giving the SPA its own trusted certificate. The proxy avoids both and matches the production topology, which is one origin behind a reverse proxy.

### The dev server speaks https, via `@vitejs/plugin-basic-ssl`

`server.https` is on, so the page is `https://localhost:5173` — the same scheme the API is served under in production. The plugin generates a self-signed certificate on first run and caches it at `node_modules/.vite/basic-ssl/_cert.pem`; nothing goes into the machine's trust store. Expect **one** browser warning about the certificate until you accept it, after which Chrome remembers it per host.

This is not cosmetic. With the page on `http` and the backend on `https` — which is exactly what Visual Studio's `https` launch profile produces — the two are different schemes, which is the class of mismatch that produced the `307` → cross-origin → refused chain described under the middleware notes in `AGENTS.md` of the backend. Both halves being https removes the whole category rather than papering over its current instance. It also means the local dev loop is no longer a special case that behaves differently from a deployment.

The proxy target stays the backend's **http** port (`VITE_API_PROXY_TARGET`, default `http://localhost:5159`). The hop from the proxy to the backend is server-to-server on loopback and invisible to the browser, so its scheme has no bearing on SameSite; the browser only ever sees `https://localhost:5173`.

**`npm run probe` goes through `scripts/run-probe.mjs`, not straight to `node`.** Node's `fetch` does not trust a self-signed certificate, so every request through the https dev server failed with `DEPTH_ZERO_SELF_SIGNED_CERT` — and the symptom was badly misleading: `getCsrfToken` treats a network error as "no token" and returns `null`, so the probe reported a failing check *about the CSRF URL* when nothing was wrong with it. The runner spawns the bundle with `NODE_EXTRA_CA_CERTS` pointing at the generated certificate, which is read once at process start and therefore cannot be set from inside the probe. Do not "simplify" this to `NODE_TLS_REJECT_UNAUTHORIZED=0`: that turns off verification for every host the process talks to and hides a genuinely wrong certificate, which is the one thing worth seeing. With the runner in place the probe is 11/11 over `https://localhost:5173` and 11/11 over a plain `http://localhost:5159`; the http path involves no certificate at all, which is a useful control that the fix did not break the no-TLS case.

One more consequence worth remembering: `npm run probe` runs in Node, which has no cookie or SameSite semantics at all — the script has to fake a cookie jar. It can therefore verify the proxy forwards correctly and the whole flow completes, and it did (11/11 through `https://localhost:5173`, and 11/11 over plain `http://localhost:5159`), but it **cannot** detect this class of bug. Only a real browser can, and every check here was made without one.

That gap has a sharp edge worth naming: a Node-only failure can *masquerade* as an application bug. When the dev server turned to https, every probe request failed with `DEPTH_ZERO_SELF_SIGNED_CERT`, `getCsrfToken` converted that to `null`, and the check that reported it was the one asserting `getCsrfToken` uses the API base — pointing at a module that had not changed and was not wrong. A check that fails for a transport reason while describing a logic property is the most expensive kind of failure to read, so check the transport before believing the assertion.

## Verifying without a test framework

There are no tests. `typecheck`, `lint` and `build` are not enough on their own, and this repo has the receipts: `AuthCard` accepted a `footer` prop that four pages passed and it silently rendered nothing. Lint passed, the build succeeded, and the link existed nowhere in the DOM. Reading the code did not catch it. Rendering it did.

So there are three probes, all bundling the **real** `src/` modules with esbuild and printing PASS/FAIL per check. Every esbuild line in `package.json` carries `--jsx=automatic`, because the sources now include `.tsx`: esbuild's own default emits `React.createElement`, which in a Node bundle is a bare reference to a `React` nothing there defines. The automatic runtime instead emits an import esbuild can resolve and bundle.

### `npm run probe` — against a running backend

`scripts/probe-registration.mjs` exercises the transport, the code→text lookup and the real endpoints.

```bash
# through the dev server (https, the certificate is trusted by scripts/run-probe.mjs)
set FLUXY_API=https://localhost:5173

# or straight at the backend - only with the `http` profile, since :5159 answers a
# 307 to :7221 under the `https` profile and Node does not trust that certificate
set FLUXY_API=http://localhost:5159

# the backend needs SMTP configured, otherwise step 1 answers 503
# and the confirmation step is never reachable
set FLUXY_MAIL_DIR=<dir an SMTP sink writes .eml into>   # optional; skips the full flow
npm run probe
```

`FLUXY_MAIL_DIR` is what lets it read the confirmation code out of a caught message and confirm for real; without it every error path still runs.

Two things about how it is written, both learned the hard way:

- **It drives the app's own `getCsrfToken`, not a hand-rolled fetch of `/auth/csrf`.** The earlier version fetched the token itself, which meant the request the probe made was correct while the request the browser made was not — `csrf.ts` was reaching for a relative path and every probe still passed. A probe that re-implements the code under test verifies the probe.
- **It emulates a cookie jar.** Node has none, so the script wraps `globalThis.fetch` to keep and replay cookies. That is not decoration — the antiforgery check needs both the cookie and the header token, and without it every POST comes back `csrf_invalid`. It also shims `document.cookie`, which `getCsrfToken` reads, left empty so the cookie short-circuit cannot answer and the real fetch is exercised.

The rate limits make repeated runs awkward, so the script reports a full window as a **SKIP** with the `redis-cli DEL` command to clear it, rather than as a failure. A 429 is the server working correctly and says nothing about the client; failing on it made the probe useless in an ordinary edit loop. Cleared windows: `fluxy:throttle:register:::1` and `fluxy:throttle:confirm:::1` (`::1` is the loopback client, and the colons belong to the IPv6 address).

An enforced reCAPTCHA is skipped for the same reason. A v3 token is minted by Google's JavaScript in a page, so the script cannot produce one: with `RECAPTCHA_SECRET_KEY` set in the backend `.env`, both `POST /auth/register` and `POST /auth/register/confirm` answer `captcha_invalid` before doing anything measurable. That is the server working correctly, so it is a SKIP with the hint to leave the key empty, not a FAIL. Without this the probe is permanently red on any machine that has the key set, which is exactly the machine where someone would stop trusting it.

### `npm run probe:session` — sign in, refresh, sign out

`scripts/probe-session.mjs` drives the real session flow with the seeded administrator through the app's own `submitAuth`, `signOut`, `currentSession` and `getCsrfToken`: sign-in and `GET /auth/me`, the sign-out the placeholder page performs (with and without an explicit token — and with no session at all, which the endpoint refuses with `auth_required`, since logout is for signed-in visitors only), refresh rotation with reuse detection — a replayed refresh token kills the whole family, current token included — the identical `invalid_credentials` refusal for a wrong password and an unknown account, and the translation of the session codes.

It needs credentials, because the seeded password is random and printed once:

```bash
set FLUXY_ADMIN_PASSWORD=<the "Password: ..." line from the backend log>
# optional: FLUXY_ADMIN_USER (default admin), FLUXY_TEST_LOGIN_LIMIT=1 (spends the whole
# 15-minute login window, so it runs last), FLUXY_PROBE_DEBUG=1
npm run probe:session
```

It also covers the guest pages' landing rule: `homeForRole` must name the same path the sign-in was answered with in `redirect`, and must fall back to a non-guest route for a role it does not know (see the Gotcha on the guard).

Two checks exist for the sign-out bug this probe was written after: a token minted before sign-in is refused once signed in (the antiforgery claims binding — the server log names the reason), and sign-in and sign-out both drop the readable `XSRF-TOKEN` copy so nothing can read that refused token back. The third is the fix: `signOut()` mints at click time, under the session it is about to end. `FLUXY_PROBE_DEBUG=1` prints the cookies each request sent and each response set, which is how the `csrf_invalid` was traced to a specific response instead of inferred — the Node jar has no SameSite semantics, so, as with `probe`, a browser is still the only thing that can check those.

### `npm run probe:render` — no backend needed

`scripts/probe-render.jsx` renders each page with `renderToStaticMarkup` and asserts on the HTML: that the footer cross-link is present where one is meant to be, absent where it is not, that `ConfirmCodeForm` is not dead code, and that the guest guard still shows the form while its session check is in flight. Needs neither a backend nor a mail server, so run it first.

Two things it had to get right, both of which produced a false failure before being fixed:

- **React escapes `'` as `&#x27;`** even in text content, so "Don't" reaches the DOM escaped. Comparing rendered HTML against the source string fails on a correct page; the script decodes entities before matching.
- **The script is `.jsx`, not `.mjs`,** because `eslint.config.js` enables `jsx` for `**/*.{js,jsx}` only and `npm run lint` would otherwise reject its own JSX.

All five guest pages have a footer, and what each one carries is a fact about the app rather than an oversight. `ClientLoginPage` and `RegisterPage` carry the cross-link between registration and sign-in; `ResellerLoginPage` and `AdminLoginPage` were extended to offer the password-reset link (they had no footer at all before that, which is what an earlier version of this note recorded); `ForgotPasswordPage` carries "back to sign in" in all three of its states. `probe-render.jsx` asserts `hasFooter: true` for all five, so a `footer` prop that silently renders nothing fails there — that is the check, and asserting a footer *absent* on any of them would now be the wrong expectation.

## Gotchas

- **`CODE_STANDARDS.md` is a generic template; where the two disagree, this file wins.** It prescribes Tailwind, Vitest/RTL/Playwright/jest-axe, Storybook, `src/features/[feature]/` and **named exports only**. This repository deliberately has none of those: styling is antd 6 plus `src/index.css`, verification is the three `npm run probe*` scripts, structure is `src/components|pages|lib|config|types|locales|hooks|router`, and **`export default` is the convention here** — every page and component uses it and `react-refresh` is fine with it. Read its sections on React hooks, TypeScript safety, accessibility and performance as applicable; read its Tailwind, testing, feature-structure and named-export rules as inapplicable. Do not "fix" default exports to named exports: that is a repo-wide diff with no benefit, and the standard is what needs the correction, not the code.
- **The tree is TypeScript, and `tsc` runs before every build.** `npm run build` is `typecheck && vite build`; `tsconfig.json` is `strict` with `noUncheckedIndexedAccess` and `isolatedModules`, `include: ["src"]` and `allowJs: false`. Nothing under `src/` may be `.js`/`.jsx` again — a half-checked JavaScript file sitting in an included tree is the exact state the flag exists to prevent. `npm run typecheck` is the fast loop: it is what fails on a bad import or a forgotten prop long before the bundle does, and it is not a substitute for `npm run probe:render`, which is the only thing that notices markup rendering nothing.
- **The i18n keys are deliberately untyped; the locale files are not.** `t('profile.typo')` compiles and falls back at runtime, which is accepted because the keys are written twice and the two copies must agree: `en.ts` exports `type Translation = typeof en` and `ru.ts` declares itself `: Translation`, so a key present in one file and missing from the other is `TS2353` on the line it happened. Adding a translation means both files, and that type is what makes it an error rather than a missing string.
- **`AuthForm` and `ConfirmCodeForm` are generic in the values their page collects, and neither has a default.** A page that writes `<AuthForm>` alone infers `unknown`, and its first field read fails to compile — that is the design: the alternative is a `values` typed as whatever the component guessed, which compiles happily and is `undefined` at the first submit. Name the shape at the call site: `SignInValues` or `RegistrationValues` from `src/lib/api.ts` for `AuthForm`, and `RegistrationConfirmValues` or `ResetConfirmValues` from the component for `ConfirmCodeForm`.
- **`useSession()` answers `SessionContextValue | null` and never throws without a provider.** `npm run probe:render` renders `ProfilePage` with no provider at all, so a hook that threw would take the probe down instead of describing the page. Consumers narrow instead: `AccountHeader` and `AccountSidebar` write `if (!session) return null` — only `RequireAuth` renders them, so `null` there means the guard was bypassed rather than that nobody is signed in, and the comment above each says so — while everything else reaches through `session?.`.
- **`src/config/index.ts` reads `import.meta.env ?? {}`.** The probes bundle `src/` for Node, where `import.meta.env` does not exist at all: the fallback is what keeps those bundles running, and typing it as `Record<string, unknown>` behind two narrow helpers (`pick`, `raw`) is what keeps `tsc` honest about a value that genuinely is not always there. Vite inlines the real values at build time, so the type is a description of a build step rather than of the object at hand.
- **The route table is annotated `RouteObject[]`, not inferred.** `index: true` widens to `boolean` the moment nothing says otherwise, and an entry carrying both a `path` and an `index` is a contradiction the union only reports once a union is being asked for — which, unannotated, would be the first consumer of the table rather than the line the entry sits on.
- **Every request to the API must go through `apiUrl`.** There are exactly two `fetch` calls in the app — one in `csrf.ts`, one in `http.ts` — and both build the URL with `apiUrl(path)`. Nothing may fetch a bare `/auth/...`.
  This is not a style rule. A relative path resolves against whatever serves the page, which in development is the Vite server; it answers an unknown path with the SPA shell and a **200**, so `res.ok` is true, the JSON parse fails, and the token silently comes back `null`. Every later POST is then refused with `csrf_invalid` and the UI shows no cause. It happened once already.
- **`apiUrl` lives in `src/lib/url.ts` and must stay there.** `http.ts` imports `csrfHeader` from `csrf.ts`, and `csrf.ts` needs `apiUrl` too — so defining `apiUrl` in either of them closes an import cycle. A cycle does not necessarily throw here; it can resolve in an order that yields a stale binding, which surfaces as a wrong URL rather than a crash. The module exists to break that, and it imports nothing but `config`.
- **`VITE_API_BASE_URL` must stay empty in development.** `vite.config.js` proxies `/auth` to the backend (`VITE_API_PROXY_TARGET`, default `http://localhost:5159`), and `preview` proxies it too, so the page and the API share one origin. That is not a convenience — see the SameSite note below. `credentials: 'include'` is still set on both fetches; it is a no-op same-origin and load-bearing if a deployment ever splits the origins.
- **Either launch profile works, and that is deliberate — do not "fix" it back to one.** The proxy sets `X-Forwarded-Proto: https`, which is how the backend recognises a proxied request and skips `UseHttpsRedirection` for it (the branch is Development-only; outside it the header is attacker-controlled). Without that header the `https` launch profile answers the http port with a **307** to `https://localhost:7221`, and the proxy relays the redirect verbatim. The browser follows it to a different **scheme**, the call becomes cross-origin, needs a preflight, and the antiforgery cookies come back `secure` over a plain http connection and get discarded — which reads as `csrf_invalid` with nothing wrong in the response body. Measured on the `https` profile: proxied `/auth/csrf` was `307`, then after this change `200` with `XSRF-TOKEN` and **no** `secure` flag, and the POST advanced to `captcha_invalid`, which is the point past CSRF. Visual Studio launches the `https` profile by default, so a rule that only works under the `http` profile would be a rule that breaks again on the next F5.
  The header value must state the scheme the **browser** used, not the one the proxy used to reach the backend. The backend reads it only as a marker that a terminator already decided, so `http` there would be a marker stating the wrong scheme — worse than no header at all, because it would suppress the redirect while misreporting what happened.
- **`strictPort: true` is set on `server` and `preview` on purpose.** Vite's default is to take the next free port, and a silent move from 5173 to 5174 changes the page origin — hence a different CORS decision and a different rate-limit bucket — while the console still prints "ready". This is not hypothetical: a stray second dev server on 5173 pushed the real one to 5174, and that port drift was half of what made the CORS failure above look like an unfixable mystery. The port has to be a fact, not a suggestion.
- **API paths carry no `api/` segment.** The backend serves nothing but the API, so the prefix was dropped there (`[Route("auth")]` in `FluxyBackend/Fluxy.API/Controllers/AuthController.cs`). Both halves hardcode their own copy — `/auth/${action}` in `src/lib/api.ts` and `TOKEN_URL` in `src/lib/csrf.ts` — so a rename has to be made in two repositories at once or registration silently 404s.
- **The three login endpoints exist and are per-role.** `submitAuth` posts to `/auth/client-login`, `/auth/reseller-login` and `/auth/admin-login`; each refuses an account of a different role with the same `invalid_credentials` a wrong password gets, and each entrance has its own window (5 attempts per 15 minutes per IP and role, keyed without the username). An earlier version of this note said all three 404 — they were filled in when the JWT + refresh-token session was built, and `npm run probe:session` exercises them against a live backend.
- **The profile page has a login guard card next to the time zone card.** `LoginGuardCard` edits the two switches and the three allow lists (`changeLoginGuard` posts to `/auth/profile/geo`, `lookupGeo` reads `/auth/geo/lookup` for the "use my current network" button) and joins the same confirmation step as the other changes — a `profile_change_submitted` answer swaps the page to `ConfirmCodeForm` with `kind: 'guard'`, no rename afterwards. The form item names are the server's camelCase field names (`allowedIps`, `allowedCountry`, `allowedAutonomousSystemNumber`), so `fieldErrors()` lands rejections under their inputs with no translation step. Limits mirror the server (`LOGIN_GUARD_MAX_IPS` in `src/lib/policy.ts`); the server stays the authority.
- **The guest pages are guarded in the router, not inside the pages.** `GuestOnly` asks `/auth/me` once per navigation and leaves through `homeForRole(session.role)` — by role, never by entrance: an admin at `/login` belongs on the admin area, a client at `/admin/login` on the client one, because that is where signing that account in would land it. Two properties are easy to break without anything failing loudly. The fallback for an unknown role must be a **non-guest** route — a session that demonstrably exists sent back to login would pass through the guard that just asked and come straight back, which is a loop and not a fallback. And the destination must come from this app's own route table rather than a path the server sent, so a path the two sides disagreed about cannot bounce a signed-in visitor between the catch-all and the guard. The two copies of the landing rule agreeing is asserted by `npm run probe:session`: `homeForRole(role)` must equal the `redirect` the sign-in itself was answered with.
- **`homeForRole` and `areaForRole` answer different questions, and building a section URL from the wrong one is how `/admin/dashboard/profile` happened.** The landing path the server names is a *page* — `admin/dashboard` — while the area every section hangs from is only its prefix — `admin`. So the router's area path, the sidebar's menu keys and the header's profile shortcut all take `areaForRole`; `homeForRole` is where a sign-in opens and nothing else. Both are cut from the same `config.*_HOME_ROUTE` by `areaOf` / `insideArea` in `src/config/index.ts`, so neither can move without the other, and neither the server's `Auth:LandingPath*` nor the landing paths themselves changed when the nesting did. `npm run probe:render` asserts the result twice: every menu key must match a declared child route, and `matchRoutes` must resolve each of them to that role's own area rather than to the catch-all — a nested path still resolves, just to the 404, so string comparison alone would have passed it.
- **Page `action` is the reCAPTCHA action and comes from a `*CaptchaAction` constant; the submit path is kebab-case.** `ClientLoginPage` passes `action={ClientLoginCaptchaAction}` (`'client_login'`, the string the server expects to find in the token) and submits to `action: 'client-login'`. The two spellings still have to agree when a flow is added — the captcha half is now a constant in `src/lib/api.ts` rather than a literal copied into five pages, but the endpoint is still written where it is used.
- **reCAPTCHA is disabled in dev** (`getCaptchaToken` returns `null` when `import.meta.env.DEV`) and skipped entirely when the site key is empty. Captures in prod only.
- **`getCsrfToken` always mints a fresh token — never a cached one, never the cookie read back.** It is best-effort only in the failure direction: it silently returns `null` when the backend is absent, and that silence is load-bearing for the login pages. It is also what hid the wrong-URL bug above, which is why the probe asserts the URL `getCsrfToken` actually requests.
  The freshness is a correctness rule, not a preference. The antiforgery service binds a token to the identity that was current when it was minted: a token obtained on the sign-in page is refused after sign-in, with `csrf_invalid` and `"The provided antiforgery token was meant for a different claims-based user than the current user"` in the server log. That is exactly what killed the placeholder's sign-out button — it read the `XSRF-TOKEN` cookie written before the session existed, the POST came back 400, and `handleSignOut` had no `catch`, so nothing appeared to happen at all. Two fixes close it from both sides: every submit now mints immediately before sending, and the backend drops the readable `XSRF-TOKEN` copy whenever the session changes (`AuthCookies.Write`/`Clear`), so a cookie cannot outlive the identity it was minted under either. Both halves — and the refusal of the stale token, which is the rule itself — are asserted by `npm run probe:session`.
- **The app renders two assets: `public/logo.jpg` (both `AuthCard` and `AccountHeader`) and `public/logo.svg` (the favicon generator).** All of `public/` is tracked, so a fresh clone renders both. Two files sit in the *repository root* — `logo.jpg` and `reflux_logo.png` — and nothing in `src/` or `scripts/` references them; `public/logo.png` is likewise referenced by nothing. Do not "wire up" any of the three, and do not assume a root `logo.jpg` is what `/logo.jpg` serves.
- `index.html` hardcodes `lang="ru"`; the real language comes from i18next (`localStorage` key `fluxy-lang`, then `navigator.language`, default `en`).
- `.env` is gitignored; `.env.example` is the committed template (it documents `VITE_API_BASE_URL`, the route vars and the reCAPTCHA key). Env vars are inlined at build time, so changing them requires a rebuild, not just a dev-server restart.
- `dist/` is committed/present locally and ignored by eslint. Don't edit build output.
- Comment language in this repo is mixed Russian/English; `csrf.ts` and `launch.json` contain Russian comments. Keep the surrounding style when editing existing lines. **Commit messages are English-only and short** — one line for the subject, a body of a few lines at most.
