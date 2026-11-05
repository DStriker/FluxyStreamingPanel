> Part of the FluxyBackend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).

## Auth is wired: JWT in cookies, rotation on disk and Redis

The old inline `BasicAuthHandler` (hardcoded `admin`/`admin`) is **deleted**. Replacement:

- `Program.cs` calls `builder.Services.AddFluxyAuthentication(builder.Configuration)` (`Fluxy.API/Extensions/AuthenticationExtensions.cs`); `app.UseAuthentication()` is live — before `UseAuthorization()` and after the middleware that can change the request's identity.
- **One `AddAuthentication(JwtBearerDefaults.AuthenticationScheme)` + `AddJwtBearer`.** Signing key (`Auth:SigningKey`, from `JWT_SIGNING_KEY` in `.env`) is read and materialized **once at startup**, so a missing or malformed key fails the host instead of answering every request as unauthorized while looking healthy. Issuer, audience and lifetime validated; `ClockSkew` 30 s; `MapInboundClaims = false`; `NameClaimType`/`RoleClaimType` point at `TokenClaimTypes.*`.
- **The token travels two ways.** Browser: `fluxy.at` HttpOnly cookie (`SameSite=Lax`, written by `AuthCookies`). Non-browser: `Authorization: Bearer`. Header read first, cookie only when the header is absent — a caller sending a header is never overridden by a stray cookie.
- **Access 5 minutes, refresh 7 days** (`Auth:AccessLifetime` / `Auth:RefreshLifetime`; defaults in `AuthenticationOptions`), both validated at startup with `ValidateOnStart()` — `AccessLifetime > 0` and `RefreshLifetime >= AccessLifetime`, each with a message saying what would go wrong otherwise.
- **`OnTokenValidated` refuses a revoked session**: one keyed read per request against `ITokenRevocationStore` (`RedisTokenRevocationStore` in `Fluxy.DataAccess/Infrastructure/Redis/`), key `fluxy:revoked:sid:<sessionId>`, TTL = `Auth:AccessLifetime` plus the 30 s clock skew. One marker covers every access token the session ever held, including tokens minted by earlier rotations, and empties itself when the last possible token would have expired. Every path that ends a session writes it, not just logout. Redis unreachable → the check logs and passes (fail-open) — the documented direction; reason in the store's remarks.
- **Authorization is stricter than the token.** Three policies — `AdminOnly`, `ResellerOnly`, `ClientOnly` — each demand **exactly** one role (`RoleRequirement`, deliberately not `AtLeast`: each sign-in form is a door to one area, and an operator arriving through the reseller door is a different audience at the wrong address). `RoleRequirementHandler` then re-reads the account row on **every** authorized request through `IAccessChecker`, so a blocked, deleted or demoted account is refused while its token is still inside its own lifetime. The handler is **Scoped** because it depends on the DbContext — registering it a singleton fails DI validation.
- **401/403 answer `{ code, message }`**, not bare framework headers — `ApiAuthorizationResultHandler`. It serializes with `JsonSerializerDefaults.Web` on purpose: controllers go through MVC's formatter (camelCase) and the client reads `data.code`, so a bare `JsonSerializer.Serialize` (PascalCase, `Code`) would degrade every refusal from this handler to the frontend's generic `server_error` and lose the difference between "sign in again" and "you are not allowed here".
- **Endpoints**: `POST /auth/client-login`, `/auth/reseller-login`, `/auth/admin-login` (identical `invalid_credentials` refusal for every failure, bcrypt for every one, early exit with a decoy hash when there is no such account); `POST /auth/refresh` (rotation; a replayed refresh token revokes the whole family; **a request with no refresh cookie is refused without spending one of the 30 permits** — there is nothing to rotate, and the frontend asks `refresh` after any 401 it cannot answer otherwise, so a guest page loading `/auth/me` would otherwise drain the window); `POST /auth/logout` (revokes the refresh family in the DB and records the session in Redis so every access token it ever held is refused at once — fail-open with a warning if Redis is down; **requires a session** — `[Authorize]` refuses an unauthenticated caller with 401 `auth_required` before the antiforgery check, which used to answer a visitor with no session to end the misleading `csrf_invalid`); `GET /auth/me`. Limits: **5 logins / 15 min** keyed by role and client address (no username — that would let someone lock an account out, not just slow themselves down) and **30 refreshes / 15 min**. Registration confirmation issues a session and follows `redirect`, like a sign-in.
- Session state lives in `refresh_tokens` rows and the Redis markers of revoked sessions. No server-side session store, no cookie session, nothing else to clean up.

