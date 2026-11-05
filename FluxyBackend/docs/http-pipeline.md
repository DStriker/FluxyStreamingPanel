> Part of the FluxyBackend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).

## HTTP pipeline — middleware, CSRF, CORS, throttling

### CSRF

Built-in `IAntiforgery`. The framework cookie name is **not** changed. A readable `XSRF-TOKEN` cookie is written manually on every `GET /auth/csrf` response, containing `tokenSet.RequestToken`. The request header is `X-CSRF-Token` (`options.HeaderName`). The frontend always fetches a token immediately before a POST, so the body is what it uses; the cookie exists for a client that wants to read it synchronously, and `AuthCookies` deletes it whenever the session changes (below).

**A token is bound to the identity that was current when it was minted — the rule everything else follows from.** `ValidateRequestAsync` refuses a token issued while the request was anonymous once the same request now carries a signed-in user, and the other way round, with `AntiforgeryValidationException: The provided antiforgery token was meant for a different claims-based user than the current user`. There is no option to switch this off, and it is not a formality: it is what makes one browser's token useless to another identity. It is also exactly what killed the first sign-out button — the page read the `XSRF-TOKEN` cookie written on the sign-in page, presented it after signing in, got `400 csrf_invalid`, and with no `catch` did nothing visible at all. Two changes close it from both sides: the frontend's `getCsrfToken` **always** mints a fresh token instead of reading cache or cookie, and `AuthCookies.Write`/`Clear` **delete the readable copy** on every sign-in, sign-out and confirmation, because a stale copy is worse than none — a missing one gets fetched, a stale one gets read. The remaining window is the access token expiring between the mint and the POST milliseconds later, which is sub-second and self-healing on a retry. All of it is asserted by `npm run probe:session` on the frontend: the stale token is refused with `csrf_invalid`, both sign-in and sign-out drop the copy, and `signOut()` (which mints at click time) succeeds.

**The token is checked by calling `IAntiforgery.ValidateRequestAsync` in each POST, not with `[ValidateAntiForgeryToken]`.** The attribute resolves to the MVC filter `ValidateAntiforgeryTokenAuthorizationFilter`, registered by **`AddControllersWithViews` only** — this project calls `AddControllers` because it is a JSON API with no views, so the attribute resolves to a type that is not in the container and every protected request fails with **500 `No service for type ... ValidateAntiforgeryTokenAuthorizationFilter has been registered`**. Verified against a running instance. Pulling the Razor view engine in to satisfy an attribute is the wrong trade for an API with no views.

The explicit call is also strictly better on the response: the attribute answers a bad token with a 400 and **no body at all**, which the frontend renders as "something went wrong". Asking the service directly produces the same verdict with `{ code: "csrf_invalid", message: ... }`. Every POST — register, confirm, the three sign-ins, refresh, logout — calls `RejectsCsrfAsync()` first, before the captcha, the throttle and any database work. (`logout` only once it has cleared `[Authorize]`: an unauthenticated caller is refused with `auth_required` before reaching the check — the fix for the misleading `csrf_invalid` a visitor with no session used to get.)

Two API changes in this framework version worth remembering, both verified here:

- There is **no `AddApiBehaviorOptions`** extension. Use `services.Configure<ApiBehaviorOptions>(...)`.
- There is **no `HttpContext.GetAntiforgeryTokenSet()`**. Inject `IAntiforgery` and call `GetAndStoreTokens(HttpContext)`, which is synchronous and also writes the framework cookie.

### The middleware order is load-bearing: CORS before HTTPS redirection

`app.UseCors(...)` runs **before** `app.UseHttpsRedirection()` — the reverse of the order the templates suggest, deliberately.

A CORS preflight must be answered by the CORS middleware itself. When redirection ran first, a preflight sent to the http port was answered with a **307** carrying no `Access-Control-Allow-Origin` at all — the request never reached the CORS middleware — and the browser reported **`PreflightMissingAllowOriginHeader`**. The origin policy was irrelevant; the failure was pure middleware ordering, and it looks identical to an origin that was refused. Verified against a running instance: the same preflight returned `307` with zero CORS headers before the change, `204` with the full set after it.

The real `POST` improved as a side effect: the CORS middleware writes its headers before calling the next middleware, so the `307` on a non-preflight request now carries `Access-Control-Allow-Origin` and `Access-Control-Allow-Credentials` and a browser can follow the redirect instead of aborting the chain.

Do not move `UseHttpsRedirection` back above `UseCors` to "tidy up" the pipeline — that is the exact regression this paragraph exists to prevent.

### Serving clients that are not on this machine

Kestrel binds `localhost` in the `http` and `https` profiles, so nothing outside the machine can reach it. The **`lan` profile** binds `http://0.0.0.0:5159`:

```bash
dotnet run --project Fluxy.API --launch-profile lan
```

