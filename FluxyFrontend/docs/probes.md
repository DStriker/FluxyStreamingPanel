> Part of the FluxyFrontend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).

## Verifying: Vitest for pure logic, probes for the rest

`typecheck`, `lint` and `build` are not enough on their own, and this repo has the receipts: `AuthCard` accepted a `footer` prop that four pages passed and it silently rendered nothing. Lint passed, the build succeeded, and the link existed nowhere in the DOM. Reading the code did not catch it. Rendering it did.

Verification therefore has two layers. `npm test` (Vitest, `vitest.config.js`) covers the **pure logic** in the suite under `tests/` — the policy and status mirrors, the permission table, the URL rules, the display helpers — the half that needs neither a browser nor a backend and that a probe can only reach sideways or not at all. Nothing in it renders; a component, a router or a `fetch` in a test would be a second implementation of what the probes already exercise for real.

Everything else stays with the probes, which bundle the **real** `src/` modules with esbuild and print PASS/FAIL per check rather than stopping at the first failure. Every esbuild line in `package.json` carries `--jsx=automatic`, because the sources include `.tsx`: esbuild's own default emits `React.createElement`, which in a Node bundle is a bare reference to a `React` nothing there defines. The automatic runtime instead emits an import esbuild can resolve and bundle. `probe:paths` is the one exception to the bundling: it reads files from disk and is plain Node.

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

An enforced reCAPTCHA is skipped for the same reason. A v3 token is minted by Google's JavaScript in a page, so the script cannot produce one: with `RECAPTCHA_SECRET_KEY` set in the backend `.env`, both `POST /auth/registrations` and `POST /auth/registrations/confirm` answer `captcha_invalid` before doing anything measurable. That is the server working correctly, so it is a SKIP with the hint to leave the key empty, not a FAIL. Without this the probe is permanently red on any machine that has the key set, which is exactly the machine where someone would stop trusting it.

### `npm run probe:session` — sign in, refresh, sign out

`scripts/probe-session.mjs` drives the real session flow with the seeded administrator through the app's own `submitAuth`, `signOut`, `currentSession` and `getCsrfToken`: sign-in and `GET /auth/me`, the sign-out the placeholder page performs (with and without an explicit token — and with no session at all, which the endpoint refuses with `auth_required`, since logout is for signed-in visitors only), refresh rotation with reuse detection — a replayed refresh token kills the whole family, current token included — the identical `invalid_credentials` refusal for a wrong password and an unknown account, and the translation of the session codes.

It needs credentials, because the seeded password is random and printed once:

```bash
set FLUXY_ADMIN_PASSWORD=<the "Password: ..." line from the backend log>
# optional: FLUXY_ADMIN_USER (default admin), FLUXY_TEST_LOGIN_LIMIT=1 (spends the whole
# 15-minute login window, so it runs last), FLUXY_PROBE_DEBUG=1
npm run probe:session
```

It also covers the guest pages' landing rule: `homeForRole` must name the same path the sign-in was answered with in `redirect`, and must fall back to a non-guest route for a role it does not know (see the guest-guard note in [gotchas.md](gotchas.md)).

Two checks exist for the sign-out bug this probe was written after: a token minted before sign-in is refused once signed in (the antiforgery claims binding — the server log names the reason), and sign-in and sign-out both drop the readable `XSRF-TOKEN` copy so nothing can read that refused token back. The third is the fix: `signOut()` mints at click time, under the session it is about to end. `FLUXY_PROBE_DEBUG=1` prints the cookies each request sent and each response set, which is how the `csrf_invalid` was traced to a specific response instead of inferred — the Node jar has no SameSite semantics, so, as with `probe`, a browser is still the only thing that can check those.

### `npm run probe:groups` — the user-group API and its permission gate

`scripts/probe-groups.mjs` exercises `GET/POST/PATCH/DELETE /admin/user-groups` and the accounts list's `groupId` filter through the app's own `getUserGroups` / `getUserGroup` / `createUserGroup` / `updateUserGroup` / `deleteUserGroup` / `getUsers`. It exists because groups are the first thing in this application behind **two independent gates at once** — a role and a permission — and a single-account probe cannot see a permission gate at all: everything it tries is allowed.

So it needs **two** accounts, and the difference between them is the point:

| Account | Group | Grants | Must |
| --- | --- | --- | --- |
| `historyadmin` | `Admins` (base) | all twelve | succeed at everything |
| `admintest` | `Limited admins` | `viewClients`, `editClients` only | read client rows (not administrator rows), be refused every group endpoint with `permission_denied` |

The second account is the check the whole probe is built around. Both take the same password, set directly on the two rows (`bcrypthash` via `dotnet fsi`, cost 11 — the same library the application verifies with). The login window is shared by role and address, so the two sign-ins spend two of the same five permits; a full window is reported as a **SKIP** with the `redis-cli DEL` command, not a failure.

```bash
set FLUXY_ADMIN_PASSWORD=<the password of both accounts>
# optional: FLUXY_GROUP_ADMIN_USER (default historyadmin), FLUXY_GROUP_LIMITED_USER (default admintest)
npm run probe:groups
```

What it asserts, all against a running backend:

- **The base groups.** All three present and only those marked `isBase`; the Admin one grants all twelve, the Client one grants **zero** — drawn as `0`, not as a gap.
- **The asymmetry in the permission catalog.** A permission name that does not exist (`viewEverything`) is **refused** with `validation_failed`; a *valid* one the chosen role does not own (`viewClients` on a Client group) is **dropped**, and the probe re-reads the row to prove it stored nothing.
- **A base group may be renamed and nothing else.** Role change and permission stripping each answer `409 user_group_immutable`; deletion of one answers the same.
- **`409 user_group_in_use`** on a group that still has a member, and `404 user_group_not_found` on an identifier nobody answers to.
- **`400 csrf_invalid`** on a write with no antiforgery token — checked before the rules above, so it must not come back as a 409 wearing a different hat.
- **`GET /admin/users?groupId=`** returns only that group's accounts, with `groupId` matching on every row.
- **The limited account**: signs in, still reads the client rows while an administrator's row is refused, and is refused the group list, the group detail, create, patch and delete - each `403 permission_denied`.
- **Every code it asserted by name is translated in both locale bundles**, by importing `en.ts` and `ru.ts` and looking each one up — a code that reaches the untranslated fallback is an English sentence on a Russian page.

**It cleans up both groups it creates** (including the wrong-role fixture, whose identifier is kept for that purpose). That is a correctness rule and not tidiness: a leftover `Probe client` would make the *next* run's "a second group with the same name is refused" check pass for the wrong reason, having caused its own 409.

### `npm run probe:i18n` — every literal `t()` key, in both bundles

`scripts/i18n-audit.mjs` walks every `.ts`/`.tsx` under `src/`, collects each literal
`t('a.b.c')` key, and resolves it against `en.ts` **and** `ru.ts`.

It exists because the `Translation` type cannot see the bug it was written for. `ru.ts`
declares itself `: Translation` where `Translation = typeof en`, so a key present in one file
and missing from the other is `TS2353` — but a key missing from **both** is absent from the
type itself, and a type cannot report what it does not contain. `t()` keys are deliberately
untyped on the call side (see [gotchas.md](gotchas.md)), so `tsc` has nothing to check there either; `lint`
does not, the build does not, and `probe:render` only walks the keys the shell renders.
`videoStreamsForm.namePlaceholder` shipped exactly that way and the form drew the raw key
into its input — in both languages, so it was invisible on the machine it was written on.

A key missing from both bundles is therefore invisible to **every** checker in this repo
except this one, which is the reason it is a probe of its own rather than a note.

No backend, no render, no credentials. It imports the two bundles directly (esbuild compiles
the `.ts`) and reads `src/` from disk, so it is the cheapest of the probes.

Two limits, both deliberate:

- **Only literal keys are collected.** `t(\`videoStreams.statuses.${value}\`)` is a real call
  whose key is built at runtime; the audit skips it rather than guessing an enum's members.
  Those keys are covered by `probe:render`, which renders the pages that build them.
- **The locale files themselves are skipped as sources.** They call no `t()`, and reading them
  would only re-report their own keys.

`probe:render` also asserts the *shape* of the failure over one render — the stream add form
must draw translated text rather than `videoStreamsForm.…` — so a key dropped from both
bundles still fails the probe most likely to be run.

### `npm run probe:paths` — API prefixes vs the dev-server proxy

