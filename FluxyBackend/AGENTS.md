# AGENTS.md — FluxyBackend

ASP.NET Core **10** (`net10.0`) backend for the `Refluxy` monorepo. Git root is **one level up** (`..\`); sibling app is `..\FluxyFrontend` (has its own `AGENTS.md` — read it before touching anything frontend-facing). Monorepo `README.md` is a single line (`# Fluxy`) — no help there.

## Commands

```bash
dotnet build FluxyBackend.slnx                                 # 4 projects, ~4s, 0 warnings
dotnet run --project Fluxy.API --launch-profile http          # http://localhost:5159
dotnet run --project Fluxy.API --launch-profile https         # https://localhost:7221 + http://localhost:5159, opens /swagger
dotnet run --project Fluxy.API --launch-profile lan           # http://0.0.0.0:5159, reachable from other machines
dotnet ef migrations add <Name> --project Fluxy.DataAccess    # scaffolds into Fluxy.DataAccess/Migrations
docker_run.bat                                                # generates .env if missing, then == docker compose up -d (run from FluxyBackend)
powershell -ExecutionPolicy Bypass -File scripts\init-secrets.ps1   # (re)generate secrets in .env; -Force regenerates all
```

- Needs the .NET 10 SDK (installed: 10.0.401). Solution uses the new XML `.slnx` format.
- **No test project, no `.editorconfig`, no lint/format config, no CI, no `Directory.Build.props`/`Directory.Packages.props` (no central package management).** Verification is `dotnet build` plus hitting the endpoint.
- All three launch profiles hardcode `ASPNETCORE_ENVIRONMENT=Development`, so the dev-only middleware is on even outside Visual Studio — and the `lan` profile therefore serves Swagger and the developer exception page to the whole network. Content root is `Fluxy.API/`, not the repo root — run docker from `FluxyBackend/`, not from the project folder.
- Single branch, commit directly. Don't open branches or PRs.
- `git` 2.43.0 is on PATH via the **user** PATH entry `C:\Users\user\Tools\Git\cmd` (a copy of the portable git that ships inside `C:\Program Files\BeefLang\bin\Git`; `msys64` has no git). It works on the repo one level up. New shells pick it up; a shell started before the change needs a restart.

## Intended architecture — the layers are wired, the API layer is still thin

`FluxyBackend.slnx` contains **4** projects, wired as the graph below. `Fluxy.Core` holds `Abstractions/AuditableEntity` and `Models/Users/` (the business model), `Fluxy.DataAccess` holds the `DbContext`, the entities, their table configurations and the migrations, `Fluxy.Application` holds `Services/` (the default user seeder) and its DI extension. `Fluxy.API` holds the `Program.cs` wiring and `Configuration/DotEnvConfigurationExtensions.cs`.

The dependency graph (confirmed by the owner — this is the contract, not a guess):

```
Fluxy.API ──┬──> Fluxy.Application ──┐
            │                        ├──> Fluxy.Core
            └──> Fluxy.DataAccess ───┘
```

- **Fluxy.API** — controllers, request/response DTOs (contracts), configuration.
- **Fluxy.Application** — services and business logic. References Core **and** DataAccess.
- **Fluxy.Core** — immutable business models and interfaces. Depends on nothing.
- **Fluxy.DataAccess** — PostgreSQL infrastructure: `Context/FluxyDbContext.cs` + `Context/FluxyDbContextFactory.cs`, `ServiceCollectionExtensions.cs` (`AddDataAccess`), `Entities/`, `Configurations/` (table configs), `Migrations/`, and the planned `Repositories/` (data-access logic). Empty folders are declared explicitly as `<Folder Include>` in the csproj, because empty folders are invisible to git.

**The graph above is already wired in the csproj files** — add a class to a layer and it is visible to the consumers immediately, no `ProjectReference` editing needed.

## Interfaces belong in Core, implementations in Application

**Every business-logic interface lives in `Fluxy.Core`. Every implementation lives in `Fluxy.Application`.** This is not a new convention being invented — `IUserSeeder` already follows it, and so does everything added since.

The practical rule: if the API layer needs to *call* something, it references the interface from `Fluxy.Core`, and the API csproj never names a concrete implementation type. `Program.cs` wires them together and is the only place where an interface and a class meet.

Why it matters here: it keeps the dependency graph honest. Core depends on nothing, Application depends on Core and DataAccess, API depends on Core and Application. If an interface leaked into DataAccess, the API would be able to reach infrastructure abstractions directly and the layering would stop meaning anything.

**Watch out for the MSB3277 trap.** Any new project that references `Fluxy.DataAccess` and touches EF types must explicitly pin `Microsoft.EntityFrameworkCore.Relational` 10.0.12 (see the PostgreSQL section below for the full explanation). The registration service in `Fluxy.Application` does this already.

## Auth is wired: JWT in cookies, rotation on disk and Redis

The old inline `BasicAuthHandler` (hardcoded `admin`/`admin`) has been **deleted**. What replaced it:

- `Program.cs` calls `builder.Services.AddFluxyAuthentication(builder.Configuration)` (`Fluxy.API/Configuration/AuthenticationExtensions.cs`), and `app.UseAuthentication()` is live — it runs before `UseAuthorization()` and after the middleware that can change the request's identity.
- **One `AddAuthentication(JwtBearerDefaults.AuthenticationScheme)` + `AddJwtBearer`.** The signing key (`Auth:SigningKey`, from `JWT_SIGNING_KEY` in `.env`) is read and materialized **once at startup**, so a missing or malformed key fails the host instead of answering every request as unauthorized while looking healthy. Issuer, audience and lifetime are all validated, `ClockSkew` is 30 s, `MapInboundClaims = false`, `NameClaimType`/`RoleClaimType` point at `TokenClaimTypes.*`.
- **The token travels in two ways.** A browser gets it in the `fluxy.at` HttpOnly cookie (`SameSite=Lax`, written by `AuthCookies`); a non-browser client sends `Authorization: Bearer`. The header is read first and the cookie only when the header is absent, so a caller that sends a header is never overridden by a cookie that happens to be on the request.
- **Access 5 minutes, refresh 7 days** (`Auth:AccessLifetime` / `Auth:RefreshLifetime`; defaults in `AuthenticationOptions`), both validated at startup with `ValidateOnStart()` — `AccessLifetime > 0` and `RefreshLifetime >= AccessLifetime`, each with a message that says what would go wrong otherwise.
- **`OnTokenValidated` refuses a withdrawn `jti`**: one keyed read per request against `ITokenRevocationStore` (`RedisTokenRevocationStore`), TTL = the token's remaining lifetime, so the store empties itself. Redis unreachable → the check logs and passes (fail-open); that is the documented direction, and the reason is in the store's remarks.
- **Authorization is stricter than the token.** Three policies — `AdminOnly`, `ResellerOnly`, `ClientOnly` — each demand **exactly** one role (`RoleRequirement`, deliberately not `AtLeast`: each sign-in form is a door to one area, and an operator arriving through the reseller door is a different audience at the wrong address). `RoleRequirementHandler` then re-reads the account row on **every** authorized request through `IAccessChecker`, so a blocked, deleted or demoted account is refused while its token is still inside its own lifetime. The handler is **Scoped** because it depends on the DbContext — registering it a singleton fails DI validation.
- **401/403 answer with `{ code, message }`**, not the framework's bare headers — `ApiAuthorizationResultHandler`. It serializes with `JsonSerializerDefaults.Web` on purpose: the controllers go through MVC's formatter (camelCase) and the client reads `data.code`, so a bare `JsonSerializer.Serialize` — PascalCase, `Code` — would degrade every refusal from this handler to the frontend's generic `server_error` and lose the difference between "sign in again" and "you are not allowed here".
- **Endpoints**: `POST /auth/client-login`, `/auth/reseller-login`, `/auth/admin-login` (identical `invalid_credentials` refusal for every failure, bcrypt for every one of them, early exit with a decoy hash when there is no such account), `POST /auth/refresh` (rotation; a replayed refresh token revokes the whole family; **a request with no refresh cookie is refused without spending one of the 30 permits** — there is nothing to rotate, and the frontend asks `refresh` after any 401 it cannot answer otherwise, so a guest page loading `/auth/me` would otherwise drain the window), `POST /auth/logout` (revokes the refresh family in the DB and denylists the access `jti` in Redis for its remaining lifetime — fail-open with a warning if Redis is down; **requires a session** — `[Authorize]` refuses an unauthenticated caller with 401 `auth_required` before the antiforgery check, which used to answer a visitor with no session to end the misleading `csrf_invalid`), `GET /auth/me`. Limits: **5 logins / 15 min** keyed by role and client address (no username — that would let someone lock an account out, not just slow themselves down) and **30 refreshes / 15 min**. Registration confirmation issues a session and follows `redirect`, like a sign-in.
- Session state lives in the `refresh_tokens` rows and the Redis denylist. There is no server-side session store, no cookie session, nothing else to clean up.

## Login guard: GeoIP allow lists, session binding, fail-closed refusals

Accounts carry two switches (`users.geo_protection_enabled`, `users.bind_session_to_ip`, both
`boolean NOT NULL DEFAULT FALSE`, mapped on `User`/`UserEntity`) and three allow lists in
`user_login_guard_rules` (`id uuid PK`, `user_id FK → users ON DELETE CASCADE`, `kind smallint`
as `LoginGuardRuleKind`: `IpAddress=1, Country=2, AutonomousSystem=3`, `value` canonical,
`UNIQUE(user_id, kind, value)`). Limits are the contract: **5 addresses,
1 country, 1 provider**. Between lists the relation is AND, inside one list OR, an empty list
does not restrict. Addresses are exact or CIDR, v4 and v6 (`LoginGuardPolicy`); countries are
upper case ISO alpha-2 validated against the runtime's own regions; providers match by
**autonomous system number only**. There is no stored display name for a rule: the `label`
column existed on the first version of the table and was dropped by
`20261005183847_DropLoginGuardRuleLabel` - a name the account typed would be a second source
of truth for a fact only the GeoIP database knows, and nothing ever matched on it. The
organization name that `GET /auth/geo/lookup` reports is a *lookup* of the caller's own
network, not a stored field.

- **Source of truth is local MaxMind files**, not a lookup API: `GeoLite2-City.mmdb` (country)
  + `GeoLite2-ASN.mmdb` (provider), read by `MaxMindGeoIpResolver` (`MaxMind.GeoIP2` 6.1.0 in
  `Fluxy.Application`). The files live next to the solution and are linked into the API output
  as `Content` with `PreserveNewest` (`Fluxy.API.csproj`), so they travel with the binaries
  into publish as well. Paths come from the gitignored `.env` as `GEOIP_CITY_DB_PATH` /
  `GEOIP_ASN_DB_PATH` → `GeoIp:CityDbPath` / `GeoIp:AsnDbPath`; a relative path is resolved
  against the API output folder, not the working directory, so a bare file name works however
  the host was started. The `.mmdb` files are not in git (tens of megabytes, refreshed
  monthly) - a missing file is not a startup error, the resolver answers `Unknown` for every
  address and logs a warning naming the `GeoIp` section.
- **Enforcement is fail-closed and silent.** `AuthenticationService` checks the guard after the
  password, status and role, before `IssueAsync`: a violation returns the same `Refused()` as a
  wrong password, so the client gets `401 invalid_credentials` either way and cannot tell a
  wrong password from a wrong network. `JwtTokenService.RefreshAsync` rechecks on every
  rotation: a violation revokes the whole session and answers `401 session_expired`, like a
  blocked account. The distinct reasons exist only in the server log
  (`LoginGuardService ... not on the account's allow lists`).
- **`bind_session_to_ip` pins a session to its opening address.** The address is already stored
  on every refresh row; a refresh from another address revokes the session (`session_expired`).
  Compared as parsed addresses, not as text (`::1` vs `0:0:...:1` must not end a session).
  Access tokens stay stateless, so a move is caught on rotation, within one access lifetime.
- **Development bypasses local networks with a warning, nothing else does.**
  `LoginGuardPolicy.IsLocalAddress` (loopback, private, link-local) + `IHostEnvironment`:
  in Development a local address skips both the enforcement and the anti-lock below, because
  loopback carries no GeoIP data and would otherwise refuse its own developer. Outside
  Development there is no bypass - a local address there is a misconfiguration, not the
  developer. `Microsoft.Extensions.Hosting` 10.0.12 is pinned in `Fluxy.Application.csproj`
  for `IHostEnvironment`, same MSB3277 reason as the other pins.
- **Profile surface**: `GET /auth/profile` renders the guard fields, `POST /auth/profile/geo`
  (`ChangeLoginGuardRequest`) changes them behind the current password plus the shared change
  attempt window, confirmed by the existing `POST /auth/profile/confirm` flow with the new
  `PendingChangeKind.ChangeLoginGuard = 5`. The staged guard travels as JSON in the new
  nullable `pending_changes.payload` column (five networks do not fit `target_value`), and a
  guard change touches no session - the refresh path rechecks each one on rotation.
  `GET /auth/geo/lookup` (`[Authorize]`, no guards of its own) reports the caller's own
  `{ ip, countryCode, autonomousSystemNumber, organization }` for the "allow my current
  network" button; every field but the address may be null.
- **Anti-lock is a server rule, not UI advice.** Saving `geoProtectionEnabled=true` resolves the
  caller's own address server-side and refuses with `validation_failed` on
  `geoProtectionEnabled` when the new lists would lock that very network out. The caller-supplied
  "current" values are never trusted for this.
- **A password reset clears the guard.** `PasswordResetService.ConfirmAsync` sets both switches
  off and deletes the rules in the same `SaveChanges` as the password; the controller already
  ends every session there. The mailbox is the recovery path for a self-inflicted lockout.
- **Foreign keys are real now.** `refresh_tokens.user_id`, `pending_changes.user_id` and
  `user_login_guard_rules.user_id` are `ON DELETE CASCADE` constraints, and the migration
  deletes orphan rows before adding them. This reverses the old "bare indexed column"
  decision: an account can now be deleted, and a session or a code that outlives its account
  is a credential for nobody. Verified: deleting an account removes its rules, tokens and
  pending rows, nothing else.

What was verified by running (no `.mmdb` files on the machine, so country/provider paths
were exercised as `Unknown` and the address list carried the allow decisions):

| Scenario | Result |
| --- | --- |
| `GET /auth/profile` | 200 with `geoProtectionEnabled`, `bindSessionToIp`, `allowedIps`, `allowedCountry`, `allowedAutonomousSystemNumber` (no `allowedOrganizationName` - the field went with the `label` column) |
| `POST /auth/profile/geo` with a stale `allowedOrganizationName` in the body | 200 `profile_updated`, ASN rule stored as `15169`, unknown property ignored, profile still carries no organization name |
| `20261005183847_DropLoginGuardRuleLabel` applied at startup | `user_login_guard_rules` = `id, user_id, kind, value, created_at, updated_at`, guard round trip after the drop unchanged |
| `POST /auth/profile/geo`, 6 addresses / bad CIDR / `XX` / ASN 0 | 400 `validation_failed` with `errors.allowedIps` / `allowedCountry` / `allowedAutonomousSystemNumber` |
| `POST /auth/profile/geo` enabling, no mail configured | 200 `profile_updated`, rules in the table, `created_at`-only write on replace |
| `POST /auth/profile/geo` with `[8.8.8.8]` from loopback (Production) | 400 `validation_failed` on `geoProtectionEnabled`, row untouched (anti-lock) |
| Correct password, blocked network (Production) | 401 `invalid_credentials`, indistinguishable from a wrong password |
| Allowed network, then list narrowed | next `POST /auth/refresh` → 401 `session_expired`, session revoked, retry → 401 |
| Session bound, stored address tampered to another | `POST /auth/refresh` → 401 `session_expired`, session revoked |
| Development + loopback + guard on | 200 with a bypass warning naming the address, no lockout of the developer |
| Missing `.mmdb` files | startup warning naming the `GeoIp` section, lookups `Unknown` |
| `DELETE FROM users` | rules, refresh rows and pending rows of that account gone, other accounts untouched |
| `POST /auth/admin-login` with the old `Test1234` after the rotation | 401 (the password printed during verification no longer works) |

## The visit history: `GET /auth/sessions`

One page of where an account's sessions came from, reachable from the `account` menu of all three
areas (`/client/sessions`, `/reseller/sessions`, `/admin/sessions`). Read only, and everything
about it follows from that.

- **There is no table of visits.** The rows are `refresh_tokens` (`user_id`, `client_address`,
  `user_agent`, `created_at`, `session_id`) — the same rows every sign-in and every rotation
  already writes. A second table would be a second source for one fact, and the day the two
  disagree the history is worth nothing. **A row is a token, not a session**: a browser that
  refreshed from a different network appears twice, which is the point — "a sign-in I do not
  recognise" and "a rotation I do not recognise" are the same alarm.
- **Geo is resolved at read time, not stored.** `IGeoIpResolver` / `MaxMindGeoIpResolver`, memoized
  per request in a `Dictionary` keyed by address (one page from a home connection is the same
  address twenty times over). A missing `.mmdb` or a private address yields three nulls and a list
  that still reads correctly — an unknown network is a fact to display, not a reason to fail.
  Depth is whatever `cleanup` leaves behind (the ~30 day window documented in `JwtTokenService`).
- **The bounds are refused, not clamped.** `page < 1` or `pageSize` outside `1..SessionHistoryLimits.MaxPageSize`
  (default 20, max 100) answer `400 validation_failed` with `errors.page` / `errors.pageSize`.
  Silently turning 500 rows into 100 would answer with a page whose items do not match the size
  the caller was told it got. A non-numeric value never reaches the action — model binding
  produces the same `validation_failed` through `InvalidModelStateResponseFactory`.
- **No CSRF, no captcha, no attempt window.** Those guard the expensive step (key derivation) and
  the state a POST can alter; this is a GET over an index, behind `[Authorize]`. The account comes
  from the `sub` claim and there is **no `userId` parameter to pass** — adding `?userId=<someone>`
  is ignored and answers the caller's own history. That was verified.
- **A blocked account is handed nothing**: `SessionHistoryService` returns null and the controller
  answers `403 account_not_active`, the same code the profile gives, with its own sentence —
  `ProfileResponses.Describe` words it as a *change* that was refused, and nothing here was being
  changed.
- **`isCurrent`** compares the row's `session_id` with the access token's session claim.
- **Arithmetic is done in `long`.** `page` comes from a query string, so `(page - 1) * pageSize`
  can overflow `int` and reach EF Core as a negative `Skip`. `page=2000000000` was checked
  against a running instance: `200` with an empty page.
- **Migration `20261006083556_AddSessionHistoryIndex`** replaces `IX_refresh_tokens_user_id` with
  `IX_refresh_tokens_user_id_created_at`. It is a replacement rather than an addition: a prefix of
  a b-tree is itself a b-tree, so the leading column answers every lookup the single-column index
  did (`RevokeAllSessionsAsync`, the account's row count) and keeping both would pay for two index
  writes on every token issued to answer what one already answers. The order is
  `created_at DESC, id DESC` — the identifier is the tie-break, because two tokens written in the
  same transaction share a stamp and without it two pages could both show or both skip a row.

Layering follows the usual split: `SessionVisit` / `SessionHistoryPage` / `SessionHistoryLimits`
in `Fluxy.Core/Models/Authentication/`, `ISessionHistoryService` in `Fluxy.Core/Abstractions/`,
`SessionHistoryService` in `Fluxy.Application/Services/Authentication/` (Scoped, `AsNoTracking`),
the contracts in `Fluxy.API/Contracts/SessionHistoryResponse.cs`, and the action on
`ProfileController` — which is already the "read the signed-in account" controller, since it also
holds `GET /auth/profile` and `GET /auth/geo/lookup`.

Verified against a running instance with seeded rows of five ages, three roles and one blocked
account: 200 for Client, Reseller and Admin alike; ordering `created_at DESC`; `total` 6 with
`pageSize=2` giving 2/2/2/0 across four pages (page 4 empty but still carrying the true `total`);
the four refusals above; and no SQL emitted at all after a `validation_failed`.

## PostgreSQL is wired up (EF Core 10, Npgsql provider)

The app talks to the compose postgres, to the already existing `fluxy` database, through EF Core:

- `Fluxy.DataAccess` references `Npgsql.EntityFrameworkCore.PostgreSQL` **10.0.3**, `Microsoft.EntityFrameworkCore` **10.0.12** (pinned explicitly) and `Microsoft.EntityFrameworkCore.Design` **10.0.12** (`PrivateAssets=all`). The explicit EF reference is not redundant: the provider only requires EF `>= 10.0.4`, so without it NuGet resolved 10.0.4 against the 10.0.12 of Design and the build failed with **MSB3277**. Never use the 11.x line of the provider — it targets EF Core 11.
- **The same MSB3277 trap now applies to `Fluxy.Application`**, which uses `AnyAsync`/`MigrateAsync`. Because Design is `PrivateAssets=all`, the 10.0.12 it drags in does not flow, so NuGet resolved Relational 10.0.4 there while `Fluxy.DataAccess.dll` wanted 10.0.12. Fixed by pinning `Microsoft.EntityFrameworkCore.Relational` **10.0.12** explicitly in `Fluxy.Application.csproj` (Relational brings Core 10.0.12 with it). **Any new project that references `Fluxy.DataAccess` and touches EF types must do the same**, otherwise it builds with MSB3277 and then throws a confusing `ServiceProvider` failure at runtime.
- `Fluxy.DataAccess/Context/FluxyDbContext.cs` — plain `DbContext`, one `DbContextOptions<FluxyDbContext>` constructor, `DbSet<UserEntity> Users`, both `SaveChanges` overloads stamping the audit columns, and `OnModelCreating` that applies all `IEntityTypeConfiguration` from its own assembly and then the entity conventions (see "Domain rules" below). The model is no longer empty: it holds the `users` entity.
- `Fluxy.DataAccess/Context/FluxyDbContextFactory.cs` — `IDesignTimeDbContextFactory`, so `dotnet ef` works without starting the application. The connection string only selects the provider and is never used to connect; it reads `ConnectionStrings__Postgres` and otherwise falls back to a placeholder. Set that variable for `dotnet ef database update`, otherwise the command would try the placeholder credentials.
- `Fluxy.DataAccess/ServiceCollectionExtensions.cs` — `services.AddDataAccess(configuration)` reads `ConnectionStrings:Postgres` and calls `UseNpgsql`. It **throws `InvalidOperationException` at startup** when the connection string is missing, with a message naming the three ways to provide it. That is the intended fail-fast, not a bug to silence.
- `Program.cs` calls `builder.Services.AddDataAccess(builder.Configuration)` — the API layer never names Npgsql or EF Core.

### Redis is now connected (StackExchange.Redis 3.3.1)

`Fluxy.DataAccess` and `Fluxy.Application` both reference **StackExchange.Redis 3.3.1**.

- `Fluxy.DataAccess/ServiceCollectionExtensions.cs` also has `AddRedis(configuration)`, which reads `ConnectionStrings:Redis` and registers a **singleton** `IConnectionMultiplexer`. It parses the string at startup (`ConfigurationOptions.Parse`) so a malformed value fails loudly, but it forces `AbortOnConnectFail = false` so a redis that is not running does **not** prevent the host from starting. `IConnectionMultiplexer` may be `null` when no connection string was configured at all — that is valid, not an error.
- `AddDerivedRedisConnectionString` in `DotEnvConfigurationExtensions.cs` mirrors `AddDerivedPostgresConnectionString`: it composes `ConnectionStrings:Redis` from `REDIS_USER` / `REDIS_PASSWORD` / `REDIS_HOST` / `REDIS_PORT` in the `.env` file, defaulting to `localhost:6379`. The `user=` segment is **mandatory** because compose runs redis with `user default off`.
- `RedisAttemptThrottle` (Application) uses a Lua script (`INCR` + `PEXPIRE` on first) so several instances share the window. It **falls back to a `ConcurrentDictionary` in process memory** when redis is unreachable, so registration keeps working even with redis down.
- **The `Redis:Required` config key (`true`/`false`) controls whether a missing connection string is fatal.** It defaults to non-fatal.

**Where the connection string comes from.** There is no `ConnectionStrings` section in either appsettings file. `Configuration/DotEnvConfigurationExtensions.cs` loads the compose `.env` and **composes** `ConnectionStrings:Postgres` from its `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` keys (`POSTGRES_HOST` / `POSTGRES_PORT` are honoured, defaulting to `localhost:5432`, which is right for a host process and wrong for a container — that would need the service name `postgres`). Consequences worth remembering:

- The file is searched **upwards from the content root, 3 levels** (content root is `Fluxy.API/`, the file is next to `compose.yaml`), and a missing file is not an error — the app only logs a warning.
- The source is **inserted at position 0** of `builder.Sources`, not appended. In .NET configuration the **last** provider that knows a key wins, so appending would have made `.env` the *highest* priority source and it would have overridden user-secrets. Verified both ways.
- Therefore anything in `appsettings.json`, user-secrets or environment variables wins, including a hand-written `ConnectionStrings:Postgres`. `scripts\init-secrets.ps1 -SeedUserSecrets` writes exactly that key, so after running it the stored secret overrides the value derived from `.env` — after a rotation either re-run the script or remove the key with `dotnet user-secrets --project Fluxy.API remove "ConnectionStrings:Postgres"`.
- An incomplete set of `POSTGRES_*` keys produces **no** connection string at all, on purpose: connecting with an empty password would be worse than the descriptive startup failure.
- The password is not quoted/escaped. It works because `init-secrets.ps1` generates hex; a secret containing `;` or `=` would need quoting.
- Deriving from the file also means `POSTGRES_*` supplied as real environment variables are **not** picked up — configure `ConnectionStrings:Postgres` directly in that setup.
- `dotnet ef` **10.0.12** is installed as a global tool (`dotnet tool install --global dotnet-ef --version 10.0.12`). Scaffolding works with no connection string at all thanks to the design time factory.

Verifying the wiring without touching the database: `dotnet build` plus resolving the context (`provider: Npgsql.EntityFrameworkCore.PostgreSQL`, `NpgsqlConnection` in state `Closed`) is enough — a `DbContext` opens no connection on construction. The seeder is the one place that talks to the database at startup.

## Domain rules — every entity, every row

Two rules apply to **every** persisted entity. They are enforced by code, not by a convention somebody has to remember:

1. The primary key is a **`uuid`**, generated on the client (`Guid.NewGuid()` in the `AuditableEntity` constructor, `ValueGenerated.Never` in the model convention). Never `gen_random_uuid()` and never an `int`.
2. The row stores **`created_at`** and **`updated_at`** (`timestamp with time zone`, mapped from `DateTimeOffset`). They are stamped by the context, never by the entity.

`Fluxy.Core/Abstractions/AuditableEntity.cs` supplies the three properties — public getters, **private** setters, so EF can write them and nothing else can. It has two constructors: one that generates the key, and one that rehydrates a row (used by `UserEntity.FromModel`).

`FluxyDbContext` does the rest:

- `ApplyAudit()` runs from **both** `SaveChanges` overloads. `Added` gets `CreatedAt = UpdatedAt = DateTimeOffset.UtcNow` (equal, so a fresh row is not born with an `UpdatedAt` a few ticks newer), `Modified` gets a fresh `UpdatedAt`. One `now` per call, so reading `UpdatedAt` twice for one save is safe.
- `ApplyAuditableEntityConvention()` walks every entity type and **throws** `InvalidOperationException` when a non-owned, non-shared type has no single-column `Guid` primary key, or a nullable `CreatedAt`/`UpdatedAt`. The columns themselves still come from the base class — the convention validates the shape.
- Column and table names are **explicit** (`.HasColumnName(...)` in each `IEntityTypeConfiguration`) — snake_case, table plural. `UseSnakeCaseNamingConvention` is **not available** in `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3; it lives in the separate `EFCore.NamingConventions` package, which this project does not reference.
- `HasDefaultValue` is used **nowhere**, on purpose: a value the database invents when the application forgot a column hides the mistake instead of surfacing it.

Verified by running: an insert leaves `created_at = updated_at`; a later update keeps `created_at` and moves `updated_at` forward.

## The user domain

One aggregate, four artifacts, plus the first row.

| Artifact | Where | What it is |
| --- | --- | --- |
| Model | `Fluxy.Core/Models/Users/User.cs` | `sealed record` with init-only properties, plus `UserRole` and `UserStatus` in the same folder |
| Entity | `Fluxy.DataAccess/Entities/UserEntity.cs` | `sealed class UserEntity : AuditableEntity` with `ToModel()` / `FromModel(User)` |
| Configuration | `Fluxy.DataAccess/Configurations/UserConfiguration.cs` | the `users` table, the limits, the indexes, the CHECK constraint |
| Migration | `Fluxy.DataAccess/Migrations/20260930145246_InitialCreate.cs` | DDL only, generated by `dotnet ef` |

`users`: `id uuid` PK; `username varchar(20)` **unique**; `email varchar(254)` **unique**; `password_hash varchar(255)`; `role smallint`; `status smallint`; `registration_code_hash varchar(255)`; `registration_code_expires_at timestamptz`; `registered_at timestamptz`; `created_at` / `updated_at timestamptz`.

Decisions worth not re-litigating:

- **Both enums are stored as `smallint`** via `.HasConversion<short>()`. Their numeric values are fixed; do not renumber them.
- **`UserRole` is ordered** — `Client=1 < Reseller=2 < Admin=3`, every level includes the rights of the one below, so a permission check is `user.Role >= requiredRole`.
- **`UserStatus` is NOT ordered** — `Unregistered=0, Registered=1, Blocked=2` are states of one user, not levels of access. Comparing statuses with `>=` is a bug; test for the exact member. The XML docs on both enums say so.
- **`status` is stored, not derived from `registered_at`.** A row once carried only `registered_at IS NOT NULL`, which covered two states; adding `Blocked` broke that, so `status` became a real column and `registered_at` is now a pure timestamp — *when* it was registered. Rule: `registered_at` is filled when leaving `Unregistered` and is **not** cleared by a block.
- `ck_users_registered_at_status` = `registered_at IS NULL OR status <> 0` guards the one contradiction that would make a row mean two things at once (a registration timestamp on an account that is still `Unregistered`). The opposite is deliberately allowed — blocking an account that never confirmed its email is a legitimate move against a spammer. Verified both directions in the database.
- **Login and email are compared case sensitively** (`admin` and `Admin` are two accounts). Verified in the database.
- **`User` is not a response contract.** It carries `PasswordHash` and `RegistrationCodeHash` because the state of the account needs them; anything sent to a client has to be built from it explicitly, without those two.

### The first administrator

`Fluxy.Application/Services/DefaultUserSeeder.cs`, reached through `AddApplicationServices()` in `Program.cs` and run by `await app.Services.SeedDatabaseAsync()` **before** `app.Run()`.

- It calls `Database.MigrateAsync()` first — idempotent, so installing is `docker compose up` plus `dotnet run`, and `dotnet ef database update` is not needed.
- If any user with `Role = Admin` exists, it does nothing and prints nothing.
- Otherwise it inserts `admin` / `admin@localhost`, `Role = Admin`, `Status = Registered`, `RegisteredAt = CreatedAt`, and a **random 20 character password**, and logs it once.
- **There is no `HasData` anywhere, on purpose.** A bootstrap password baked into a migration file is a password in the git history forever, and it cannot be a random one because a migration cannot print anything. This is the reason the row is created by a service.
- There are **no configuration keys** for it — the password is always generated, and it is stored nowhere in clear text (not in the repo, not in the database). Save it from the log; `docker compose logs` also contains it, which is accepted for a dev machine.
- It is an awaited call rather than an `IHostedService` on purpose: hosted services start in registration order, so Kestrel could serve requests before the schema existed.
- On the very first run EF logs `fail: … SELECT "MigrationId" … FROM "__EFMigrationsHistory"` — that is the probe for the migrations history table, not a failure. The migration is applied on the next line.

## Infrastructure (compose.yaml) and secrets

`docker compose up -d` from `FluxyBackend/` starts postgres (5432), pgadmin (5050) and redis (6379). All three publish their ports on `0.0.0.0` on purpose — they are reachable from the LAN, so every service authenticates with its own strong password.

**No password lives in `compose.yaml`.** Values are interpolated from `FluxyBackend/.env` (gitignored) with `${VAR:?hint}`, so a missing secret fails compose with a readable message instead of starting a service with an empty password. Generate the file once:

```bash
powershell -ExecutionPolicy Bypass -File scripts\init-secrets.ps1
```

- `.env.example` is the committed template; the script copies its key order and comments, filling only the blanks with 32 random bytes as hex. It is idempotent. `-Force` regenerates everything, `-RedisAdmin` adds the optional redis `dev-admin` user, `-SeedUserSecrets` writes the connection strings into `dotnet user-secrets`.
- `.env` is deliberately written **without a BOM** — compose misreads the first key of a BOM-prefixed `.env`. The `.ps1` **must** keep its UTF-8 BOM: PowerShell 5.1 parses `.ps1` as ANSI without one and the Cyrillic comments break the parser.
- `POSTGRES_PASSWORD` and `PGADMIN_DEFAULT_PASSWORD` reach the container as env vars and are therefore visible in `docker inspect` / `docker compose config`. That is the accepted dev trade-off; redis only ever receives the SHA-256 hash. For a shared or non-dev host, switch to docker secrets or an external manager.
- The old shared password `asnsnn34ANBShdj333be` is rotated and still present in the git history on the remote — treat it as public forever.

### postgres (`postgres:18.6`)

- The named volume mounts at **`/var/lib/postgresql`**, and `PGDATA` is left at the image default `/var/lib/postgresql/18/docker`. In postgres 18 both `PGDATA` and the image's `VOLUME` moved off `/var/lib/postgresql/data` (the entrypoint even detects the old mount). **Do not move the volume back** — use `pg_dumpall`/`pg_restore` or `pg_upgrade` to cross major versions.
- `POSTGRES_INITDB_ARGS: "--auth-host=scram-sha-256"` is required. Without it `initdb` writes `trust` for `127.0.0.1/32` and `::1/128`, and the entrypoint's `host all all all scram-sha-256` line lands *after* them — `pg_hba.conf` is first-match-wins, so TCP logins need no password at all. The unix socket deliberately stays `trust` so `docker exec postgres psql -U fluxy` keeps working.
- **Changing `POSTGRES_PASSWORD` does not affect an existing volume** — the entrypoint reads it only when `PGDATA` is empty. Either `docker compose down -v` (acceptable while the database is empty) or `docker exec -it postgres psql -U fluxy -d postgres -c "ALTER USER fluxy WITH PASSWORD '...'"`.
- The schema is created by EF Core migrations in `Fluxy.DataAccess/Migrations`, applied by the seeder at startup. There is **no** bootstrap SQL: compose still bind-mounts the missing `../2. Init Database` folder, and that stays empty.

### redis (`redis:8.6`)

- The ACL is generated by the container `command` into `/tmp/redis.conf` and `/tmp/users.acl` on **every start** from `REDIS_ACL_HASH` (SHA-256 hex of `REDIS_PASSWORD`). `requirepass` and the dead `REDIS_USER`/`REDIS_PASSWORD`/`REDIS_DATABASES` env vars are gone — the official image never read them.
- `user default off` — unauthenticated access is impossible.
- The app user has `~* &* +@all -@admin -@dangerous +client|setname +client|getname +client|setinfo`: full read/write over keys and pub/sub channels, but no `CONFIG`, `MODULE`, `DEBUG`, `SHUTDOWN`, `REPLICAOF`, `FLUSHALL`, `FLUSHDB`, `RESTORE`, `KEYS`, `SORT`, `INFO`, `SAVE` or `ACL`. `&*` is required separately from `~*`, otherwise `PUBLISH` is denied. Lua stays enabled — StackExchange.Redis uses it for distributed locks.
- **No super-admin by default.** `-RedisAdmin` adds `dev-admin` with `~* &* +@all` for manual `redis-cli` work and for RedisInsight (below); local dev machine only. `&*` is there for the same reason as for `fluxy` — `+@all` does not grant pub/sub channels on its own.
- Lockout recovery: `docker compose up -d --force-recreate redis` rebuilds the ACL from `.env`.
- The dead `redis-conf.conf/` folder was deleted.

### pgadmin (`dpage/pgadmin4:9.10`)

- `PGADMIN_CONFIG_SERVER_MODE` is `"True"` **because the port is LAN-exposed**: in desktop mode (`"False"`) pgAdmin serves `/browser/` with no login form, so `PGADMIN_DEFAULT_PASSWORD` protects nothing. Verified: anonymous `GET /browser/` → 302 to `/login`.
- The entrypoint creates the admin user only when `/var/lib/pgadmin/pgadmin4.db` does not exist, so `PGADMIN_DEFAULT_EMAIL`/`PGADMIN_DEFAULT_PASSWORD` are silently ignored on an existing volume. After changing them: `docker compose rm -f pgadmin`, `docker volume rm fluxybackend_pgadmin-data`, `docker compose up -d pgadmin`. The volume name is prefixed by the compose project name, which follows the folder name.
- `PGADMIN_CONFIG_MASTER_PASSWORD_REQUIRED: "False"` — the admin login is the access control.
- **The server is pre-registered from `pgadmin/servers.json`** (`PGADMIN_SERVER_JSON_FILE` + `PGADMIN_REPLACE_SERVERS_ON_STARTUP="True"`), so the list is rebuilt from that file on every start and a misconfigured entry cannot stick. The DB password is deliberately absent from the file — pgAdmin prompts for it, and you paste `POSTGRES_PASSWORD` from `.env`.
- **Host must be `postgres`, not `127.0.0.1`.** pgAdmin and postgres are separate containers, each with its own network namespace, so `127.0.0.1` inside pgAdmin *is pgAdmin* and the error is `Connection refused` while postgres is plainly running. `postgres` is the compose service name and resolves on the shared `postgres` network (172.18.x.x). Same rule anywhere else: from a container use the service name, from the host use `localhost` (the ports are published).

### redisinsight (`redis/redisinsight:3.8`)

- **Insight не входит в образ `redis:8.6`.** Бандл на порту 8001 существовал только у `redis-stack` и ушёл вместе с Redis 8 (`redis/discussions#14020`). Проверяется это напрямую: `docker inspect redis` показывает открытый только `6379/tcp`, а в `/usr/local/bin` контейнера лежат одни `redis-server`/`redis-cli`. Поэтому «открыть 8001 в браузере» не может работать в принципе — панель поднимается отдельным контейнером на **5540**.
- **Порт привязан к `127.0.0.1`, а не к `0.0.0.0`** — в отличие от postgres/redis/pgadmin. У UI Insight нет аутентификации вообще, а подключение сделано под `dev-admin` с полным доступом; публиковать такую панель в LAN нельзя. С хоста: `http://localhost:5540`.
- **Подключение предзаписано через `RI_REDIS_*`** (`RI_REDIS_HOST: redis` — имя сервиса compose на общей сети `redis`, `RI_REDIS_USERNAME: dev-admin`, пароль из `.env`). Работает сразу, вводить в UI ничего не нужно. **Пока эти переменные заданы, любые соединения, добавленные в UI вручную, удаляются при рестарте контейнера.**
- **`RI_REDIS_PASSWORD` — это открытый `REDIS_ADMIN_PASSWORD`, он виден в `docker inspect` / `docker compose config`.** Тот же осознанный компромисс, что у `POSTGRES_PASSWORD`. Если пароль должен жить только в `.env` — уберите `RI_REDIS_*` и добавьте соединение в UI один раз руками (host `redis`, port `6379`, username `dev-admin`).
- **Пользователь `fluxy` для панели не годится**: `-@dangerous` запрещает `INFO`, `SLOWLOG`, `LATENCY`, `CONFIG` — а это ровно те панели, ради которых Insight и открывают. Проверено: под `fluxy` те же команды дают `NOPERM`, под `dev-admin` — работают.
- Проверка: `curl http://localhost:5540/api/health/` → 200; `GET /api/databases` → список с предзаписанным `fluxy-redis` и `version`, полученным из `INFO`. Swagger UI — на `/api/docs`.
- Состояние: том `redisinsight-data:/data` (база соединений и история в sqlite), `restart: unless-stopped`.

### Rotation runbook

1. `powershell -ExecutionPolicy Bypass -File scripts\init-secrets.ps1 -Force`
2. `docker compose down -v` (destroys all data) — or per service: `ALTER USER` for postgres, `--force-recreate` for redis, volume removal for pgadmin as above.
3. Confirm the old password is rejected by postgres (`psql -h <host>`) and by redis (`redis-cli --user fluxy -a <old> PING` → `WRONGPASS`).

### Verifying from PowerShell

Two traps that produce false failures:

- **Assign to a local variable first.** `docker exec -e PGPASSWORD=$env['POSTGRES_PASSWORD'] ...` is silently mangled by PowerShell's argument parsing for native commands — the hashtable index never reaches the process and the login fails with `password authentication failed`. `docker exec -e PGPASSWORD=$pw ...` with `$pw` assigned on a previous line works. The same applies to `redis-cli -a $m['REDIS_PASSWORD']` and to any JSON/URL passed inline (quotes get eaten) — write it to a file and use `curl --data-binary "@file"`.
- `docker exec` inherits the **container's configured** env, not PID 1's, so a variable the entrypoint `export`ed (`PGADMIN_SETUP_PASSWORD`) always reads as empty there. Don't conclude anything from it.

### Still true from before

- **Postgres and redis are both connected from the app** (see the sections above). Redis holds only the rate-limit counters and degrades to process memory when unreachable — there is no cache, no session store, and no distributed lock. pgadmin is manual tooling.
- compose bind-mounts `../2. Init Database` into `/docker-entrypoint-initdb.d`, but **that folder does not exist** in the repo, so Docker silently creates an empty directory in the parent and Postgres gets no bootstrap SQL.
- `UserSecretsId` is set — use `dotnet user-secrets --project Fluxy.API set "..."` rather than committing connection strings to appsettings. The app prefers it over the `.env`-derived value.
- Ports are on `0.0.0.0`, so the LAN can attempt every service. Password strength is the only control; nothing here is hardened for the public internet.

## The registration endpoints

Three endpoints exist now. They are the first thing in the API layer, and they fix the empty-Swagger problem described earlier.

| Endpoint | Auth | Rate limit | Purpose |
| --- | --- | --- | --- |
| `GET /auth/csrf` | none | none | Issue the antiforgery token |
| `POST /auth/register` | CSRF + captcha | 1 per IP / 15 min | Create account, mail confirmation code |
| `POST /auth/register/confirm` | CSRF + captcha | 10 per IP / 15 min | Confirm account with the code |

**There is no `api` segment in any route, on purpose.** This process serves nothing but the API, so a prefix that distinguishes it from a website tells nobody anything, and it becomes one more thing to change when the API is moved behind a path of its own — which a reverse proxy or a gateway adds anyway. The route lives in one place, `[Route("auth")]` on `AuthController`; the frontend builds its paths from the same shape in `src/lib/api.js`. If a shared host ever needs the distinction back, put it in front of the application as a path prefix rather than in the controllers.

### The layering

```
AuthController (API)
  └─ IRegistrationService  (Core) ── RegistrationService  (Application)
  ├─ IRecaptchaValidator   (Core) ── GoogleRecaptchaValidator (Application)
  ├─ IAttemptThrottle      (Core) ── RedisAttemptThrottle  (Application)
  └─ IEmailSender          (Core) ── SmtpEmailSender       (Application)
```

The controller owns transport: which check runs first, what HTTP status each outcome gets, what the error code is. The service owns rules: what is valid, what happens to the row, when the code expires. Neither knows about the other.

### Error codes (snake_case, matching the `captchaAction` precedent)

| Code | HTTP | When |
| --- | --- | --- |
| `registration_submitted` | 200 | Account stored, code mailed |
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

Every response carries `{ code, message }`. The frontend reads `data.message`; the `code` is for programmatic branching. **The messages are English fallback text** — the frontend will eventually resolve them from locale files keyed by `code`.

### The decisions worth not re-litigating

- **Email is normalized**: `Trim().ToLowerInvariant()`. Username is only `Trim()`-ed; case is preserved and compared case sensitively (existing decision, see the user domain section).
- **An existing `Unregistered` account is silently taken over.** The new password replaces the old hash and a fresh code is issued. A `Registered` or `Blocked` account is never touched.
- **A lost race for a unique value becomes the same 409 the loser would have got from a sequential check.** `DuplicateRegistrationException` carries that race so the controller can map it. This is the one place a database exception is caught and treated as an expected outcome.
- **The row is written before the mail is sent.** A code that exists only in memory could not survive a crash, and sending first would mail a code for a row that may never commit. The cost: a transient SMTP failure leaves the user `Unregistered` with a pending code, and a new registration request replaces it.
- **The mail-config check is the first thing `RegisterAsync` does.** A server without SMTP refuses a registration immediately, before any validation or bcrypt, and returns 503 rather than storing a row it cannot follow up on. `register/confirm` does not send mail and is therefore unaffected.
- **`BCrypt.Verify` costs as much as `BCrypt.HashPassword`.** That is why the confirm endpoint must be rate-limited — it is the only place the server will do expensive key derivation on an unauthenticated request. The throttle records the attempt *before* the code is compared, so the expensive path is unreachable once the window is full.

### CSRF

Built-in `IAntiforgery`. The framework cookie name is **not** changed. A readable `XSRF-TOKEN` cookie is written manually on every `GET /auth/csrf` response, containing `tokenSet.RequestToken`. The request header is `X-CSRF-Token` (`options.HeaderName`). The frontend always fetches a token immediately before a POST, so the body is what it uses; the cookie exists for a client that wants to read it synchronously, and `AuthCookies` deletes it whenever the session changes (below).

**A token is bound to the identity that was current when it was minted — that is the rule everything else follows from.** `ValidateRequestAsync` refuses a token issued while the request was anonymous once the same request now carries a signed-in user, and the other way round, with `AntiforgeryValidationException: The provided antiforgery token was meant for a different claims-based user than the current user`. There is no option to switch this off, and it is not a formality: it is what makes one browser's token useless to another identity. It is also exactly what killed the first sign-out button — the page read the `XSRF-TOKEN` cookie written on the sign-in page, presented it after signing in, got `400 csrf_invalid`, and with no `catch` did nothing visible at all. Two changes close it from both sides: the frontend's `getCsrfToken` **always** mints a fresh token instead of reading cache or cookie, and `AuthCookies.Write`/`Clear` **delete the readable copy** on every sign-in, sign-out and confirmation, because a stale copy is worse than none — a missing one gets fetched, a stale one gets read. The remaining window is the access token expiring between the mint and the POST milliseconds later, which is sub-second and self-healing on a retry. All of it is asserted by `npm run probe:session` on the frontend: the stale token is refused with `csrf_invalid`, both sign-in and sign-out drop the copy, and `signOut()` (which mints at click time) succeeds.

**The token is checked by calling `IAntiforgery.ValidateRequestAsync` in each POST, not with `[ValidateAntiForgeryToken]`.** The attribute resolves to the MVC filter `ValidateAntiforgeryTokenAuthorizationFilter`, which is registered by **`AddControllersWithViews` only** — this project calls `AddControllers` because it is a JSON API with no views, so the attribute resolves to a type that is not in the container and every protected request fails with **500 `No service for type ... ValidateAntiforgeryTokenAuthorizationFilter has been registered`**. Verified against a running instance. Pulling the Razor view engine in to satisfy an attribute is the wrong trade for an API that has no views.

The explicit call is also strictly better on the response: the attribute answers a bad token with a 400 and **no body at all**, which the frontend renders as "something went wrong". Asking the service directly produces the same verdict with `{ code: "csrf_invalid", message: ... }`. Every POST — register, confirm, the three sign-ins, refresh, logout — calls `RejectsCsrfAsync()` first, before the captcha, the throttle and any database work. (`logout` only once it has cleared `[Authorize]`: an unauthenticated caller is refused with `auth_required` before reaching the check, which is the fix for the misleading `csrf_invalid` a visitor with no session used to get.)

Two API changes in this framework version worth remembering, both verified here:

- There is **no `AddApiBehaviorOptions`** extension. Use `services.Configure<ApiBehaviorOptions>(...)`.
- There is **no `HttpContext.GetAntiforgeryTokenSet()`**. Inject `IAntiforgery` and call `GetAndStoreTokens(HttpContext)`, which is synchronous and also writes the framework cookie.

### The middleware order is load-bearing: CORS before HTTPS redirection

`app.UseCors(...)` runs **before** `app.UseHttpsRedirection()`. That is the reverse of the order the templates suggest, and it is deliberate.

A CORS preflight must be answered by the CORS middleware itself. When redirection ran first, a preflight sent to the http port was answered with a **307** carrying no `Access-Control-Allow-Origin` at all — the request never reached the CORS middleware — and the browser reported it as **`PreflightMissingAllowOriginHeader`**. The origin policy was irrelevant; the failure was pure middleware ordering, and it looks identical to an origin that was refused. Verified against a running instance: the same preflight returned `307` with zero CORS headers before the change, `204` with the full set after it.

The real `POST` improved as a side effect: the CORS middleware writes its headers before calling the next middleware, so the `307` on a non-preflight request now carries `Access-Control-Allow-Origin` and `Access-Control-Allow-Credentials` and a browser can follow the redirect instead of aborting the chain.

Do not move `UseHttpsRedirection` back above `UseCors` to "tidy up" the pipeline. That is the exact regression this paragraph exists to prevent.

### Serving clients that are not on this machine

Kestrel binds `localhost` in the `http` and `https` profiles, so nothing outside the machine can reach it. The **`lan` profile** binds `http://0.0.0.0:5159`:

```bash
dotnet run --project Fluxy.API --launch-profile lan
```

It is **http only on purpose.** The dev certificate is `CN=localhost`, so an https listener on `0.0.0.0` would present a certificate that fails hostname validation on every remote client. Real TLS needs a certificate for the name clients actually use, which is a deployment concern rather than a profile.

**CORS does not apply to these clients at all.** It is a browser mechanism: a browser refuses to hand a response to script that did not come from an allowed origin. `curl`, a native app and a server-to-server caller never consult it, which is why exposing the ports is enough for them and why the antiforgery cookies stay `SameSite=Lax` — the weakening a genuinely cross-site browser deployment would need is not required and has not been done.

Verified over the LAN address: `GET /auth/csrf` → 200 with both cookies and no `secure` flag; `POST /auth/register` with the cookie and header → `captcha_invalid`, which is the next gate after CSRF; the same POST without the cookie → `csrf_invalid`.

### The `https` profile turns a same-origin call into a CORS failure

This one produced a long, misleading hunt, so the chain is written out. It is **not** a CORS configuration problem, and every link was measured against a running instance rather than inferred.

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

**Both launch profiles now work**, which is the point. Visual Studio launches the `https` profile by default, and a rule that only holds under `http` is a rule that breaks again on the next F5. Scoped to Development deliberately: outside it the header is attacker-controlled, and honouring it would let anyone skip TLS on this process — production terminates TLS at its own reverse proxy, which is not this middleware's job.

The header carries the scheme the **browser** used. The dev server now speaks https (`@vitejs/plugin-basic-ssl` on the frontend side), so it sends `https`; the value is read only for its presence, but a marker stating the wrong scheme is worse than none — it would suppress the redirect while misreporting what happened.

Note what this does **not** do: it does not run `UseForwardedHeaders`, so `Request.IsHttps` stays false and the antiforgery cookies are still written without `secure`. That is correct and required — the proxy reaches this process over plain http, so a `secure` cookie would be set on a response the browser received over an insecure connection. The browser's own connection to the dev server is https, which is a different hop and is what the antiforgery scheme check actually cares about. It also means the header is used **only** as a marker of "a terminator already decided", never to rewrite the scheme or the client address. Do not be tempted to switch that on: `RemoteIpAddress` is what the rate limiter keys on, and trusting a forwarded address from an unvalidated source would let one caller mint unlimited windows.

Verified on the `https` profile with the proxy: `/auth/csrf` → `200` JSON, both cookies with `samesite=lax` and **no** `secure`, POST advancing to `captcha_invalid` (past CSRF), and the same POST without the cookie still refused with `csrf_invalid`. Directly hitting the http port without the header still returns the `307`, which is the intended behaviour — the guard is about proxied requests, not about disabling TLS.

**Remember that the origin list is per-machine and lives in `.env`,** as `CORS_ALLOWED_ORIGINS` — currently `http://localhost:5173,http://localhost:5174`. A browser on any origin absent from it — a LAN address, another machine, a real domain — is refused, and it fails as a 204 preflight **with no `Access-Control-Allow-Origin`**, which is indistinguishable from the redirect chain above. Both 5173 and 5174 are listed because a second dev server used to push the page onto 5174 silently; `strictPort` in `vite.config.js` stops that happening again, and the extra entry is there so the failure cannot return if it does.

**What `lan` exposes.** Both launch profiles pin `ASPNETCORE_ENVIRONMENT=Development`, so `lan` also serves Swagger UI, `/openapi/v1.json` and the developer exception page to the network. The whole authentication surface is on it as well — the three sign-in endpoints, refresh, logout and `me` — behind the rate limits (5 logins and 30 refreshes per 15 minutes per IP) and a signing key that never leaves `.env`. That is acceptable on a trusted LAN and is not a public deployment.

### reCAPTCHA v3 cannot be satisfied by a non-browser client

reCAPTCHA v3 tokens are minted by Google's JavaScript **running in a page**. There is no server-side way to produce one: `siteverify` only checks a token a page has already obtained. So with `RECAPTCHA_SECRET_KEY` set, `POST /auth/register` and `POST /auth/register/confirm` are **browser-only by construction** — a native app, `curl` or a server-to-server caller gets `captcha_invalid` however correct its antiforgery pair is. Verified: a correct cookie and header over the LAN address advances past CSRF and is then refused at the captcha.

This is a real conflict between two reasonable requirements and it has no clean code fix. Exempting non-browser callers would remove the protection from exactly the callers worth protecting, since an attacker impersonating a legitimate integration is the case the check exists for. The workable answer is deployment-shaped rather than code-shaped: **leave the key empty on a development machine and set it only on the instance meant to serve browsers.** An empty key bypasses the check, which is also why a production deployment that forgot it logs a warning instead of failing quietly.

### reCAPTCHA

Bypassed when `Recaptcha:SecretKey` is empty. This keeps a dev machine usable and is the reason `getCaptchaToken()` can return `null` in DEV. **A startup warning is logged when the key is missing** so a production deployment that forgot it does not silently have no protection.

The action is fixed per endpoint, not chosen by the caller: `register` for `POST /auth/register`, `register_confirm` for `POST /auth/register/confirm`. The frontend must call `getCaptchaToken('register')` to match.

### Email

`SmtpEmailSender` never throws. A missing server, an unreachable server, or refused credentials all come back as an `EmailSendResult`, never as an exception. `IsConfigured` requires `Email:Host` + `Email:From`; `Email:User` / `Email:Password` are an optional **pair** — one without the other is treated as misconfiguration and logged.

`AddEmail` does **not** fail-fast at startup (unlike `AddDataAccess` and `AddRedis`). This asymmetry is intentional: a machine with no mail server is a valid state for the rest of the application.

### CORS

`CorsExtensions.AddFrontendCors(configuration)` reads `Cors:AllowedOrigins`. `AllowCredentials` is required (the frontend uses cookies). A wildcard `*` is **rejected at startup** — it cannot be combined with credentials and would produce a confusing browser error later. An empty list logs a warning and means no browser can reach the API.

Both settings come from the gitignored `.env` as `CORS_ENABLED` and `CORS_ALLOWED_ORIGINS` (comma separated exact origins, each with scheme and port). `appsettings.json` carries **only** a `_comment` under `Cors:` — any key written there would shadow the file, and an empty string counts as written just as much as a filled one. `CORS_ALLOWED_ORIGINS` becomes indexed configuration keys (`Cors:AllowedOrigins:0`, `:1`, ...) because a list cannot be expressed through the flat `ApplicationSecretKeys` mapping. An origin is per-machine, which is the whole reason it is not committed.

**CORS cannot be removed, only switched off — and switching it off is not the same as allowing everything.** The mechanism lives in the browser, not in the server: the server can only decide which origins to answer. The browser's default when no policy matches is to **refuse**. So `CORS_ENABLED=false` turns "sometimes refused" into "always refused"; it cannot make a cross-origin call succeed. It is honest only in the one topology where the policy is never consulted: page and API on a single origin, which is what the vite dev proxy produces, and what a reverse proxy produces in production.

Verified both branches against a running instance: with `CORS_ENABLED` set in `.env`, the origin listed there answers `204` with `Access-Control-Allow-Origin` and an origin that is not listed answers `204` **without** it; with `Cors:Enabled=false` the preflight falls through to `405` and no CORS header is written, plus the startup warning naming the key.

The two halves of the switch have to agree, which is why they are one method each rather than a call in `Program.cs`: `UseCors` for a policy that was never registered **throws at startup**. `AddFrontendCors` skips registering and `UseFrontendCors` skips asking, both reading the same key.

### The custom `InvalidModelStateResponseFactory`

The default `ValidationProblemDetails` has no `message` field, so the frontend would show nothing. `ConfigureApiBehavior` replaces it with `{ code, message, errors }` where `errors` is a field-name → messages map.

Note: there is no `AddApiBehaviorOptions` extension in .NET 10; use `services.Configure<ApiBehaviorOptions>(...)` instead.

### What was verified by running, and what it caught

Every row below was produced against a live instance talking to the compose postgres and the compose redis, with a throwaway SMTP sink standing in for a mail server.

| Scenario | Result |
| --- | --- |
| `GET /auth/csrf` | 200, both cookies written, `XSRF-TOKEN` readable, body token equals the cookie |
| POST without `X-CSRF-Token` | 400 `csrf_invalid` **with** a body |
| POST, no mail configured | 503 `registration_not_configured`, **no row written** |
| POST valid | 200 `registration_submitted`, row inserted with `status = 0`, mail delivered |
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

The Redis keys were read straight out of the container — `fluxy:throttle:register:::1` and `fluxy:throttle:confirm:::1`, both with a live `PTTL`. They survived three restarts of the application, which the in-memory fallback cannot do, so Redis and not process memory is what was measured. `KEYS` from the `fluxy` user is denied by the ACL exactly as documented above; read a **known** key with `GET` instead.

Running it caught two bugs that reading the code did not:

- **`SmtpEmailSender` authenticated with an empty user name.** The test was `settings.User is not null`, and the shipped `appsettings.json` carries `"User": ""`, which binds to an empty string and *is* not null. MailKit then called `AuthenticateAsync` against a relay that requires no authentication and threw `NotSupportedException: The SMTP server does not support authentication` — a working configuration turned into a delivery failure. `ResolvedSettings` now carries a `Credentials` record or `null`, so the pair is never half present and never empty.
- **Validation errors answered with `Username`, not `username`.** Model binding reports a failing member under the **C# property name**, not under the JSON path, so the camelCase normalization in `FieldErrorKeys` never ran for a body property. The factory also groups by normalized key instead of calling `ToDictionary`, because a value that failed both while being read and while being validated lands under two keys that normalize to one name and would throw out of a factory that exists to *describe* a problem.

## Dev-mode behavior (verified by running)

- `MapOpenApi()` + Swagger UI (doc at `/openapi/v1.json`) + developer exception page + HTTP logging are **all inside `if (app.Environment.IsDevelopment())`**. Outside Development there is no API doc and no detailed error page.
- `app.UseHttpsRedirection()` is unconditional, but the `http` profile configures no HTTPS port, so the middleware logs "Failed to determine the https port for redirect" and passes http requests straight through (200, not a 307).
- **CORS is configured** (`Cors:AllowedOrigins` in `appsettings.json`). The frontend has no Vite dev proxy, so `localhost:5173` must be listed there.

## The IP address used for rate limiting

`HttpContext.Connection.RemoteIpAddress` — the socket address as this server sees it. **No reverse proxy is configured yet.** The moment one is put in front of the app, this becomes the proxy's address and every client shares one rate-limit window. `UseForwardedHeaders` must be added at the very top of the pipeline before anything else reads the address.

On a loopback client the address is `::1`, so the Redis keys are `fluxy:throttle:register:::1` and `fluxy:throttle:confirm:::1` — the colons belong to the IPv6 address and are **not** a key nesting separator.

Limits are configured under `RateLimit` (`RegisterLimit` 1 per `RegisterWindow` 15 min, `ConfirmLimit` 10 per `ConfirmWindow` 15 min) and validated at startup with `ValidateOnStart()`.

## Known limits of the current throttling

Deliberate, but worth writing down so nobody re-derives them:

- **Failed registration attempts are free.** The permit is spent only when a row is actually persisted, so `validation_failed` and `user_already_exists` do not consume it. That was the requirement, and it keeps a user who mistypes their password from being locked out — but it means the registration endpoint has **no cap on its own failure rate**. Account enumeration is mitigated by anonymizing both conflicts into one `user_already_exists` answer, not by the limit. If enumeration ever matters more than the convenience, count failures too.
- **The 6-digit code is only throttled per IP, with no per-account counter.** Ten guesses per window per address is cheap for one attacker with many addresses.
- **A transient SMTP failure leaves the account `Unregistered`** with a pending code for the remainder of its 15 minutes. A new registration request replaces it, which is the intended recovery, but the window is spent until then.

## Conventions

- Block-scoped namespaces (`namespace X { ... }` with the type indented inside) and `using` directives at the top of the file — not file-scoped namespaces. Current root namespace is `Fluxy.API`; the old `Refluxy.*` rename is complete.
- `ImplicitUsings` and `Nullable` are enabled in all 4 projects.
- **Comments in project files are written in English** — code, config files and this rule itself. Russian stays where it belongs: this document and anything that is prose for humans, not source. XML doc comments (`///`) are preferred over inline comments.
- **Commit messages are written in English and kept short** — one line for the subject, a body of a few lines at most, and only when the change genuinely needs it.
- Identifier naming is standard .NET (`FluxyDbContext`, not `FluxyContext`/`DBContext`); folder names are `Configurations/`, `Entities/`, `Context/`, `Migrations/`, `Repositories/`.