It is **http only on purpose.** The dev certificate is `CN=localhost`, so an https listener on `0.0.0.0` would present a certificate that fails hostname validation on every remote client. Real TLS needs a certificate for the name clients actually use — a deployment concern rather than a profile.

**CORS does not apply to these clients at all.** It is a browser mechanism: a browser refuses to hand a response to script that did not come from an allowed origin. `curl`, a native app and a server-to-server caller never consult it, which is why exposing the ports is enough for them and why the antiforgery cookies stay `SameSite=Lax` — the weakening a genuinely cross-site browser deployment would need is not required and has not been done.

Verified over the LAN address: `GET /auth/csrf` → 200 with both cookies and no `secure` flag; `POST /auth/registrations` with the cookie and header → `captcha_invalid`, the next gate after CSRF; the same POST without the cookie → `csrf_invalid`.

### The `https` profile turns a same-origin call into a CORS failure

This produced a long, misleading hunt, so the chain is written out. It is **not** a CORS configuration problem, and every link was measured against a running instance rather than inferred.

1. Under the `https` profile the **http** port answers **`307`** to `https://localhost:7221`.
2. A Vite dev server proxying `/auth` relays that redirect verbatim — a proxied response must not redirect, but nothing stops the proxy from passing it on.
3. The browser follows it to a different **scheme**, so a request that was same-origin for the page becomes **cross-origin**.
4. Cross-origin means a preflight. The backend answers it with `Access-Control-Allow-Origin` only for an origin in `Cors:AllowedOrigins`; otherwise the response is a `204` with no such header.
5. The browser reports the missing header as **`PreflightMissingAllowOriginHeader`**.

So a CORS error that appears *together with a redirect* means **the request reached this process without passing through the dev proxy** — usually the wrong port, or the proxy header missing. Check that first and the origin list second, or you will spend the day on the list. Measured while this was still broken: proxied `/auth/csrf` returned `307 → https://localhost:7221/auth/csrf`; the follow-up preflight from the real page origin came back `204` with no ACAO, while the same preflight from a listed origin came back `204` **with** it.

### The fix is a forwarded-proto header, not "use the other profile"

The chain above is now handled rather than merely documented around. The Vite dev proxy sets **`X-Forwarded-Proto: http`**, and `Program.cs` wraps `UseHttpsRedirection` in a `UseWhen` branch that skips it when that header is present **in Development only**:

```csharp
app.UseWhen(
    context => !app.Environment.IsDevelopment()
        || !context.Request.Headers.ContainsKey(CorsExtensions.ProxyProtocolHeader),
    branch => branch.UseHttpsRedirection());
```

**Both launch profiles now work**, which is the point. Visual Studio launches the `https` profile by default, and a rule that only holds under `http` breaks again on the next F5. Scoped to Development deliberately: outside it the header is attacker-controlled, and honouring it would let anyone skip TLS on this process — production terminates TLS at its own reverse proxy, which is not this middleware's job.

The header carries the scheme the **browser** used. The dev server now speaks https (`@vitejs/plugin-basic-ssl` on the frontend side), so it sends `https`; the value is read only for its presence, but a marker stating the wrong scheme is worse than none — it would suppress the redirect while misreporting what happened.

What this does **not** do: it does not run `UseForwardedHeaders`, so `Request.IsHttps` stays false and the antiforgery cookies are still written without `secure`. That is correct and required — the proxy reaches this process over plain http, so a `secure` cookie would be set on a response the browser received over an insecure connection. The browser's own connection to the dev server is https, a different hop, and that is what the antiforgery scheme check actually cares about. The header is used **only** as a marker of "a terminator already decided", never to rewrite the scheme or the client address. Do not switch that on: `RemoteIpAddress` is what the rate limiter keys on, and trusting a forwarded address from an unvalidated source would let one caller mint unlimited windows.

Verified on the `https` profile with the proxy: `/auth/csrf` → `200` JSON, both cookies with `samesite=lax` and **no** `secure`, POST advancing to `captcha_invalid` (past CSRF), and the same POST without the cookie still refused with `csrf_invalid`. Directly hitting the http port without the header still returns the `307`, the intended behaviour — the guard is about proxied requests, not about disabling TLS.

**Remember that the origin list is per-machine and lives in `.env`,** as `CORS_ALLOWED_ORIGINS` — currently `http://localhost:5173,http://localhost:5174`. A browser on any origin absent from it — a LAN address, another machine, a real domain — is refused, and it fails as a 204 preflight **with no `Access-Control-Allow-Origin`**, indistinguishable from the redirect chain above. Both 5173 and 5174 are listed because a second dev server used to push the page onto 5174 silently; `strictPort` in `vite.config.js` stops that happening again, and the extra entry is there so the failure cannot return if it does.