`scripts/api-paths-audit.mjs` reads every feature's `api.ts`, plus `src/utils/csrf.ts` and `src/utils/http.ts`,
takes the first segment of every path a request is made with (`/auth`, `/admin`), and
compares that set with `backendPaths` in `vite.config.js` — in **both** directions: a prefix
a request uses but the proxy does not know, and a prefix the proxy knows but nothing
requests.

It exists because the failure it catches is silent twice over. An endpoint outside every
proxied prefix is answered by Vite's HTML fallback with a **200**, so `res.ok` is true and
the JSON parse fails; and the one time this class of mismatch actually happened, it surfaced
much later as `csrf_invalid`, because `getCsrfToken` treats any failure as "no token".
Nothing in either symptom names the prefix — see the `apiUrl` note in [gotchas.md](gotchas.md).

No backend, no render, no credentials, and no esbuild step: it reads files from disk and
fails loudly when it finds no sources or no `backendPaths`, rather than passing over an
empty set (the same guard `probe:i18n` keeps).

### `npm run probe:render` — no backend needed

`scripts/probe-render.jsx` renders each page with `renderToStaticMarkup` and asserts on the HTML: that the footer cross-link is present where one is meant to be, absent where it is not, that `ConfirmCodeForm` is not dead code, and that the guest guard still shows the form while its session check is in flight. Needs neither a backend nor a mail server, so run it first. Every page render is wrapped in `withQuery` — a **fresh** `QueryClientProvider` per render (`createQueryClient` from `src/utils/queryClient.ts`) — because the pages now read through `useQuery` and would render nothing at all without a client, and a shared one would let one render's cache paint the loaded state the next render is asserted not to show. It also calls two functions directly — `countryName` and `describeUserAgent` — and renders `RowActions` directly, because every render in it lands before the first answer arrives: no row is ever drawn, so neither helper nor the row-action cluster would otherwise be reachable from a probe. The visit history section is where both live, and where the resize handles and the sortable header's own keyboard access are asserted; the accounts table is where the address-borne table state is asserted — a render opened at `?search=…&status=…&sortBy=…` must draw the box, the filter and the header arrow it was opened with, since every one of those falls back to its default silently if the read is dropped. The streams section renders the import form as well: the playlist must be the first label, no name field or source-list editor may appear, and the rail must offer `videoStreamsAdd`, `videoStreamsImport` and `videoStreamsManage` in that order while the two content sections that have no importer stay without an import item. **The audio section is asserted the same way and against the same facts** — its own table, form and import, its own seven columns and their widths, and above all the *differences* from the video ones (no EPG column, no delay, no archive, no programme guide, and only the two audio kinds), because the only way to keep two resources from quietly becoming one is to assert what separates them.

Two things it had to get right, both of which produced a false failure before being fixed:

- **React escapes `'` as `&#x27;`** even in text content, so "Don't" reaches the DOM escaped. Comparing rendered HTML against the source string fails on a correct page; the script decodes entities before matching.
- **The script is `.jsx`, not `.mjs`,** because `eslint.config.js` enables `jsx` for `**/*.{js,jsx}` only and `npm run lint` would otherwise reject its own JSX.

And one that produced a false **hang**: `main()` ends with an explicit `process.exit(failed === 0 ? 0 : 1)`, and that is load-bearing rather than tidy. Every query the pages create keeps its garbage-collection timer alive — TanStack Query's default `gcTime` is five minutes — and `renderToStaticMarkup` never unmounts anything, so nothing ever cancels those timers. Without the exit the probe prints its full verdict and then sits through the whole five minutes before the process dies, which reads as a hang (and did: the probe "hung" for a day before this was traced with `process.getActiveResourcesInfo()` to a lone `Timeout`).

All five guest pages have a footer, and what each one carries is a fact about the app rather than an oversight. `ClientLoginPage` and `RegisterPage` carry the cross-link between registration and sign-in; `ResellerLoginPage` and `AdminLoginPage` were extended to offer the password-reset link (they had no footer at all before that, which is what an earlier version of this note recorded); `ForgotPasswordPage` carries "back to sign in" in all three of its states. `probe-render.jsx` asserts `hasFooter: true` for all five, so a `footer` prop that silently renders nothing fails there — that is the check, and asserting a footer *absent* on any of them would now be the wrong expectation.