## Login guard: GeoIP allow lists, session binding, fail-closed refusals

Accounts carry two switches (`users.geo_protection_enabled`, `users.bind_session_to_ip`, both
`boolean NOT NULL DEFAULT FALSE`, mapped on `User`/`UserEntity`) and three allow lists in
`user_login_guard_rules` (`id uuid PK`, `user_id FK → users ON DELETE CASCADE`, `kind smallint`
as `LoginGuardRuleKind`: `IpAddress=1, Country=2, AutonomousSystem=3`, `value` canonical,
`UNIQUE(user_id, kind, value)`). Limits are the contract: **5 addresses,
1 country, 1 provider**. Between lists the relation is AND, inside one list OR, an empty list
does not restrict. Addresses are exact or CIDR, v4 and v6 (`LoginGuardPolicy`); countries are
upper case ISO alpha-2 validated against the runtime's own regions; providers match by
**autonomous system number only**. No stored display name for a rule: the `label` column existed
on the first version of the table and was dropped by `20261005183847_DropLoginGuardRuleLabel` —
a name the account typed would be a second source of truth for a fact only the GeoIP database
knows, and nothing ever matched on it. The organization name that `GET /auth/geo/lookup` reports
is a *lookup* of the caller's own network, not a stored field.

- **Source of truth is local MaxMind files**, not a lookup API: `GeoLite2-City.mmdb` (country)
  + `GeoLite2-ASN.mmdb` (provider), read by `MaxMindGeoIpResolver` (`MaxMind.GeoIP2` 6.1.0 in
  `Fluxy.DataAccess/Infrastructure/GeoIp/` — the reader of a data source belongs to the layer
  that owns data sources, not the business rules that consult it). Files live next to the
  solution, linked into the API output as `Content` with `PreserveNewest` (`Fluxy.API.csproj`),
  each behind an `Exists` condition: present → travel with the binaries into publish; absent →
  a fresh clone or CI still builds. Paths from the gitignored `.env` as `GEOIP_CITY_DB_PATH` /
  `GEOIP_ASN_DB_PATH` → `GeoIp:CityDbPath` / `GeoIp:AsnDbPath`; a relative path resolves against
  the API output folder, not the working directory, so a bare file name works however the host
  started. `.mmdb` files are not in git (tens of megabytes, refreshed monthly); a missing file is
  not a startup error — the resolver answers `Unknown` for every address and logs a warning
  naming the `GeoIp` section.
- **Enforcement is fail-closed and silent.** `AuthenticationService` checks the guard after the
  password, status and role, before `IssueAsync`: a violation returns the same `Refused()` as a
  wrong password, so the client gets `401 invalid_credentials` either way and cannot tell a wrong
  password from a wrong network. `JwtTokenService.RefreshAsync` rechecks on every rotation: a
  violation revokes the whole session and answers `401 session_expired`, like a blocked account.
  Distinct reasons exist only in the server log
  (`LoginGuardService ... not on the account's allow lists`).
- **`bind_session_to_ip` pins a session to its opening address.** The address is already stored on
  every refresh row; a refresh from another address revokes the session (`session_expired`).
  Compared as parsed addresses, not text (`::1` vs `0:0:...:1` must not end a session). Access
  tokens stay stateless, so a move is caught on rotation, within one access lifetime.
- **Development bypasses local networks with a warning, nothing else does.** `GeoIpAddress.IsLocal`
  (loopback, private, link-local; in `Fluxy.Core/GeoIp/Utils/`, because the resolver that needs it
  and the guard that needs it live in different layers) + `IHostEnvironment`: in Development a
  local address skips both enforcement and the anti-lock below — loopback carries no GeoIP data and
  would otherwise refuse its own developer. Outside Development there is no bypass — a local
  address there is a misconfiguration, not the developer. `Microsoft.Extensions.Hosting` 10.0.12 is
  pinned in `Fluxy.Application.csproj` for `IHostEnvironment`, same MSB3277 reason as the other pins.
