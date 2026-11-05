> Part of the FluxyBackend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).

## The registration endpoints

Three endpoints exist now — the first thing in the API layer; they fixed the empty-Swagger problem the API had before any action existed.

| Endpoint | Auth | Rate limit | Purpose |
| --- | --- | --- | --- |
| `GET /auth/csrf` | none | none | Issue the antiforgery token |
| `POST /auth/registrations` | CSRF + captcha | 1 per IP / 15 min | Create account, mail confirmation code (201, no `Location` — see below) |
| `POST /auth/registrations/confirm` | CSRF + captcha | 10 per IP / 15 min | Confirm account with the code |

**There is no `api` segment in any route, on purpose.** This process serves nothing but the API, so a prefix that distinguishes it from a website tells nobody anything, and it becomes one more thing to change when the API moves behind a path of its own — which a reverse proxy or a gateway adds anyway. The route lives in one place, `[Route("auth")]` on `AuthController`; the frontend builds its paths from the same shape in `src/features/auth/api.ts`. If a shared host ever needs the distinction back, put it in front of the application as a path prefix rather than in the controllers.

### The layering

```
AuthController (API)
  └─ IRegistrationService  (Core) ── RegistrationService  (Application)
  ├─ IRecaptchaValidator   (Core) ── GoogleRecaptchaValidator (Application)
  ├─ IAttemptThrottle      (Core) ── RedisAttemptThrottle  (DataAccess)
  └─ IEmailSender          (Core) ── SmtpEmailSender       (Application)
```

The controller owns transport: which check runs first, what HTTP status each outcome gets, what the error code is. The service owns rules: what is valid, what happens to the row, when the code expires. Neither knows about the other.

### Error codes (snake_case, matching the `captchaAction` precedent)

| Code | HTTP | When |
| --- | --- | --- |
| `registration_submitted` | 201 | Account stored, code mailed (deliberately no `Location` — the row has no URI the client can address yet) |
| `registration_not_configured` | 503 | No SMTP settings on this server |
| `validation_failed` | 400 | Field-level rejection (body or service) |
| `user_already_exists` | 409 | Username or email is taken |
| `registration_confirmed` | 200 | Code was valid and unexpired |
| `invalid_code` | 400 | Code wrong, or no pending code for that address |
| `code_expired` | 400 | Code was correct but the 15-minute window closed |
| `captcha_invalid` | 400 | reCAPTCHA refused the token |
| `registration_rate_limited` | 429 | Registration window for this IP is full |
| `confirmation_rate_limited` | 429 | Confirmation attempts for this IP are exhausted |
| `email_delivery_failed` | 502 | Account stored, but mail did not go out |
| `csrf_invalid` | 400 | Missing or stale antiforgery token |

Every response carries `{ code, message }`. The frontend reads `data.message`; `code` is for programmatic branching. **Messages are English fallback text** — the frontend will eventually resolve them from locale files keyed by `code`.

### The decisions worth not re-litigating

- **Email is normalized**: `Trim().ToLowerInvariant()`. Username is only `Trim()`-ed; case is preserved and compared case sensitively (existing decision, see the user domain section in `auth.md`).
- **An existing `Unregistered` account is silently taken over.** The new password replaces the old hash and a fresh code is issued. A `Registered` or `Blocked` account is never touched.
- **A lost race for a unique value becomes the same 409 the loser would have got from a sequential check.** `DuplicateRegistrationException` carries that race so the controller can map it. This is the one place a database exception is caught and treated as an expected outcome.
- **The row is written before the mail is sent.** A code that exists only in memory could not survive a crash, and sending first would mail a code for a row that may never commit. The cost: a transient SMTP failure leaves the user `Unregistered` with a pending code, and a new registration request replaces it.
- **The mail-config check is the first thing `RegisterAsync` does.** A server without SMTP refuses a registration immediately, before any validation or bcrypt, and returns 503 rather than storing a row it cannot follow up on. `register/confirm` does not send mail and is therefore unaffected.
- **`BCrypt.Verify` costs as much as `BCrypt.HashPassword`.** That is why the confirm endpoint must be rate-limited — it is the only place the server does expensive key derivation on an unauthenticated request. The throttle records the attempt *before* the code is compared, so the expensive path is unreachable once the window is full.

### reCAPTCHA v3 cannot be satisfied by a non-browser client

reCAPTCHA v3 tokens are minted by Google's JavaScript **running in a page**. There is no server-side way to produce one: `siteverify` only checks a token a page has already obtained. So with `RECAPTCHA_SECRET_KEY` set, `POST /auth/registrations` and `POST /auth/registrations/confirm` are **browser-only by construction** — a native app, `curl` or a server-to-server caller gets `captcha_invalid` however correct its antiforgery pair is. Verified: a correct cookie and header over the LAN address advances past CSRF and is then refused at the captcha.