**What `lan` exposes.** Both launch profiles pin `ASPNETCORE_ENVIRONMENT=Development`, so `lan` also serves Swagger UI, `/openapi/v1.json` and the developer exception page to the network. The whole authentication surface is on it as well — the three sign-in endpoints, refresh, logout and `me` — behind the rate limits (5 logins and 30 refreshes per 15 minutes per IP) and a signing key that never leaves `.env`. Acceptable on a trusted LAN; not a public deployment.

### CORS

`CorsExtensions.AddFrontendCors(configuration)` reads `Cors:AllowedOrigins`. `AllowCredentials` is required (the frontend uses cookies). A wildcard `*` is **rejected at startup** — it cannot be combined with credentials and would produce a confusing browser error later. An empty list logs a warning and means no browser can reach the API.

Both settings come from the gitignored `.env` as `CORS_ENABLED` and `CORS_ALLOWED_ORIGINS` (comma separated exact origins, each with scheme and port). `appsettings.json` carries **only** a `_comment` under `Cors:` — any key written there would shadow the file, and an empty string counts as written just as much as a filled one. `CORS_ALLOWED_ORIGINS` becomes indexed configuration keys (`Cors:AllowedOrigins:0`, `:1`, ...) because a list cannot be expressed through the flat `ApplicationSecretKeys` mapping. An origin is per-machine, which is the whole reason it is not committed.

**CORS cannot be removed, only switched off — and switching it off is not the same as allowing everything.** The mechanism lives in the browser, not the server: the server can only decide which origins to answer. The browser's default when no policy matches is to **refuse**. So `CORS_ENABLED=false` turns "sometimes refused" into "always refused"; it cannot make a cross-origin call succeed. It is honest only in the one topology where the policy is never consulted: page and API on a single origin, which is what the vite dev proxy produces, and what a reverse proxy produces in production.

Verified both branches against a running instance: with `CORS_ENABLED` set in `.env`, the origin listed there answers `204` with `Access-Control-Allow-Origin` and an origin that is not listed answers `204` **without** it; with `Cors:Enabled=false` the preflight falls through to `405` and no CORS header is written, plus the startup warning naming the key.

The two halves of the switch have to agree, which is why they are one method each rather than a call in `Program.cs`: `UseCors` for a policy that was never registered **throws at startup**. `AddFrontendCors` skips registering and `UseFrontendCors` skips asking, both reading the same key.

### The custom `InvalidModelStateResponseFactory`

The default `ValidationProblemDetails` has no `message` field, so the frontend would show nothing. `ConfigureApiBehavior` replaces it with `{ code, message, errors }` where `errors` is a field-name → messages map.

Note: there is no `AddApiBehaviorOptions` extension in .NET 10; use `services.Configure<ApiBehaviorOptions>(...)` instead.

## Dev-mode behavior (verified by running)

- `MapOpenApi()` + Swagger UI (doc at `/openapi/v1.json`) + developer exception page + HTTP logging are **all inside `if (app.Environment.IsDevelopment())`**. Outside Development there is no API doc and no detailed error page.
- `app.UseHttpsRedirection()` is unconditional, but the `http` profile configures no HTTPS port, so the middleware logs "Failed to determine the https port for redirect" and passes http requests straight through (200, not a 307).
- **CORS is configured** (`Cors:AllowedOrigins` in `appsettings.json`). The frontend has no Vite dev proxy, so `localhost:5173` must be listed there.

## The IP address used for rate limiting

`HttpContext.Connection.RemoteIpAddress` — the socket address as this server sees it. **No reverse proxy is configured yet.** The moment one is put in front of the app, this becomes the proxy's address and every client shares one rate-limit window. `UseForwardedHeaders` must be added at the very top of the pipeline before anything else reads the address.

On a loopback client the address is `::1`, so the Redis keys are `fluxy:throttle:register:::1` and `fluxy:throttle:confirm:::1` — the colons belong to the IPv6 address and are **not** a key nesting separator.

Limits are configured under `RateLimit` (`RegisterLimit` 1 per `RegisterWindow` 15 min, `ConfirmLimit` 10 per `ConfirmWindow` 15 min) and validated at startup with `ValidateOnStart()`.

## Known limits of the current throttling

Deliberate, but written down so nobody re-derives them:

- **Failed registration attempts are free.** The permit is spent only when a row is actually persisted, so `validation_failed` and `user_already_exists` do not consume it. That was the requirement, and it keeps a user who mistypes their password from being locked out — but the registration endpoint has **no cap on its own failure rate**. Account enumeration is mitigated by anonymizing both conflicts into one `user_already_exists` answer, not by the limit. If enumeration ever matters more than the convenience, count failures too.
- **The 6-digit code is only throttled per IP, with no per-account counter.** Ten guesses per window per address is cheap for one attacker with many addresses.
- **A transient SMTP failure leaves the account `Unregistered`** with a pending code for the remainder of its 15 minutes. A new registration request replaces it, which is the intended recovery, but the window is spent until then.