- **Profile surface**: `GET /auth/profile` renders the guard fields; `PATCH /auth/profile`
  (the `loginGuard` field of `PatchProfileRequest`) changes them behind the current password plus
  the shared change attempt window, confirmed by the existing `POST /auth/profile/confirm` flow
  with the new `PendingChangeKind.ChangeLoginGuard = 5`. The staged guard travels as JSON in the
  new nullable `pending_changes.payload` column (five networks do not fit `target_value`), and a
  guard change touches no session — the refresh path rechecks each one on rotation.
  `GET /auth/geo/lookup` (`[Authorize]`, no guards of its own) reports the caller's own
  `{ ip, countryCode, autonomousSystemNumber, organization }` for the "allow my current network"
  button; every field but the address may be null.
- **Anti-lock is a server rule, not UI advice.** Saving `geoProtectionEnabled=true` resolves the
  caller's own address server-side and refuses with `validation_failed` on `geoProtectionEnabled`
  when the new lists would lock that very network out. Caller-supplied "current" values are never
  trusted for this.
- **A password reset clears the guard.** `PasswordResetService.ConfirmAsync` sets both switches off
  and deletes the rules in the same `SaveChanges` as the password; the controller already ends
  every session there. The mailbox is the recovery path for a self-inflicted lockout.
- **Foreign keys are real now.** `refresh_tokens.user_id`, `pending_changes.user_id` and
  `user_login_guard_rules.user_id` are `ON DELETE CASCADE`, and the migration deletes orphan rows
  before adding them. This reverses the old "bare indexed column" decision: an account can now be
  deleted, and a session or a code that outlives its account is a credential for nobody. Verified:
  deleting an account removes its rules, tokens and pending rows, nothing else.

What was verified by running (no `.mmdb` files on the machine, so country/provider paths were
exercised as `Unknown` and the address list carried the allow decisions):

| Scenario | Result |
| --- | --- |
| `GET /auth/profile` | 200 with `geoProtectionEnabled`, `bindSessionToIp`, `allowedIps`, `allowedCountry`, `allowedAutonomousSystemNumber` (no `allowedOrganizationName` — the field went with the `label` column) |
| `POST /auth/profile/geo` with a stale `allowedOrganizationName` in the body | 200 `profile_updated`, ASN rule stored as `15169`, unknown property ignored, profile still carries no organization name |
| `20261005183847_DropLoginGuardRuleLabel` applied at startup | `user_login_guard_rules` = `id, user_id, kind, value, created_at, updated_at`, guard round trip after the drop unchanged |
| `POST /auth/profile/geo`, 6 addresses / bad CIDR / `XX` / ASN 0 | 400 `validation_failed` with `errors.allowedIps` / `errors.allowedCountry` / `errors.allowedAutonomousSystemNumber` |
| `POST /auth/profile/geo` enabling, no mail configured | 200 `profile_updated`, rules in the table, `created_at`-only write on replace |
| `POST /auth/profile/geo` with `[8.8.8.8]` from loopback (Production) | 400 `validation_failed` on `geoProtectionEnabled`, row untouched (anti-lock) |
| Correct password, blocked network (Production) | 401 `invalid_credentials`, indistinguishable from a wrong password |
| Allowed network, then list narrowed | next `POST /auth/refresh` → 401 `session_expired`, session revoked, retry → 401 |
| Session bound, stored address tampered to another | `POST /auth/refresh` → 401 `session_expired`, session revoked |
| Development + loopback + guard on | 200 with a bypass warning naming the address, no lockout of the developer |
| Missing `.mmdb` files | startup warning naming the `GeoIp` section, lookups `Unknown` |
| The reader in `Fluxy.DataAccess` with the real files | `8.8.8.8` → `US`, ASN `15169`, `Google LLC`; a local address → `Unknown` |
| `DELETE FROM users` | rules, refresh rows and pending rows of that account gone, other accounts untouched |
| `POST /auth/admin-login` with the old `Test1234` after the rotation | 401 (the password printed during verification no longer works) |