A real conflict between two reasonable requirements with no clean code fix. Exempting non-browser callers would remove the protection from exactly the callers worth protecting, since an attacker impersonating a legitimate integration is the case the check exists for. The workable answer is deployment-shaped rather than code-shaped: **leave the key empty on a development machine and set it only on the instance meant to serve browsers.** An empty key bypasses the check, which is also why a production deployment that forgot it logs a warning instead of failing quietly.

### reCAPTCHA

Bypassed when `Recaptcha:SecretKey` is empty — keeps a dev machine usable and is why `getCaptchaToken()` can return `null` in DEV. **A startup warning is logged when the key is missing** so a production deployment that forgot it does not silently have no protection.

The action is fixed per endpoint, not chosen by the caller: `register` for `POST /auth/registrations`, `register_confirm` for `POST /auth/registrations/confirm`. The captcha action is a token property, not a path — its spelling did not move with the rename. The frontend must call `getCaptchaToken('register')` to match.

### Email

`SmtpEmailSender` never throws. A missing server, an unreachable server, or refused credentials all come back as an `EmailSendResult`, never an exception. `IsConfigured` requires `Email:Host` + `Email:From`; `Email:User` / `Email:Password` are an optional **pair** — one without the other is misconfiguration and is logged.

`AddEmail` does **not** fail-fast at startup (unlike `AddDataAccess` and `AddRedis`). Intentional: a machine with no mail server is a valid state for the rest of the application.

### What was verified by running, and what it caught

Every row below was produced against a live instance talking to the compose postgres and the compose redis, with a throwaway SMTP sink standing in for a mail server.

| Scenario | Result |
| --- | --- |
| `GET /auth/csrf` | 200, both cookies written, `XSRF-TOKEN` readable, body token equals the cookie |
| POST without `X-CSRF-Token` | 400 `csrf_invalid` **with** a body |
| POST, no mail configured | 503 `registration_not_configured`, **no row written** |
| POST valid | 201 `registration_submitted`, **no `Location` header** (re-checked after it was removed), row inserted with `status = 0`, mail delivered |
| POST password too simple | 400 `validation_failed`, `errors.password`, **permit not spent** |
| POST taken username | 409 `user_already_exists`, **permit not spent** |
| POST again for the same `Unregistered` account | 200, `UPDATE` of password + code only, `created_at` untouched |
| `register/confirm` wrong code | 400 `invalid_code` |
| `register/confirm` unknown address | 400 `invalid_code` — identical, so addresses cannot be probed |
| `register/confirm` correct but past its window | 400 `code_expired` |
| `register/confirm` correct, address in a different case | 200 `registration_confirmed`, `status = 1`, code cleared |
| 11th confirmation in a window | 429 `confirmation_rate_limited`, and the log shows **no SQL at all after the refusal** |
| 2nd registration in a window | 429 `registration_rate_limited` |
| Preflight from `localhost:5173` | 204 with `Allow-Origin` + `Allow-Credentials: true` |
| Preflight from a foreign origin | 204 with **no** `Access-Control-*` header |
| `Cors:AllowedOrigins` set to `*` | startup aborts with a message naming the section |

The Redis keys were read straight out of the container — `fluxy:throttle:register:::1` and `fluxy:throttle:confirm:::1`, both with a live `PTTL`. They survived three restarts of the application, which the in-memory fallback cannot do, so Redis and not process memory is what was measured. `KEYS` from the `fluxy` user is denied by the ACL exactly as documented in `data-and-infra.md`; read a **known** key with `GET` instead.

Running it caught two bugs that reading the code did not:

- **`SmtpEmailSender` authenticated with an empty user name.** The test was `settings.User is not null`, and the shipped `appsettings.json` carries `"User": ""`, which binds to an empty string and *is* not null. MailKit then called `AuthenticateAsync` against a relay that requires no authentication and threw `NotSupportedException: The SMTP server does not support authentication` — a working configuration turned into a delivery failure. `ResolvedSettings` now carries a `Credentials` record or `null`, so the pair is never half present and never empty.
- **Validation errors answered with `Username`, not `username`.** Model binding reports a failing member under the **C# property name**, not the JSON path, so camelCase normalization in `FieldErrorKeys` never ran for a body property. The factory also groups by normalized key instead of calling `ToDictionary`, because a value that failed both while being read and while being validated lands under two keys that normalize to one name and would throw out of a factory that exists to *describe* a problem.