The four `POST /auth/profile/geo` rows record the run as it happened, before the May 2026 REST
pass; the endpoint is now `PATCH /auth/profile` with the guard nested under `loginGuard`, the same
body one level deeper, and the rules behind it did not change.

## The user domain

One aggregate, four artifacts, plus the first row.

| Artifact | Where | What it is |
| --- | --- | --- |
| Model | `Fluxy.Core/Users/Models/User.cs` | `sealed record` with init-only properties; `UserRole` and `UserStatus` are in `Fluxy.Core/Users/Enums/` |
| Entity | `Fluxy.DataAccess/Entities/UserEntity.cs` | `sealed class UserEntity : AuditableEntity` with `ToModel()` / `FromModel(User)` |
| Configuration | `Fluxy.DataAccess/Configurations/UserConfiguration.cs` | the `users` table, the limits, the indexes, the CHECK constraint |
| Migration | `Fluxy.DataAccess/Migrations/20260930145246_InitialCreate.cs` | DDL only, generated by `dotnet ef` |

`users`: `id uuid` PK; `username varchar(20)` **unique**; `email varchar(254)` **unique**; `password_hash varchar(255)`; `role smallint`; `status smallint`; `registration_code_hash varchar(255)`; `registration_code_expires_at timestamptz`; `registered_at timestamptz`; `created_at` / `updated_at timestamptz`.

Decisions worth not re-litigating:

- **Both enums stored as `smallint`** via `.HasConversion<short>()`. Numeric values are fixed; do not renumber.
- **`UserRole` is ordered** — `Client=1 < Reseller=2 < Admin=3`; every level includes the rights of the one below, so a permission check is `user.Role >= requiredRole`.
- **`UserStatus` is NOT ordered** — `Unregistered=0, Registered=1, Blocked=2` are states of one user, not levels of access. Comparing with `>=` is a bug; test for the exact member. The XML docs on both enums say so.
- **`status` is stored, not derived from `registered_at`.** A row once carried only `registered_at IS NOT NULL`, which covered two states; adding `Blocked` broke that, so `status` became a real column and `registered_at` is now a pure timestamp — *when* it was registered. Rule: `registered_at` is filled when leaving `Unregistered` and is **not** cleared by a block.
- `ck_users_registered_at_status` = `registered_at IS NULL OR status <> 0` guards the one contradiction that would make a row mean two things at once (a registration timestamp on a still-`Unregistered` account). The opposite is deliberately allowed — blocking an account that never confirmed its email is a legitimate move against a spammer. Verified both directions in the database.
- **Login and email are compared case sensitively** (`admin` and `Admin` are two accounts). Verified in the database.
- **`User` is not a response contract.** It carries `PasswordHash` and `RegistrationCodeHash` because the account state needs them; anything sent to a client is built from it explicitly, without those two.

### The first administrator

`Fluxy.Application/Users/Services/DefaultUserSeeder.cs`, reached through `AddApplicationServices()` in `Program.cs` and run by `await app.Services.SeedDatabaseAsync()` **before** `app.Run()`.

- Calls `Database.MigrateAsync()` first — idempotent, so installing is `docker compose up` plus `dotnet run`; `dotnet ef database update` is not needed.
- If any user with `Role = Admin` exists it does nothing and prints nothing.
- Otherwise inserts `admin` / `admin@localhost`, `Role = Admin`, `Status = Registered`, `RegisteredAt = CreatedAt`, and a **random 20 character password**, and logs it once.
- **There is no `HasData` anywhere, on purpose.** A bootstrap password baked into a migration is a password in git history forever, and it cannot be random because a migration cannot print anything — hence a service.
- **No configuration keys** — the password is always generated and stored nowhere in clear text (not in the repo, not in the database). Save it from the log; `docker compose logs` also contains it, accepted for a dev machine.
- An awaited call rather than an `IHostedService` on purpose: hosted services start in registration order, so Kestrel could serve requests before the schema existed.
- On the very first run EF logs `fail: … SELECT "MigrationId" … FROM "__EFMigrationsHistory"` — that is the probe for the migrations history table, not a failure. The migration is applied on the next line.
