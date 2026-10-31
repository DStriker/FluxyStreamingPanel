# AGENTS.md — FluxyBackend

ASP.NET Core **10** (`net10.0`) backend for the `Refluxy` monorepo. Git root is **one level up** (`..\`); sibling app is `..\FluxyFrontend` (has its own `AGENTS.md` — read it before touching anything frontend-facing). Monorepo `README.md` is a single line (`# Fluxy`) — no help there.

## Commands

```bash
dotnet build FluxyBackend.slnx                                 # 6 projects, ~5s, 0 warnings
dotnet test --project tests\Fluxy.UnitTests                     # 227 tests, no Docker
dotnet test --project tests\Fluxy.IntegrationTests              # 136 tests, Docker required
dotnet test --solution FluxyBackend.slnx                        # both projects, 363 tests
dotnet run --project Fluxy.API --launch-profile http          # http://localhost:5159
dotnet run --project Fluxy.API --launch-profile https         # https://localhost:7221 + http://localhost:5159, opens /swagger
dotnet run --project Fluxy.API --launch-profile lan           # http://0.0.0.0:5159, reachable from other machines
dotnet ef migrations add <Name> --project Fluxy.DataAccess    # scaffolds into Fluxy.DataAccess/Migrations
docker_run.bat                                                # generates .env if missing, then == docker compose up -d (run from FluxyBackend)
powershell -ExecutionPolicy Bypass -File scripts\init-secrets.ps1   # (re)generate secrets in .env; -Force regenerates all
```

- Needs the .NET 10 SDK (installed: 10.0.401; `global.json` pins the 10.0.4xx feature band with `latestPatch`). Solution uses the new XML `.slnx` format.
- **The unit tests need nothing but the SDK; the integration tests need Docker Desktop because Testcontainers starts `postgres:18.6` and `redis:8.6`.** They use the real composition root, migrations and HTTP pipeline, reset the database and Redis before each test, and run sequentially in one xUnit collection. The test host runs in the `Testing` environment, so the production error envelope is what is asserted.
- **No `.editorconfig`, no lint/format config, no `Directory.Build.props`/`Directory.Packages.props` (no central package management).** Verification is `dotnet build`, `dotnet test`, and hitting the endpoint. `dotnet format` is deliberately not a gate yet.
- `dotnet-ef` is pinned in `.config/dotnet-tools.json`; run `dotnet tool restore` once after cloning.
- All three launch profiles hardcode `ASPNETCORE_ENVIRONMENT=Development`, so the dev-only middleware is on even outside Visual Studio — and the `lan` profile therefore serves Swagger and the developer exception page to the whole network. Content root is `Fluxy.API/`, not the repo root — run docker from `FluxyBackend/`, not from the project folder.
- Single branch, commit directly. Don't open branches or PRs.
- `git` 2.43.0 is on PATH via the **user** PATH entry `C:\Users\user\Tools\Git\cmd` (a copy of the portable git that ships inside `C:\Program Files\BeefLang\bin\Git`; `msys64` has no git). It works on the repo one level up. New shells pick it up; a shell started before the change needs a restart.

## Tests and CI

Two xUnit v3 projects (Microsoft Testing Platform) live under `tests/` and are part of the solution:

- **`Fluxy.UnitTests`** — pure unit tests for the rules in Core and Application (login guard, GeoIP address handling, status composition, permissions, registration, time zones, one-time codes, token signing) plus architecture checks that keep Application free of EF Core/DataAccess/Redis/MaxMind and Core free of project references. No Docker, no database, ~1 s.
- **`Fluxy.IntegrationTests`** — `WebApplicationFactory<Program>` drives the real composition root over HTTP. Testcontainers starts PostgreSQL 18.6 and Redis 8.6 once per run; Respawn truncates the database and Redis is flushed before every test, then the three base groups are recreated. Tests run sequentially in one `[Collection]`. `IEmailSender`, `IRecaptchaValidator`, `IGeoIpResolver`, `TimeProvider` and `IUserSeeder` are replaced with deterministic fakes, and the host runs in `Testing`, so the assertions see the production status-code/error-envelope pipeline. Docker Desktop must be running for this project.
- **Tests never call internal persistence types.** Data is planted through Core repository interfaces (`TestDataSeeder`), except for a few deliberate SQL fixtures (for example an expired registration code).
- **Do not change API behaviour to make a test pass.** If a scenario disagrees with the code, the test documents the actual contract or the code has a bug worth reporting separately.
- **CI**: `.github/workflows/backend.yml` runs on pushes/PRs touching `FluxyBackend/**` (and on demand). It restores, builds Release with `-warnaserror`, runs both projects separately with a minimum test count and their own Cobertura coverage file, verifies `dotnet ef migrations has-pending-model-changes`, and fails if `dotnet list package --vulnerable` reports any package. Test results and coverage are uploaded as the `backend-test-results` artifact.
- Coverage has no threshold yet; the baseline is collected so one can be chosen deliberately later. `dotnet format` is not a gate because there is no `.editorconfig`.

## Intended architecture — the layers are isolated

`FluxyBackend.slnx` contains **4 production projects and 2 test projects**, wired as the graph below. `Fluxy.Core` holds `<Domain>/<Kind>/` — the business model, the contracts and the shared rules, grouped by domain first and by kind second (see "The core layer is organised by domain, then by kind" below), `Fluxy.DataAccess` holds the `DbContext`, the entities, their table configurations, the migrations, the repository implementations and `Infrastructure/` (unit of work and database initializer), `Fluxy.Application` holds `<Domain>/<Kind>/` — the business services, the helpers around them and the configuration they read (see "The application layer is organised by domain, then by kind" below), and its DI extension. `Fluxy.API` holds the `Program.cs` wiring, the top-level `Extensions`/`Middleware`/`Options`/`Security` folders, `Contracts/<Feature>/` and `Controllers/<Area>/`.

The dependency graph (confirmed by the owner — this is the contract, not a guess):

```
Fluxy.API ──┬──> Fluxy.Application ──┐
            │                        ├──> Fluxy.Core
            └──> Fluxy.DataAccess ───┘
```

- **Fluxy.API** — controllers, request/response DTOs (contracts), and the HTTP concerns: DI wiring, middleware, options, security handlers.
- **Fluxy.Application** — services and business logic. References Core **only**; it has no Entity Framework, StackExchange.Redis or MaxMind.GeoIP2 package and cannot see `Fluxy.DataAccess`.
- **Fluxy.Core** — immutable business models, contracts and shared rules. Depends on nothing.
- **Fluxy.DataAccess** — persistence infrastructure: PostgreSQL through `Context/FluxyDbContext.cs` + `Context/FluxyDbContextFactory.cs`, `ServiceCollectionExtensions.cs` (`AddDataAccess`), `Entities/`, `Configurations/` (table configs), `Migrations/` and `Repositories/` (data access behind the Core interfaces); Redis through `Infrastructure/Redis/` (shared connection, attempt throttle, revoked-session store) and the MaxMind `.mmdb` reader through `Infrastructure/GeoIp/`. `Infrastructure/` also holds `UnitOfWork` and `DatabaseInitializer`. Empty folders are declared explicitly as `<Folder Include>` in the csproj, because empty folders are invisible to git.

**The graph above is wired in the csproj files** — add a class to a layer and it is visible to the consumers named by the arrows, not to the layer above them. Adding `Application → DataAccess` back would defeat the isolation this split exists for.

## The core layer is organised by domain, then by kind

`Fluxy.Core` is a **domain-first** tree: the domain is the senior axis, the kind of type is the second one. The namespace mirrors the folder exactly — `Fluxy.Core.<Domain>.<Kind>` — so the path of a file is the name of its namespace.

```
Fluxy.Core/
  Persistence/       Base 1 · Interfaces 2 · Exceptions 1
  Security/          Models 1 · Interfaces 2 · Enums 1
  Email/             Commands 1 · Interfaces 1 · Enums 1
  GeoIp/             ReadModels 1 · Interfaces 1 · Utils 1
  Users/             Models 5 · Rows 4 · ReadModels 8 · Commands 9 · Outcomes 9 · Enums 17 · Utils 8 · Interfaces 7 · Repositories 4
  Authentication/    Models 1 · Rows 5 · ReadModels 7 · Commands 1 · Outcomes 3 · Enums 5 · Utils 3 · Interfaces 7 · Repositories 1
  Content/           Models 1 · Commands 3 · Enums 3 · Utils 1 · Interfaces 1 · Repositories 1
  Streams/           Models 2 · Rows 2 · ReadModels 4 · Commands 13 · Outcomes 11 · Enums 17 · Utils 10 · Interfaces 6 · Repositories 2
  Vod/               Models 1 · Rows 1 · ReadModels 3 · Commands 5 · Outcomes 6 · Enums 9 · Utils 4 · Interfaces 4 · Repositories 1
  Series/            Models 2 · ReadModels 7 · Commands 9 · Outcomes 8 · Enums 12 · Utils 7 · Interfaces 6 · Repositories 2
  Epg/               Models 2 · ReadModels 4 · Commands 3 · Outcomes 1 · Enums 5 · Utils 3 · Interfaces 1 · Repositories 1
```

**The domain is decided by what the type is about, not by who reads it.** `IGeoIpResolver` is GeoIP because it answers a question about an address; `ILoginGuardService` is Users because the allow lists belong to an account and GeoIP is only where the answer comes from; `ITokenRevocationStore` is Authentication because it ends a session, not because it is a "security" thing.

**The kind is decided by the role the type plays**, and every kind answers one question:

| Kind | What it holds | The test |
| --- | --- | --- |
| `Models/` | the business model, the direct analogue of `Entities/` in DataAccess | a persisted business thing or its value object |
| `Rows/` | a projection shaped for one query | it never reaches a response |
| `ReadModels/` | the form the API answers with | it reaches the HTTP response as it is |
| `Commands/` | an intent to change something | `New*`, `*Patch`, `*Write`, `*Operation` |
| `Outcomes/` | how an operation ended | `*Outcome`, `*Result`, `IssuedTokens` |
| `Enums/` | the enumerations | |
| `Interfaces/` | the contracts of the business services | |
| `Repositories/` | the contracts of the data access | |
| `Exceptions/` | the exceptions | |
| `Utils/` | static helpers, limit tables, catalogs, constants | no state, no dependency on a layer |
| `Options/` | the configuration | `AuthenticationOptions` |
| `Base/` | base types to inherit from | `AuditableEntity` |

**`Models/` holds the business model and nothing else.** It has **fourteen** types, one per persisted business thing: `User`, `UserGroup`, `LoginGuardRule`, `PendingChange`, `LoginGuardSettings` (Users), `RefreshToken` (Authentication), `ContentCategory` (Content), `VideoStream`, `AudioStream` (Streams), `SeriesItem`, `Episode` (Series), `EpgSource`, `EpgData` (Epg), `AttemptPolicy` (Security). Everything that is not one of those is a projection, a read model, a command or a result, and lives in the folder that says so. The `Stored` prefix now means exactly one thing — *a projection shaped for a single query* — and it survives only in `Rows/`.

**One type per file.** Twelve files used to hold two or three types each; they were split, so the file name is the type name and nothing else.

**The API layer is organised by area, not by file kind.** The tree and the rule for where a new type goes:

```
Fluxy.API/
  Program.cs                  composition only: every interface meets its implementation here
  Extensions/                 DI wiring (authentication, CORS, rate limits, antiforgery, .env)
  Middleware/                 ApiAuthorizationResultHandler: 401/403 answered as { code, message }
  Options/                    RateLimitOptions; AuthenticationOptions lives in Core, under Authentication/Options/
  Security/                   cookies, role/permission handlers, RequestStandingReader
  Contracts/<Feature>/        request/response DTOs, namespace Fluxy.API.Contracts.<Feature>
  Contracts/Common/           types a second feature names (MessageResponse, bulk answers, ...)
  Controllers/
    ApiControllerBase.cs      the antiforgery pair and NoStore(), shared by every area
    Support/                  stateless helpers plus the scoped AttemptLimiter and CaptchaGuard
    Auth/                     guest, profile and session controllers; AuthControllerBase adds ClientAddress
    Admin/                    accounts, groups, admin sessions
    Content/                  content categories, video streams, audio streams
```

**A controller lives in the area its route starts with** — `auth` in `Auth/`, `admin/users|user-groups|sessions` in `Admin/`, `admin/content-categories|video-streams|audio-streams` in `Content/` — and its namespace mirrors the folder (`Fluxy.API.Controllers.<Area>`). A file is split by a **seam** (a separate resource or a separate request shape), not by length: `ProfileController` gave the session history to `SessionsController`, `UsersController` and `UserGroupsController` gave their bulk endpoints their own controllers, while one-resource controllers stay whole. `ApiControllerBase` carries only what every area needs (antiforgery and `NoStore`); `AuthControllerBase` adds only `ClientAddress` for the `Auth` controllers, and the two guest checks are composed as `AttemptLimiter`/`CaptchaGuard` into the four controllers that spend them (registration, authentication, password reset and profile). `SessionsController`, `Admin` and `Content` therefore never inject a throttle they would not use. Contracts follow the same split and live in `Contracts/<Feature>/`; `Common/` is for a type a second feature names.

**The envelope is API-owned, including the framework's own refusals.** Every action answers with `{ code, message }`, and the two paths that never reach an action are covered by the same shape: an unhandled exception is `500 server_error` through `ApiExceptionHandler`, and a bodyless 404/405/415 is `not_found` / `method_not_allowed` / `unsupported_media_type` through `UseFluxyStatusCodePages()`. Both write `Cache-Control: no-store`; the exception itself is logged and never sent to the client. Development keeps `UseDeveloperExceptionPage` for 500s, and the handler is placed only on the other branch.

- **`AddFluxyExceptionHandling()` calls `AddProblemDetails()` as well.** Parameterless `UseExceptionHandler()` refuses to start without a problem-details service even when an `IExceptionHandler` is registered — found by booting in Production; the compiler says nothing. The handler returns `true` first, so problem details are a container dependency rather than the answer.
- **`SuppressMapClientErrors = true` is what lets 415 reach the envelope.** With `[ApiController]`, MVC otherwise turns the bodyless `UnsupportedMediaTypeResult` into a `ProblemDetails` body before `UseStatusCodePages` can see it. Suppressing that mapping leaves the result bodyless; the status pages then describe 404/405/415, while a controller that has something to say still returns its own `ObjectResult`.
- **The status filter is deliberate.** `UseStatusCodePages` only touches a response with no body at all, and the explicit 404/405/415 mapping leaves every `auth_required`, `permission_denied` and feature-level `*_not_found` body exactly as its owner wrote it. A 404 from a real action is still `user_not_found` / `session_not_found`, not the generic `not_found`.

## The application layer is organised by domain, then by kind

`Fluxy.Application` mirrors the shape of `Fluxy.Core`: the domain is the senior axis, the kind of type is the second one, and the namespace mirrors the folder exactly — `Fluxy.Application.<Domain>.<Kind>`. What the layer holds is what differs: Core holds the model and the contracts, Application holds the services that use them and the helpers around them.

```
Fluxy.Application/
  ServiceCollectionExtensions.cs   the assembly's DI surface: AddApplicationServices
  Authentication/  Services 6 · Utils 2
  Content/         Services 1
  Email/           Services 1 · Options 1
  Security/        Services 1 · Options 1
  Streams/         Services 6 · Utils 2
  Users/           Services 9 · Utils 8 · Options 1
```

**The domain is the same domain it is in Core.** A service that ends a session is Authentication even though the row it writes is a refresh token; a service that replaces an account's allow lists is Users even though the answer to the question is GeoIP. The two layers answer the same question in the same place, so a type's domain never depends on the layer it sits in.

**The kinds are three, and each one is decided by what the type is for.**

| Kind | What it holds | The test |
| --- | --- | --- |
| `Services/` | the business services | it implements a contract of `Fluxy.Core/<Domain>/Interfaces/` |
| `Utils/` | the helpers the services share — a normalizer, a policy, a memo, the frame of a message | it is not a business operation: no contract, no outcome |
| `Options/` | the configuration the services read | it is bound from a configuration section |

**A service is a service because it implements a contract.** `ProfileService`, `RegistrationService`, `PasswordResetService`, `UserAdminService`, `UserGroupService`, `ContentCategoryService`, `VideoStreamService`, `VideoStreamBulkService`, `VideoStreamImportService`, `AuthenticationService`, `JwtTokenService` and the three session readers all implement an interface of `Fluxy.Core`; the API layer names that interface and never the class. `AccessChecker` is the one service without one of its own — it is a helper the authorization handlers call, not an operation a client asks for — and it sits beside them because it is a service in the ordinary sense.

**A file that is not a service does not live in `Services/`.** That is the rule the kind column above encodes: `CodeMessage` (the frame of a message carrying a code), `DevelopmentGuardBypass` (the rule that lets a local address past the login guard in Development), `GeoIpMemo` (the lookups of one request, one per address), `OneTimeCode`, `EmailNormalizer`, `RegistrationPolicy`, `LoginGuardPolicy`, `LoginGuardNormalizer`, `TimeZonePolicy` and `VideoStreamRules` are helpers, and a helper in `Services/` is a service that answers nothing.

**Two domains of Core have no folder here, and that is the point.** `GeoIp` — the reader of the `.mmdb` files is a mechanism, and a mechanism belongs to the layer that owns data sources (`Fluxy.DataAccess/Infrastructure/GeoIp/`); the Application layer knows only the `IGeoIpResolver` contract from Core. `Persistence` — the transaction and the database lifecycle are Core contracts implemented in DataAccess, and the Application layer calls `IUnitOfWork` without ever naming a `DbContext`. `Persistence` would also be the wrong word for what the layer does: a service does not persist, it asks for a change to be committed.

**`ServiceCollectionExtensions.cs` sits at the root of the project**, with the namespace `Fluxy.Application`: it is the assembly's DI surface and belongs to no domain. `Program.cs` calls `AddApplicationServices` and nothing else of this layer.

**The two internal collaborators stayed concrete.** `PendingChangeService` and `UserBulkService` are not in Core: they are called by other services of the same layer and answer to no client, so there is no contract for them to implement and no reason for the API to know they exist. `UserBulkService` is the exception that proves the rule — the API *does* call it, so it has a contract of its own (`IUserBulkService`), and `BulkAsync` is no longer a method of `IUserAdminService`. A bulk request names a set a table assembled on screen and answers with an entry per row of that set, which is a different shape from the one outcome per row of the single-account methods; a client asking for the second must not have to know which single-row method it is built on.

## The rules that are written once

The Application layer had four rules written more than once. Each is now written once, and it lives where the rule belongs rather than where it happened to be used.

| Rule | Was | Now |
| --- | --- | --- |
| the Development bypass of the login guard | three copies of `BypassesGuard` | `Users/Utils/DevelopmentGuardBypass.cs` |
| the frame of a message carrying a code | three copies of `BuildCodeBody` | `Users/Utils/CodeMessage.cs` |
| the memo of the GeoIP lookups of one request | three dictionaries, one per service | `Authentication/Utils/GeoIpMemo.cs` |
| the shape of a refused write | six `try`/`catch` blocks around `SaveChangesAsync` | `Core/Persistence/Utils/UnitOfWorkExtensions.cs` |

**`TrySaveAsync` is where the rule about unique violations now lives.** The overload without a list of indexes hands the violation back and lets the caller ask `IsConstraint` about the index it knows; the overload that takes the list returns only a violation from one of them and **rethrows anything else**. That is the "a violation from an index the caller does not recognise is not a name conflict" rule of the repository section, written once instead of once per service, and it is why a bug in a list writer cannot be announced as "that name is already taken".

**Two seams came out of the deduplication.** `PendingChangeService` holds the machinery of a change that waits for a one-time code — staging the row, hashing the code, checking a code against the row that holds it, removing the row once it is spent — because the profile change and the password reset were each writing their own copy of those three steps. The flows keep what is theirs: the wording of the message, where it is sent, and what happens after the code is accepted. `PendingCodeStatus` (in `Core/Users/Enums/`) is the verdict of a code, because "wrong", "expired" and "valid" are the same three answers wherever a code is checked.

## Interfaces belong in Core, implementations in their layer

**Every interface lives in `Fluxy.Core`. The implementation lives in the layer that owns the mechanism.** Business services implement interfaces under `Fluxy.Core/<Domain>/Interfaces/` and live in `Fluxy.Application/<Domain>/Services/`; persistence interfaces live under `Fluxy.Core/<Domain>/Repositories/` and are implemented in `Fluxy.DataAccess/Repositories/`. The readers of an external data source are the third case: `IGeoIpResolver` lives in `Fluxy.Core/GeoIp/Interfaces/` and its MaxMind implementation in `Fluxy.DataAccess/Infrastructure/GeoIp/`, beside the redis stores. `IUnitOfWork` and `IDatabaseInitializer` live under `Fluxy.Core/Persistence/Interfaces/` and are implemented in `Fluxy.DataAccess/Infrastructure/` — they describe a transaction and the database lifecycle, not a repository, and the owner asked for that separation explicitly.

The practical rule: if the API layer needs to *call* something, it references the interface from `Fluxy.Core`, and `Program.cs` wires it to an implementation. `Fluxy.API` also references `Fluxy.DataAccess` so it can call `AddDataAccess`/`AddRedis`/`AddGeoIp`, but it never names a repository class, a `DbContext` or any other EF type.

Why it matters here: it keeps the dependency graph honest. Core depends on nothing, Application depends on Core only, DataAccess depends on Core, and API depends on Application and DataAccess. If a repository interface leaked into DataAccess, the API could reach infrastructure abstractions directly and the layering would stop meaning anything.

### Repository rules

- **A repository speaks Core models.** Reads return Core models or page models (`AdminUserPage`, `VideoStreamDetail`, …); writes are intent methods (`SetStatusAsync`, `ReplaceSourcesAsync`, `RemoveAsync`). An `IQueryable` never leaves DataAccess.
- **Business rules stay in Application.** Role visibility, status transitions, anti-lock, which permission a write needs, and the meaning of a uniqueness conflict are decided by the service. The repository owns SQL shape, tracking, paging and the storage of rows.
- **One `SaveChangesAsync` per business operation**, through `IUnitOfWork`. Repositories stage changes; the service calls the single save after every rule has passed. A new group and its permission rows, or an account and its guard rows, therefore commit together.
- **`ExecuteUpdate`/`ExecuteDelete` are immediate** and are not rolled back by the unit of work. Each such call says so in remarks; it is used only for token revocation and cleanup. Login-guard replacement loads the old rules through the tracker, so their deletion and the new rows commit with the account in the same `SaveChanges` transaction.
- **Unique violations are matched by the exact constraint name** (`IX_user_groups_name`, `IX_content_categories_content_type_name`, `IX_video_streams_category_id_name`, …) and mapped to their feature's `already_exists` outcome. Any other violation is rethrown — a future bug must not be reported as a plausible name conflict.
- **Paging, search and sort are server side** in the repository because `total` must be counted under the same predicate; silently applying them after paging would make the pager lie.
- **`FluxyDbContext` is `internal`.** The repository classes and `DbContextExtensions` are internal too, so nothing outside DataAccess can name the persistence model. The design-time factory is internal as well; `dotnet ef migrations list --no-connect` still discovers it (verified).

**Watch out for the MSB3277 trap.** Any project that references `Fluxy.DataAccess` builds against EF Core 10.0.12 but only receives Relational 10.0.4 transitively, because the 10.0.12 that `Microsoft.EntityFrameworkCore.Design` brings is `PrivateAssets=all`. Such a project must pin `Microsoft.EntityFrameworkCore.Relational` 10.0.12 explicitly (see the PostgreSQL section below). `Fluxy.API` is that project and carries the pin; `Fluxy.Application` deliberately does not reference DataAccess, so it needs neither the reference nor the pin.

## Auth is wired: JWT in cookies, rotation on disk and Redis

The old inline `BasicAuthHandler` (hardcoded `admin`/`admin`) has been **deleted**. What replaced it:

- `Program.cs` calls `builder.Services.AddFluxyAuthentication(builder.Configuration)` (`Fluxy.API/Extensions/AuthenticationExtensions.cs`), and `app.UseAuthentication()` is live — it runs before `UseAuthorization()` and after the middleware that can change the request's identity.
- **One `AddAuthentication(JwtBearerDefaults.AuthenticationScheme)` + `AddJwtBearer`.** The signing key (`Auth:SigningKey`, from `JWT_SIGNING_KEY` in `.env`) is read and materialized **once at startup**, so a missing or malformed key fails the host instead of answering every request as unauthorized while looking healthy. Issuer, audience and lifetime are all validated, `ClockSkew` is 30 s, `MapInboundClaims = false`, `NameClaimType`/`RoleClaimType` point at `TokenClaimTypes.*`.
- **The token travels in two ways.** A browser gets it in the `fluxy.at` HttpOnly cookie (`SameSite=Lax`, written by `AuthCookies`); a non-browser client sends `Authorization: Bearer`. The header is read first and the cookie only when the header is absent, so a caller that sends a header is never overridden by a cookie that happens to be on the request.
- **Access 5 minutes, refresh 7 days** (`Auth:AccessLifetime` / `Auth:RefreshLifetime`; defaults in `AuthenticationOptions`), both validated at startup with `ValidateOnStart()` — `AccessLifetime > 0` and `RefreshLifetime >= AccessLifetime`, each with a message that says what would go wrong otherwise.
- **`OnTokenValidated` refuses a revoked session**: one keyed read per request against `ITokenRevocationStore` (`RedisTokenRevocationStore` in `Fluxy.DataAccess/Infrastructure/Redis/`), key `fluxy:revoked:sid:<sessionId>`, TTL = `Auth:AccessLifetime` plus the 30 s clock skew. One marker covers every access token the session ever held, including tokens minted by earlier rotations, and it empties itself when the last possible token would have expired. Every path that ends a session writes it, not just logout. Redis unreachable → the check logs and passes (fail-open); that is the documented direction, and the reason is in the store's remarks.
- **Authorization is stricter than the token.** Three policies — `AdminOnly`, `ResellerOnly`, `ClientOnly` — each demand **exactly** one role (`RoleRequirement`, deliberately not `AtLeast`: each sign-in form is a door to one area, and an operator arriving through the reseller door is a different audience at the wrong address). `RoleRequirementHandler` then re-reads the account row on **every** authorized request through `IAccessChecker`, so a blocked, deleted or demoted account is refused while its token is still inside its own lifetime. The handler is **Scoped** because it depends on the DbContext — registering it a singleton fails DI validation.
- **401/403 answer with `{ code, message }`**, not the framework's bare headers — `ApiAuthorizationResultHandler`. It serializes with `JsonSerializerDefaults.Web` on purpose: the controllers go through MVC's formatter (camelCase) and the client reads `data.code`, so a bare `JsonSerializer.Serialize` — PascalCase, `Code` — would degrade every refusal from this handler to the frontend's generic `server_error` and lose the difference between "sign in again" and "you are not allowed here".
- **Endpoints**: `POST /auth/client-login`, `/auth/reseller-login`, `/auth/admin-login` (identical `invalid_credentials` refusal for every failure, bcrypt for every one of them, early exit with a decoy hash when there is no such account), `POST /auth/refresh` (rotation; a replayed refresh token revokes the whole family; **a request with no refresh cookie is refused without spending one of the 30 permits** — there is nothing to rotate, and the frontend asks `refresh` after any 401 it cannot answer otherwise, so a guest page loading `/auth/me` would otherwise drain the window), `POST /auth/logout` (revokes the refresh family in the DB and records the session in Redis so every access token it ever held is refused at once — fail-open with a warning if Redis is down; **requires a session** — `[Authorize]` refuses an unauthenticated caller with 401 `auth_required` before the antiforgery check, which used to answer a visitor with no session to end the misleading `csrf_invalid`), `GET /auth/me`. Limits: **5 logins / 15 min** keyed by role and client address (no username — that would let someone lock an account out, not just slow themselves down) and **30 refreshes / 15 min**. Registration confirmation issues a session and follows `redirect`, like a sign-in.
- Session state lives in the `refresh_tokens` rows and the Redis markers of revoked sessions. There is no server-side session store, no cookie session, nothing else to clean up.

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
  `Fluxy.DataAccess/Infrastructure/GeoIp/` — the reader of a data source belongs to the layer
  that owns data sources, not to the business rules that consult it). The files live next to
  the solution and are linked into the API output as `Content` with `PreserveNewest`
  (`Fluxy.API.csproj`), each behind an `Exists` condition: when present they travel with the
  binaries into publish, and when absent a fresh clone or CI still builds. Paths come from the
  gitignored `.env` as `GEOIP_CITY_DB_PATH` /
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
  `GeoIpAddress.IsLocal` (loopback, private, link-local; in `Fluxy.Core/GeoIp/Utils/`, because
  the resolver that needs it and the guard that needs it live in different layers) +
  `IHostEnvironment`:
  in Development a local address skips both the enforcement and the anti-lock below, because
  loopback carries no GeoIP data and would otherwise refuse its own developer. Outside
  Development there is no bypass - a local address there is a misconfiguration, not the
  developer. `Microsoft.Extensions.Hosting` 10.0.12 is pinned in `Fluxy.Application.csproj`
  for `IHostEnvironment`, same MSB3277 reason as the other pins.
- **Profile surface**: `GET /auth/profile` renders the guard fields, `PATCH /auth/profile`
  (the `loginGuard` field of `PatchProfileRequest`) changes them behind the current password
  plus the shared change
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
| The reader in `Fluxy.DataAccess` with the real files | `8.8.8.8` → `US`, ASN `15169`, `Google LLC`; a local address → `Unknown` |
| `DELETE FROM users` | rules, refresh rows and pending rows of that account gone, other accounts untouched |
| `POST /auth/admin-login` with the old `Test1234` after the rotation | 401 (the password printed during verification no longer works) |

The four `POST /auth/profile/geo` rows record the run as it happened, before the May 2026 REST
pass; the endpoint is now `PATCH /auth/profile` with the guard nested under `loginGuard`, the
same body one level deeper, and the rules behind it did not change.

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
  (default 20, max 1000) answer `400 validation_failed` with `errors.page` / `errors.pageSize`.
  Silently turning 500 rows into 1000 would answer with a page whose items do not match the size
  the caller was told it got. A non-numeric value never reaches the action — model binding
  produces the same `validation_failed` through `InvalidModelStateResponseFactory`.
- **`search`, `sortBy` and `sortOrder` exist, and they are server side for one reason: `total`.**
  A filter or a sort applied in the browser to the rows already on screen would be applied to one
  page while the pager still counted everything — four pages of five rows when the filter admitted
  one, and an order that changed on every page turn. So both are built into the LINQ *before*
  `CountAsync`, and both reset the pager.
  - `search` is a case-insensitive substring of **`client_address` or `user_agent` only**, capped at
    `SessionHistoryLimits.MaxSearchLength` (200) and refused above it with `errors.search`. Country
    and provider are not columns — they are resolved while the page is built — so filtering on them
    would mean resolving GeoIP for every row an account has before one page could be shown.
  - `sortBy` is `visitedAt` (default) or `ip`; `sortOrder` is `desc` (default) or `asc`. A value that
    names nothing is `400 validation_failed` with `errors.sortBy` / `errors.sortOrder` carrying the
    names that do exist. They are parsed from plain text by `SessionsController.ParseSortField` /
    `ParseSortOrder` rather than bound to the enums, deliberately: enum binding takes a member name
    spelled in full, so the conventional `sortOrder=desc` would be refused while the unabbreviated
    `sortOrder=Descending` was accepted, and an API that answers a request this conventional with a
    400 teaches its callers to spell things oddly.
  - **An address sorts as text**, so it is the database's own collation that decides what ascending
    means: on this machine `::1` comes first because the locale drops the colons, and
    `10.0.0.10` still precedes `10.0.0.2`. That is not a claim this code makes about addresses, it
    is a claim about text. What the code does enforce is that a **null address sorts last in both
    directions** — reversing the comparison would reverse that too, and an account whose older rows
    have no address would open with a screenful of blanks.
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

Layering follows the usual split: `SessionVisit` / `SessionHistoryPage` in
`Fluxy.Core/Authentication/ReadModels/`, `SessionHistoryLimits` / `SessionSortField` /
`SessionSortOrder` in `Fluxy.Core/Authentication/Utils/` and `Enums/`,
`ISessionHistoryService` in `Fluxy.Core/Authentication/Interfaces/`,
`SessionHistoryService` in `Fluxy.Application/Authentication/Services/` (Scoped, `AsNoTracking`),
the contracts in `Fluxy.API/Contracts/Sessions/SessionHistoryResponse.cs`, and the action on
`SessionsController` — the controller that holds every session read and revoke, beside
`ProfileController`, which owns `GET/PATCH /auth/profile` and `GET /auth/geo/lookup`.

Verified against a running instance with seeded rows of five ages, three roles and one blocked
account: 200 for Client, Reseller and Admin alike; ordering `created_at DESC`; `total` 6 with
`pageSize=2` giving 2/2/2/0 across four pages (page 4 empty but still carrying the true `total`);
the four refusals above; and no SQL emitted at all after a `validation_failed`.

Verified again once search and sort existed, again against a running instance: `sortOrder=asc` runs
oldest first and is the exact reverse of the default; `sortBy=ip` in both directions gives the same
addresses mirrored, with a null address last in each; `sortBy=IP` and `sortorder=DESC` answer 200, so
the parameter names are read case-insensitively; a search narrows the list, every returned row really
matches, a search for nothing answers `total` 0 with an empty page, and pages of a filtered list sum
to its `total` with the same `total` reported on each; `sortBy=organisation`, `sortOrder=downwards`,
`sortBy=2` (a bare number that names no member), a 301-character `search`, `page=0` and `pageSize=1001`
are each refused with the field they broke named in `errors`; and all five parameters answer together
on one request.

## Active sessions: `GET /auth/active-sessions` and its two revokes

The page at `/sessions/active` (all three areas) answers what the visit history above cannot:
**what is still signed in right now, and how to end it.** One card = one session, built from
the freshest live `refresh_tokens` row of that session. The history stays exactly as it was —
token level, the opposite question.

| Endpoint | Auth | CSRF | Answer |
| --- | --- | --- | --- |
| `GET /auth/active-sessions` | session | no | one row per live session, newest first |
| `DELETE /auth/active-sessions/{sessionId:guid}` | session | yes | `session_revoked` |
| `DELETE /auth/active-sessions/others` | session | yes | `other_sessions_revoked` + `revokedCount` |

- **No table, no migration.** Live means `revoked_at IS NULL AND expires_at > now`. The second
  half is the load-bearing one: `JwtTokenService.CleanupExpiredAsync` deletes expired rows only
  *during issuance and rotation*, so a chain that quietly expired while nobody signed in
  anywhere is still a row — and it can no longer be refreshed, which is the definition of not
  active. The list therefore must not depend on when cleanup last ran (class remarks of
  `ActiveSessionService` say the same at length).
- **One row per session, the deliberate opposite of the history**: history lists every rotation
  so a network change is visible; this lists what is still signed in. A rotation is not a
  second session — between rotations exactly one live row per session exists, so the freshest
  row *is* the session, and its address/user agent/`created_at` describe the card. GeoIP is
  resolved at read time through `IGeoIpResolver` with a per-request memo keyed by address
  (same as the history; no `.mmdb` → three nulls and a list that still reads).
- **Refusals are the contract.** Blocked account → `403 account_not_active` on all three (the
  service returns null when the account is not `Registered`; the two revokes gate on
  `GetProfileAsync`). Current session → `400 cannot_revoke_current`, refused **before any
  database work** — whether the caller may end this id is first a fact about the request, and
  the session plainly exists, so 404 would contradict the disabled button on its own row.
  Foreign, missing, already-revoked **and expired-only** ids → one answer `404
  session_not_found`: the ownership check lives inside the revoke predicate itself, so four
  situations cannot be told apart and the endpoint is no probe for other accounts' rows. A
  path segment that is not a guid matches no route (`{sessionId:guid}`) → framework `404
  not_found`; no session → `401 auth_required` before CSRF on the revokes; no `X-CSRF-Token` on
  either DELETE → `400 csrf_invalid`.
- **Both revoke predicates require `ExpiresAt > now`**, mirroring the read side. "Live" has to
  mean the same thing in all three methods: without it a dead chain would answer 200 for an
  ending that changes nothing (its id could not have come from the page — the list never shows
  it), and `revokedCount` would name cards the reader never saw.
- **Revoke is `ExecuteUpdateAsync` of `revoked_at` on live rows only — never a delete.** The
  timestamp on a spent row is the record of when it was spent. Deleting is cleanup's job, and
  cleanup runs globally during issuance: a planted one-day-old expired row is gone at the very
  first sign-in of a run, before any list or revoke call was made.
- No captcha, no attempt window (an index read costs what the page that asked for it costs;
  ending sessions is what an alarmed person reaches for), no pagination or search (a handful of
  sessions at most, and the bulk button revokes *by exclusion*, which needs the whole list in
  one answer). `userId` and `sessionId` come only from token claims — there is no parameter
  that could name another account.
- An ended session's **access tokens are all refused at once** through the
  `fluxy:revoked:sid:<sessionId>` marker, not just the token that made the request. One marker
  covers every token the session ever held, including those minted by earlier rotations, and it
  is kept for `Auth:AccessLifetime` plus the 30 s clock skew. If redis is unreachable the store
  fails open and the tokens fall back to their own expiry, while the refresh chain stays
  revoked — that is the only remaining window, and it is the documented fail-open direction.
- `revokedCount` counts **sessions**, not tokens, and reads the ids before the update for the
  same reason the message on the button says "sessions".

Layering follows the usual split: `ActiveSession` / `ActiveSessionList` in
`Fluxy.Core/Authentication/ReadModels/`, `IActiveSessionService` in
`Fluxy.Core/Authentication/Interfaces/`,
`ActiveSessionService` in `Fluxy.Application/Authentication/Services/` (Scoped,
`AddScoped<IActiveSessionService, ActiveSessionService>()`), `RevokeUserSessionAsync` /
`RevokeAllExceptCurrentAsync` added to `ITokenService` and implemented in `JwtTokenService`,
the contracts in `Fluxy.API/Contracts/Sessions/ActiveSessionResponse.cs`, and the three actions on
`SessionsController` beside the history.

Verified by running against a live instance (throwaway accounts, planted expired row, both
launch profiles auto-detected by the harness — 45 checks, all passing):

| Scenario | Result |
| --- | --- |
| two sign-ins of one account, then `GET` | 200, exactly one row per session, newest first, `isCurrent` on exactly one |
| planted expired-but-unrevoked row | not listed; already gone at the first sign-in — issuance cleanup, not the list |
| `DELETE` a second session | 200 `session_revoked`, list shrinks, that session's own view stops containing it |
| `DELETE` the current session | 400 `cannot_revoke_current`, still signed in, still listed |
| foreign / unknown / expired-only id | the same 404 `session_not_found`, foreign session untouched |
| non-guid segment | route `404 not_found` — `others` not shadowed by `{sessionId}` |
| refresh rotation | still one row per session, now the newest |
| `DELETE` without the header | 400 `csrf_invalid`, nothing ended |
| no session (GET and `others` DELETE) | 401 `auth_required` |
| blocked account (GET and `DELETE`) | 403 `account_not_active`, unblock restores 200 |
| `DELETE .../others` | 200, `revokedCount` = live non-current only (expired chain *not* counted), only current survives, expired chain untouched, other account untouched |
| `others` again | 200, `revokedCount` 0 |
| the ended session's `POST /auth/refresh` | 401 `session_expired` — chain dead |

## PostgreSQL is wired up (EF Core 10, Npgsql provider)

The app talks to the compose postgres, to the already existing `fluxy` database, through EF Core:

- `Fluxy.DataAccess` references `Npgsql.EntityFrameworkCore.PostgreSQL` **10.0.3**, `Microsoft.EntityFrameworkCore` **10.0.12** (pinned explicitly) and `Microsoft.EntityFrameworkCore.Design` **10.0.12** (`PrivateAssets=all`). The explicit EF reference is not redundant: the provider only requires EF `>= 10.0.4`, so without it NuGet resolved 10.0.4 against the 10.0.12 of Design and the build failed with **MSB3277**. Never use the 11.x line of the provider — it targets EF Core 11.
- **The MSB3277 trap now belongs to `Fluxy.API`.** Because Design is `PrivateAssets=all`, the Relational 10.0.12 it drags into DataAccess does not flow to consumers, so the API would resolve Relational 10.0.4 against the 10.0.12 of `Fluxy.DataAccess.dll`. Fixed by pinning `Microsoft.EntityFrameworkCore.Relational` **10.0.12** explicitly in `Fluxy.API.csproj`. `Fluxy.Application` references Core only and has no EF package at all. **Any new project that references `Fluxy.DataAccess` must carry the same pin**, otherwise it builds with MSB3277 and then throws a confusing `ServiceProvider` failure at runtime.
- `Fluxy.DataAccess/Context/FluxyDbContext.cs` — plain **internal** `DbContext`, one `DbContextOptions<FluxyDbContext>` constructor, its own `DbSet`s, both `SaveChanges` overloads stamping the audit columns, and `OnModelCreating` that applies all `IEntityTypeConfiguration` from its own assembly and then the entity conventions (see "Domain rules" below). Internal is what makes "Application cannot reach EF" a compiler rule rather than a habit.
- `Fluxy.DataAccess/Context/FluxyDbContextFactory.cs` — internal `IDesignTimeDbContextFactory`, so `dotnet ef` works without starting the application, and still discovers the factory when it is not public. The connection string only selects the provider and is never used to connect; it reads `ConnectionStrings__Postgres` and otherwise falls back to a placeholder. Set that variable for `dotnet ef database update`, otherwise the command would try the placeholder credentials.
- `Fluxy.DataAccess/ServiceCollectionExtensions.cs` — `services.AddDataAccess(configuration)` reads `ConnectionStrings:Postgres`, calls `UseNpgsql`, and registers every repository plus `IUnitOfWork` and `IDatabaseInitializer` Scoped. It **throws `InvalidOperationException` at startup** when the connection string is missing, with a message naming the three ways to provide it. That is the intended fail-fast, not a bug to silence.
- `Program.cs` calls `builder.Services.AddDataAccess(builder.Configuration)` — the API layer never names Npgsql or EF Core, and never names a repository implementation.

### Redis is now connected (StackExchange.Redis 3.3.1)

Only `Fluxy.DataAccess` references **StackExchange.Redis 3.3.1**. `Fluxy.Application` knows redis only through the `IAttemptThrottle` and `ITokenRevocationStore` interfaces in Core; the implementations live in `Fluxy.DataAccess/Infrastructure/Redis/`.

- `Fluxy.DataAccess/ServiceCollectionExtensions.cs` has `AddRedis(configuration)`, which reads `ConnectionStrings:Redis`, parses it at startup (`ConfigurationOptions.Parse`) so a malformed value fails loudly, and registers a **singleton** `IConnectionMultiplexer` when the string exists. It forces `AbortOnConnectFail = false`, so a redis that is not running does **not** prevent the host from starting.
- `AddRedis` also registers `RedisConnection` and both stores **whether or not a connection string exists**. `RedisConnection` is the one place that holds the nullable multiplexer and answers `TryGetDatabase`; a nullable constructor parameter on each store would not be optional to Microsoft.Extensions.DependencyInjection, so without this wrapper a missing connection string would fail on the first request that resolves a store instead of degrading.
- `AddDerivedRedisConnectionString` in `DotEnvConfigurationExtensions.cs` mirrors `AddDerivedPostgresConnectionString`: it composes `ConnectionStrings:Redis` from `REDIS_USER` / `REDIS_PASSWORD` / `REDIS_HOST` / `REDIS_PORT` in the `.env` file, defaulting to `localhost:6379`. The `user=` segment is **mandatory** because compose runs redis with `user default off`.
- `RedisAttemptThrottle` (DataAccess/Infrastructure/Redis) uses a Lua script (`INCR` + `PEXPIRE` on first) so several instances share the window. It **falls back to a `ConcurrentDictionary` in process memory** when redis is unreachable, so registration keeps working even with redis down.
- `RedisTokenRevocationStore` (same folder) keeps revoked session identifiers for `Auth:AccessLifetime` plus the 30 s clock skew and **fails open** when redis is unreachable.
- **The `Redis:Required` config key (`true`/`false`) controls whether a missing connection string is fatal.** It defaults to non-fatal; `false` leaves the stores registered with a null connection, which is the documented degraded state.

### The GeoIP databases live in the data access layer (MaxMind.GeoIP2 6.1.0)

Only `Fluxy.DataAccess` references **MaxMind.GeoIP2 6.1.0**. The reader of a data source is a mechanism, not a business rule, so `MaxMindGeoIpResolver` and `GeoIpOptions` live in `Fluxy.DataAccess/Infrastructure/GeoIp/`, behind `IGeoIpResolver` from Core — the same shape as the redis stores.

- `Fluxy.DataAccess/ServiceCollectionExtensions.cs` has `AddGeoIp(configuration)`, which binds the `GeoIp` section and registers the resolver as a **singleton**: the readers are opened once and on first use rather than at startup, so a host without the files still starts and every address resolves as unknown.
- The one answer the reader and the guard share — what a local address is — lives in `Fluxy.Core/GeoIp/Utils/GeoIpAddress.cs`, because the reader is in the data access layer and the guard is a business rule, and neither may be the only place that knows it.
- `Microsoft.Extensions.Options.ConfigurationExtensions` 10.0.12 is pinned in `Fluxy.DataAccess.csproj` for that binding, and `Microsoft.Extensions.Hosting` 10.0.12 stays pinned in `Fluxy.Application.csproj` for `IHostEnvironment`.

**Where the connection string comes from.** There is no `ConnectionStrings` section in either appsettings file. `Extensions/DotEnvConfigurationExtensions.cs` loads the compose `.env` and **composes** `ConnectionStrings:Postgres` from its `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` keys (`POSTGRES_HOST` / `POSTGRES_PORT` are honoured, defaulting to `localhost:5432`, which is right for a host process and wrong for a container — that would need the service name `postgres`). Consequences worth remembering:

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

`Fluxy.Core/Persistence/Base/AuditableEntity.cs` supplies the three properties — public getters, **private** setters, so EF can write them and nothing else can. It has two constructors: one that generates the key, and one that rehydrates a row (used by `UserEntity.FromModel`).

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
| Model | `Fluxy.Core/Users/Models/User.cs` | `sealed record` with init-only properties; `UserRole` and `UserStatus` are in `Fluxy.Core/Users/Enums/` |
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

`Fluxy.Application/Users/Services/DefaultUserSeeder.cs`, reached through `AddApplicationServices()` in `Program.cs` and run by `await app.Services.SeedDatabaseAsync()` **before** `app.Run()`.

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

- **Postgres and redis are both connected from the app** (see the sections above). Redis holds the rate-limit counters and the identifiers of revoked sessions; the counters fall back to process memory and the denylist fails open when it is unreachable. There is no cache, no session store, and no distributed lock. pgadmin is manual tooling.
- compose bind-mounts `../2. Init Database` into `/docker-entrypoint-initdb.d`, but **that folder does not exist** in the repo, so Docker silently creates an empty directory in the parent and Postgres gets no bootstrap SQL.
- `UserSecretsId` is set — use `dotnet user-secrets --project Fluxy.API set "..."` rather than committing connection strings to appsettings. The app prefers it over the `.env`-derived value.
- Ports are on `0.0.0.0`, so the LAN can attempt every service. Password strength is the only control; nothing here is hardened for the public internet.

## The registration endpoints

Three endpoints exist now. They are the first thing in the API layer, and they fix the empty-Swagger problem described earlier.

| Endpoint | Auth | Rate limit | Purpose |
| --- | --- | --- | --- |
| `GET /auth/csrf` | none | none | Issue the antiforgery token |
| `POST /auth/registrations` | CSRF + captcha | 1 per IP / 15 min | Create account, mail confirmation code (201, no `Location` — see below) |
| `POST /auth/registrations/confirm` | CSRF + captcha | 10 per IP / 15 min | Confirm account with the code |

**There is no `api` segment in any route, on purpose.** This process serves nothing but the API, so a prefix that distinguishes it from a website tells nobody anything, and it becomes one more thing to change when the API is moved behind a path of its own — which a reverse proxy or a gateway adds anyway. The route lives in one place, `[Route("auth")]` on `AuthController`; the frontend builds its paths from the same shape in `src/lib/api.js`. If a shared host ever needs the distinction back, put it in front of the application as a path prefix rather than in the controllers.

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

Verified over the LAN address: `GET /auth/csrf` → 200 with both cookies and no `secure` flag; `POST /auth/registrations` with the cookie and header → `captcha_invalid`, which is the next gate after CSRF; the same POST without the cookie → `csrf_invalid`.

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

reCAPTCHA v3 tokens are minted by Google's JavaScript **running in a page**. There is no server-side way to produce one: `siteverify` only checks a token a page has already obtained. So with `RECAPTCHA_SECRET_KEY` set, `POST /auth/registrations` and `POST /auth/registrations/confirm` are **browser-only by construction** — a native app, `curl` or a server-to-server caller gets `captcha_invalid` however correct its antiforgery pair is. Verified: a correct cookie and header over the LAN address advances past CSRF and is then refused at the captcha.

This is a real conflict between two reasonable requirements and it has no clean code fix. Exempting non-browser callers would remove the protection from exactly the callers worth protecting, since an attacker impersonating a legitimate integration is the case the check exists for. The workable answer is deployment-shaped rather than code-shaped: **leave the key empty on a development machine and set it only on the instance meant to serve browsers.** An empty key bypasses the check, which is also why a production deployment that forgot it logs a warning instead of failing quietly.

### reCAPTCHA

Bypassed when `Recaptcha:SecretKey` is empty. This keeps a dev machine usable and is the reason `getCaptchaToken()` can return `null` in DEV. **A startup warning is logged when the key is missing** so a production deployment that forgot it does not silently have no protection.

The action is fixed per endpoint, not chosen by the caller: `register` for `POST /auth/registrations`, `register_confirm` for `POST /auth/registrations/confirm`. The captcha action is a token property, not a path — its spelling did not move with the rename. The frontend must call `getCaptchaToken('register')` to match.

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

## API design aims at REST; every deviation is agreed explicitly

When designing a new endpoint, reach for REST first: **plural noun resources**, the method that
describes the operation (`GET` read-only, `POST` create, `PATCH` partial update, `DELETE` remove),
and the status code that describes the outcome (`201` on create — with a `Location` naming the new
resource, when it has a URI the client can address right away — `204` where
there is nothing to say back, `404` for a resource that is not there). A new route that reads as a
verb (`/do-something`) is a smell — check whether it is really an operation on a resource before
naming it that way.

**Any departure from that is a decision, not an accident, and the owner signs off on it before it
is written.** The existing departures are all approved and each one carries its reason in the XML
docs of the controller that owns it; a new one must be agreed with the owner first and then
recorded the same way — in the endpoint's remarks and in this file. The ones already agreed:

- **The three per-role sign-in paths** (`POST /auth/client-login|reseller-login|admin-login`) —
  one path per entrance, so a stale URL cannot point at an audience it was not built for.
- **RPC-style session actions** (`POST /auth/refresh`, `POST /auth/logout`, `GET /auth/csrf`,
  `GET /auth/me`) — there is no resource to name: a refresh mints tokens, a logout ends state that
  lives across two stores, `csrf` and `me` are questions rather than things.
- **`GET /auth/me` beside `GET /auth/profile`** — two readings of "who am I": `me` is the cheap
  session identity the guest guard polls on every navigation, `profile` is the full account row
  with the login guard. Merging them would put the guest guard's round trip behind a database
  read it does not need.
- **Three admin account actions as `POST {id}/…`** (`confirm-registration`, `block`, `unblock`
  under `admin/users`) — each is a state transition on an existing row, and the alternatives were
  both worse: `PATCH {id}` with a status is a field write that would have to carry all of the
  transition rules (restore `Unregistered` vs `Registered`, clearing `registered_at`, revoking
  sessions) in the caller's payload, and `DELETE`/`PUT` cannot mean "confirm" at all. Keeping them
  as their own addressed actions also lets each answer with its own code
  (`registration_confirmed`, `user_blocked`, `user_unblocked`) and its own refusal
  (`invalid_status`) instead of overloading `user_updated`. The rest of the surface is plain
  REST — see the admin users section below.

The May 2026 REST pass collapsed the five `POST /auth/profile/*` change endpoints into a single
`PATCH /auth/profile` and renamed `register`/`password` into the plural resources
`registrations`/`password-resets`. `POST /auth/registrations` answers **201 — deliberately
without a `Location`**: `Location` names the URI of what was created, and this row has none a
client can address yet — `/auth/profile` answers 401 for its own owner until the mailed code
confirms it, and a pending-registration URI addressable by email would be the enumeration
oracle the refusal policy exists to avoid (the reasoning is in the action's remarks; RFC 9110
makes the header a SHOULD, and this is the case it leaves open). Every private read
(`me`, `profile`, `sessions`, `active-sessions`, `geo/lookup`, `csrf`) carries
`Cache-Control: no-store` through `ApiControllerBase.NoStore()`, because a shared cache that
kept one of those bodies would hand one visitor's account to the next. Its rule — **exactly one
change kind per PATCH** — exists
because the staged-confirmation flow holds one pending change at a time, and the time zone (which
is applied at once, has no captcha and its own attempt window) must never share a request with a
password change (which is mailed a code and revokes sessions): the endpoint refuses a body that
names two kinds with `validation_failed` rather than guessing an order. Contract shape:
`{ currentPassword?, username? | email? | newPassword? | loginGuard? | timeZone? }`, where an
absent property means "leave it alone" and an empty `timeZone` clears the preference.

## The admin users API: `admin/users`

One resource, eight endpoints, owned by `UsersController` (`[Route("admin/users")]`,
`[Authorize(Policy = AdminPolicy)]`, deriving from `ApiControllerBase` for `NoStore()` and the
antiforgery helper).

| Endpoint | Answer |
| --- | --- |
| `GET /admin/users` | page: `page`, `pageSize`, `search`, `role`, `status`, `sortBy`, `sortOrder` |
| `GET /admin/users/{id}` | one account with its login guard; `404 user_not_found` |
| `POST /admin/users` | `201` + relative `Location: /admin/users/{id}` |
| `PATCH /admin/users/{id}` | partial update; `404 user_not_found` |
| `POST /admin/users/{id}/confirm-registration` | `registration_confirmed` |
| `POST /admin/users/{id}/block` / `…/unblock` | `user_blocked` / `user_unblocked` |
| `DELETE /admin/users/{id}` | `user_deleted` |

- **`Location` is honest here**, unlike on `POST /auth/registrations`: the row exists and
  `/admin/users/{id}` answers `200` for its own owner immediately — no mailed code stands between
  creation and addressability, and the caller is an administrator who already knows the account.
- **No captcha and no attempt window, deliberately.** Every write is behind an `AdminOnly`
  session *and* a fresh antiforgery token (`RejectsCsrfAsync()` first, before any body is read),
  so the expensive steps are already gated by something stronger than a throttle; the reCAPTCHA
  cannot be minted outside a page anyway. This is a recorded departure (see the list above), and
  the reason lives in the controller's remarks.
- **No data annotations on the requests on purpose.** Framework model validation runs *before*
  the action, which would answer a stale token with `400 validation_failed` instead of
  `csrf_invalid`; every field check therefore happens in the action/service after the
  antiforgery call. Field errors come back keyed by the camelCase JSON name (`username`,
  `password`, `role`, `status`, `loginGuard.…`).
- **`search` is a case-insensitive substring of username or email**, `role`/`status` filter by
  enum member name (unknown member → `validation_failed`, *not* "no filter"), and `total` is
  counted before paging under the same predicate — the same "the pager must not lie" rule the
  session history follows. Sort fields: `username` (default, asc), `email`, `role`, `status`,
  `lastSeenAt`, `ip`, each tie-broken by username and then id, parsed from plain text
  case-insensitively so `sortBy=IP` and `sortOrder=desc` both work.
- **`lastSeenAt` and `ip` are correlated subqueries** over the freshest `refresh_tokens` row, and
  `ChainNullsLast` groups the never-signed-in accounts to the end of *both* directions: plain
  PostgreSQL `DESC` treats null as larger than any value, which would open a "newest activity
  first" page with accounts nobody has ever seen. (Both translate — verified against the compose
  postgres, SQL in the log: `ORDER BY (SELECT max(r.created_at) … WHERE r.user_id = u.id) DESC,
  u.username DESC`.) The page itself then reads the visits in memory, one `IN` query per page.
- **Status transitions are the service's rules, not the controller's**: `RegisteredAt` is stamped
  when the status becomes non-`Unregistered` and cleared when it returns to it; `unblock`
  restores `Unregistered` if `RegisteredAt` is null, else `Registered`; `confirm` and `unblock`
  refuse a row that is not in the state they expect with `409 invalid_status`; `block` is
  idempotent; going to `Blocked`/`Unregistered` (or changing a password) revokes the account's
  sessions. **Self-protection is refused by the service with `actingUserId`**, so the three
  answers `cannot_delete_self`, `cannot_block_self`, `cannot_demote_self` cannot depend on the
  controller remembering to pass who is asking.
- The login guard here is edited **without** the caller-address anti-lock rule: that rule stops
  an administrator from locking *themselves* out of *their own* account, and an admin editing
  someone else's allow lists is not on those lists.

Layering follows the usual split: `AdminUser` / `AdminUserPage` / `AdminUserDetail` in
`Fluxy.Core/Users/ReadModels/`, `NewUser` / `UserPatch` in `Fluxy.Core/Users/Commands/`,
`AdminUserSortField` / `AdminUserSortOrder` in `Fluxy.Core/Users/Enums/`,
`AdminUserListLimits` in `Fluxy.Core/Users/Utils/`,
`IUserAdminService` in `Fluxy.Core/Users/Interfaces/`,
`UserAdminService` in `Fluxy.Application/Users/Services/` (registered Scoped), the shared
`LoginGuardNormalizer`/`LoginGuardStore` and `TimeZonePolicy` beside it (extracted from
`ProfileService` so both flows agree on what a valid guard and time zone are), and the contracts
in `Fluxy.API/Contracts/Users/`.

Verified against a running instance (four scripted batches, all green): list with all six sort
keys, filters, search, paging and each of the seven parameter refusals; `201`+`Location`,
`409 user_already_exists` naming the offending field, `400 validation_failed` for missing and
weak values, `400 csrf_invalid` with genuinely no header; detail/patch/confirm/block/unblock/
delete each `404 user_not_found` on an unused id; PATCH persisting username, email, role, a
cleared time zone, the guard, `status=Blocked` keeping `registeredAt` and `status=Unregistered`
clearing it; block → `200` twice (idempotent) → the victim's own `POST /auth/refresh` answering
`401 session_expired`; the three self-protections; `403` for a signed-in Client and `401`
anonymous; a rotated password signing in while the old one is refused; and every fixture deleted
with no orphaned guard rules left behind.

## The user groups API: `admin/user-groups`

Groups are what a role turned into: a row carrying **one** of the three roles plus the
permissions its members inherit. `users.role` was **dropped** by `AddUserGroups` — the role a
caller holds is read through `Include(Group)` from the group the account names, and the
permission gates below are the second, independent check that sits on top of it. A group is
therefore the thing the admin area manages and the account merely points at.

| Endpoint | Auth | Answer |
| --- | --- | --- |
| `GET /admin/user-groups` | `Permission.ViewUserGroups` | page: `page`, `pageSize`, `search`, `role`, `status`, `sortBy`, `sortOrder` |
| `GET /admin/user-groups/{id}` | `Permission.ViewUserGroups` | one group with `permissions` and `members`; `404 user_group_not_found` |
| `POST /admin/user-groups` | `Permission.EditUserGroups` | `201 user_group_created` + relative `Location` |
| `PATCH /admin/user-groups/{id}` | `Permission.EditUserGroups` | `200 user_group_updated` |
| `DELETE /admin/user-groups/{id}` | `Permission.EditUserGroups` | `200 user_group_deleted` |

The class still carries `[Authorize(Policy = AdminPolicy)]`. **Role and permission are two
separate requirements, not one**: the four policies (`Permission.ViewUsers`, `.EditUsers`,
`.ViewUserGroups`, `.EditUserGroups`, the constants on `AuthenticationExtensions`) each demand
*both* `RoleRequirement(Admin)` and their `PermissionRequirement`. That is deliberate — a
permission key on its own would be checkable by any signed-in account that somehow held it,
and a role on its own is what the API already had. **Reads demand the view permission, writes
the edit one**, so an operator who may see the group list still gets `403 permission_denied`
from a `PATCH`.

- **Three base groups, always.** Their identifiers are constants in `BaseUserGroups`
  (`…0001` Clients, `…0002` Resellers, `…0003` Administrators) rather than values the database
  invents, because three different places must name the same rows without having seen each
  other: the migration that backfills accounts, the startup seeder, and the service that
  refuses to delete them. **Base-ness is the identifier** — `BaseUserGroups.IsBase(id)` — which
  is why renaming a base group cannot un-base it.
- **A base group accepts `name` and nothing else.** `UserGroupService` refuses a `PATCH` that
  carries `Role`, `Status` *or* `Permissions` on one with `409 user_group_immutable`, and
  refuses to delete one the same way. Rename is the whole of what the form offers, and the
  controls are drawn disabled rather than hidden so the visitor is told before the click.
- **`409 user_group_in_use`** for any group that still has a member (`users.group_id` is
  `RESTRICT`, so the database would refuse it anyway — the service checks first to answer with
  a code instead of a `DbUpdateException`). Delete on an empty group of one's own is the only
  group that goes.
- **The permission catalog is role-scoped, and the two failure modes are different on
  purpose.** A name that does not exist (`viewEverything`) is **refused** with
  `validation_failed` and the list that does — a client wrong about the contract is told so. A
  *valid* permission the chosen role does not own (`viewClients` on a Client group) is **dropped**
  — that is a client describing a group, and the server already knows what a Client group may
  hold. The catalog is `[Flags]` powers of two stored as `integer` in
  `user_group_permissions`, and it now has **twenty-two** members in six groups: the two
  **group-management** keys (`ViewUserGroups=4`, `EditUserGroups=8`), the six **account** keys
  split by the role of the account they act on (`ViewClients=16, ViewResellers=32,
  ViewAdmins=64, EditClients=128, EditResellers=256, EditAdmins=512`), the four **stream**
  keys — the video pair (`ViewStreams=1024`, `EditStreams=2048`) and the audio pair
  (`ViewAudioStreams=4096`, `EditAudioStreams=8192`), which are deliberately **not** the same
  pair: a group that may watch the video streams may not watch the audio ones — the
  **on-demand** pair (`ViewVideoOnDemand=16384`, `EditVideoOnDemand=32768`), the **series**
  pair (`ViewSeries=65536`, `EditSeries=131072`), the **guide** pair
  (`ViewEpgSources=262144`, `EditEpgSources=524288`), the **bouquet** pair
  (`ViewBouquets=1048576`, `EditBouquets=2097152`) and the **tariff plan** pair
  (`ViewTariffPlans=4194304`, `EditTariffPlans=8388608`). All twenty-two are
  owned by `Admin`, so
  `permissionsForRole` on the frontend answers an empty set for Client and Reseller and their
  form shows a note instead of boxes.
  - **A permission added in code reaches the base groups two ways.** The startup seeder
    (`DefaultUserSeeder.ReconcilePermissionsAsync`) reconciles the three base groups against
    `UserPermissionCatalog` on every start, which is why adding a member needs no migration. The
    tariff plan pair also got a data-only migration
    (`20261028140626_GrantTariffPlanPermissions`) for the installation that has been migrated
    but not yet restarted: it inserts the two grants for the Administrators base group with a
    `WHERE NOT EXISTS` guard, because the seeder may already have written them and the unique
    index over (user_group_id, permission) would refuse a second copy and fail the migration.
  - **The column is `integer`, not `smallint`.** The on-demand write key is `32768`, one more
    than a `smallint` can hold; the first scaffold therefore wrapped it and the base group
    stored a negative grant. `WidenUserGroupPermission` widens the column, which is lossless
    for every value that was already there. The enum's values are still the schema, and a new
    member is still a value nobody has used yet.
  - **The account keys start at 16, not at 1.** The values `1` and `2` were the retired
    `viewUsers`/`editUsers` of the first version — one pair for accounts in general, replaced
    by the per-target split above — and the numbers are schema, so renumbering the live ones
    would reinterpret every stored grant as a grant of something else without a single query
    being made. `1` and `2` are therefore empty and must not be handed to a new meaning.
  - **The stream keys are not split by role, because a stream is not an account.** There is
    one `ViewStreams` and one `EditStreams` and nothing narrower, for the same reason the
    content categories carry no permission at all: one installation-wide list of streams, one
    screen that draws it, no second axis to split along. They are a pair rather than one key so
    that a group which may watch the streams and a group which may run them stay two grants.
    The audio pair is a **second** such pair rather than a widening of the first: the two
    media are two resources with two tables and two routes, and a grant that named both would
    be a grant to a thing nobody can administer — the group form would then offer one box for
    two screens and neither screen could be locked away on its own.
  - **Adding a permission needs no migration.** The catalog is code; the base groups are
    reconciled with it at startup (the bullet below), so a new member appears on the
    Administrators row on the next boot. It does need a line in `UserPermissionCatalog.Owner`
    — an unowned member is a grant nothing ever answers — and the frontend's `ALL_PERMISSIONS`/
    `OWNER` (`src/lib/permissions.ts`) plus the two locale `permissions.*` blocks, or its
    checkbox label degrades to the raw key.
- **The seeder reconciles the base groups at startup**: role and permissions are restored to
  what the catalog says, `name` is left alone (an installation may have renamed them, and that
  is allowed), and permissions are reconciled in both directions so a grant removed from the
  catalog does not linger on the Administrators row.
- **Effective status is the most restrictive of the account's and its group's**
  (`UserStatusComposition.Combine`: `Blocked` > `Unregistered` > `Registered`). The accounts
  list draws that effective value; the detail shows the row's own `Status` beside a read-only
  `EffectiveStatus`. The same composition is copied to `src/lib/userStatus.ts` on the frontend,
  with its `RESTRICTIVENESS` map written out because the `UserStatus` enum's *numbers* are in
  the wrong order for this question.
- **No captcha, no attempt window** — the same recorded departure as `admin/users`: every write
  is behind an `AdminOnly` session plus a fresh antiforgery token, and reCAPTCHA cannot be
  minted outside a page. `RejectsCsrfAsync()` runs first, before any body is read.

**Codes:** `user_group_created|updated|deleted|not_found|already_exists|immutable|in_use`,
plus `validation_failed` and `permission_denied` — all translated in both locale bundles (the
probe imports `en.ts`/`ru.ts` and looks each one up).

**Migration `20261009130702_AddUserGroups` is hand-written**, and its order is the point: create
`user_groups` → insert the three base groups → insert the four admin permission rows → add
`users.group_id` nullable → three `UPDATE … WHERE role = n` backfilling every existing account
onto its matching base group → `SET NOT NULL` → drop `users.role` → index and FK. No `CASE`
fallback for an account that matches no base group: on this installation every account did,
and a fallback would have hidden a row the backfill had missed instead of surfacing it.

Layering follows the usual split: `UserGroup` in `Fluxy.Core/Users/Models/`,
`UserGroupListItem` / `UserGroupPage` / `UserGroupDetail` in `Fluxy.Core/Users/ReadModels/`,
`NewUserGroup` / `UserGroupPatch` in `Fluxy.Core/Users/Commands/`,
`UserGroupOutcome` / `UserGroupAction` / `UserGroupSortField` / `UserGroupSortOrder` /
`UserPermission` in `Fluxy.Core/Users/Enums/`,
`UserGroupListLimits` / `UserPermissionCatalog` / `BaseUserGroups` / `UserStatusComposition` in
`Fluxy.Core/Users/Utils/`, `IUserGroupService` in `Fluxy.Core/Users/Interfaces/`,
`UserGroupService` in `Fluxy.Application/Users/Services/` (registered Scoped), the entities and
their two configurations in `Fluxy.DataAccess/`, and the contracts in
`Fluxy.API/Contracts/UserGroups/` (`AdminUserGroup*`, `CreateUserGroupRequest` /
`UpdateUserGroupRequest`, `BulkUserGroupsRequest`, `UserGroupResponses`).

Verified against a running instance by `npm run probe:groups` in the frontend (**28/28**, which
drives the app's own modules rather than `curl`), the load-bearing rows being: the three base
groups present and only those flagged; the Admin base group granting four and the Client one
granting **zero**; a duplicate name → `409 user_group_already_exists`; the unknown-vs-wrong-role
asymmetry above, with the row re-read to prove nothing was stored; rename → `200` while role,
permissions and deletion of a base group each → `409 user_group_immutable`; a populated group →
`409 user_group_in_use`; an unused id → `404 user_group_not_found`; a write with no antiforgery
token → `400 csrf_invalid` (so the CSRF check runs *before* the rules, not after — it must not
come back as a 409 wearing a different hat); `GET /admin/users?groupId=` returning only that
group's accounts; and a second account holding only `viewUsers`+`editUsers` still reading
accounts while every group endpoint answers `403 permission_denied`.

## The content categories API: `admin/content-categories`

The first piece of the content domain. A category is a named, typed, ordered section that
pieces of content will be filed under; the content itself does not exist yet.

| Endpoint | Auth | Answer |
| --- | --- | --- |
| `GET /admin/content-categories?type=` | `AdminOnly` | every category of the type, lowest `sort_order` first |
| `POST /admin/content-categories` | CSRF | `201 content_category_created` + relative `Location` |
| `PATCH /admin/content-categories/{id}` | CSRF | `200 content_category_updated` (rename only) |
| `DELETE /admin/content-categories/{id}` | CSRF | `200 content_category_deleted`, `404 content_category_not_found` |
| `PUT /admin/content-categories/order` | CSRF | `200 content_categories_reordered` |

- **`ContentType` is four members, fixed** (`VideoStream=1, AudioStream=2, VideoOnDemand=3,
  Series=4`, `smallint` like every other enum). It is a partition of the domain, not a
  scale - nothing anywhere may compare two members. `type` is parsed from plain text
  case-insensitively (`videostream`, `audiostream`, `videoondemand`, `series`); an unknown
  value is refused with `validation_failed`, never treated as "no filter", because a GET
  without a type would fold four unrelated lists into one answer no screen draws.
- **The list is not paged, deliberately.** A type's categories are a screenful by
  construction - they are the section headings of a content grid - so the response is a
  bare `items` array with no `total`/`page`/`pageSize`. If a type ever outgrows one screen,
  that is the moment to add paging, not before. This is a recorded departure, and the
  reasoning lives in the controller's remarks.
- **The order is one whole-list write.** `PUT order` takes `{ contentType, orderedIds }`
  and the list must name **every** category of the type exactly once - a subset, a
  duplicate or a foreign id is refused whole (`errors.orderedIds`), because half an order
  is two rows sharing a position. The service writes positions 1..N over the named rows in
  one `SaveChanges`, so an interrupted request cannot leave two categories at one
  position. Positions are plain ints; deletions leave gaps and nothing hurries to close
  them (renumbering would be a second source for a fact the reorder owns), and a create
  appends at `max(sort_order) + 1`, read server-side rather than claimed by the caller.
- **Uniqueness is scoped to the type** (`UNIQUE(content_type, name)`): "Sports" over video
  streams and "Sports" over series are two unrelated sections of two different screens.
  Case sensitive, like username and email. The lost race is caught (`SQLSTATE 23505`) and
  mapped to `409 content_category_already_exists` with the field named.
- **No captcha and no attempt window** - the recorded admin-area departure; every write is
  behind an `AdminOnly` session and a fresh antiforgery pair, and `RejectsCsrfAsync()` runs
  before any body is read.
- **No "in use" refusal yet, on purpose.** No table of content items exists to be in one,
  so the check would guard an empty set. It arrives with the content tables - a foreign key
  with `ON DELETE RESTRICT` plus a `category_in_use` refusal naming the count - not before
  them. Until then the delete confirmation dialog on the frontend is the whole of the guard.
- **The frontend saves the order by an explicit button, not per drop.** A drag changes the
  screen only; "Save order" sends one `PUT order` for the whole arrangement. One
  arrangement, one request - the reduction in writes is the owner's stated goal, and it is
  also why the request is shaped as a full list: the screen knows all of it the moment the
  visitor is finished.

Layering follows the usual split: `ContentCategory` in `Fluxy.Core/Content/Models/`,
`NewContentCategory` + `ContentCategoryPatch` + `ReorderContentCategories` in
`Fluxy.Core/Content/Commands/`, `ContentType` + `ContentCategoryOutcome` +
`ContentCategoryAction` in `Fluxy.Core/Content/Enums/`, `ContentCategoryLimits` in
`Fluxy.Core/Content/Utils/`, `IContentCategoryService` in
`Fluxy.Core/Content/Interfaces/`, `ContentCategoryService` in
`Fluxy.Application/Content/Services/` (Scoped), the entity and its configuration in
`Fluxy.DataAccess/` (migration `20261012103956_AddContentCategories`), the contracts in
`Fluxy.API/Contracts/Content/`, and `ContentCategoriesController` in
`Fluxy.API/Controllers/Content/`.

Verified against a running instance (27 scripted checks, all green): empty list; missing and
unknown `type` refused with `errors.type`; a write without the antiforgery pair → `400
csrf_invalid` before any rule; create → `201` with an honest `Location`; duplicate within
the type → `409` naming `name`, the same name over a *different* type → `201`; blank name
and unknown type refused with the field named; the list ordered by `sort_order`; rename →
`200`, rename onto a taken name → `409`, foreign id → `404`, blank → `400`, empty body →
`200 content_category_updated`; reorder reversed → `200` and the next read answers the new
order with positions 1..N; partial, duplicate, foreign, non-uuid and unknown-type reorder
bodies each refused with the field named; delete → `200`, delete again → `404`, the gap
kept (1, 3) and the next create landing at `max + 1` (4); the four types fully independent;
and cleanup leaving no rows behind. The SQL in the log confirms the reorder is one batch of
two `UPDATE`s and the create reads `max(sort_order)` in its own query.

## The video streams API: `admin/video-streams`

The first piece of the relaying domain. A stream is a named, typed entry that the relay
process plays; the playing itself is not in this API. A stream has no position of its own.

| Endpoint | Auth | Answer |
| --- | --- | --- |
| `GET /admin/video-streams` | `Permission.ViewStreams` | page: `page`, `pageSize`, `search`, `categoryId`, `status`, `type`, `sortBy`, `sortOrder` |
| `GET /admin/video-streams/{id}` | `Permission.ViewStreams` | one stream with `sources` and `files`; `404 video_stream_not_found` |
| `POST /admin/video-streams` | CSRF + `Permission.EditStreams` | `201 video_stream_created` + relative `Location` |
| `PATCH /admin/video-streams/{id}` | CSRF + `Permission.EditStreams` | `200 video_stream_updated` |
| `POST /admin/video-streams/{id}/start` / `…/stop` / `…/restart` | CSRF + `Permission.EditStreams` | `video_stream_started` / `video_stream_stopped` / `video_stream_restarted` |
| `POST /admin/video-streams/bulk` | CSRF + `Permission.EditStreams` | `200 video_streams_bulk_completed`, one entry per identifier |
| `POST /admin/video-streams/import` | CSRF + `Permission.EditStreams` | `200 video_streams_import_completed`, counts and per-line problems |
| `DELETE /admin/video-streams/{id}` | CSRF + `Permission.EditStreams` | `200 video_stream_deleted`, `404` on a second |

- **The type owns the fields only it can use, and the two halves are asymmetric on purpose.**
  A `Permanent`/`OnDemand`/`Redirect` stream takes `sources`; `OnlineSeries` takes `seriesId`;
  `VodPlaylist` takes `vodId`; `FilePlaylist` takes `files`. A payload the chosen kind cannot
  use is **dropped**, not refused — a form whose operator has just changed the type is still
  carrying the previous kind's values, and refusing them would answer a request nobody meant.
  The one exception is `archiveDays`: only a `Permanent` stream may keep an archive, and a
  non-zero value on any other kind is **refused** (`400` naming `archiveDays`), because an
  archive length is a promise about retention rather than a leftover field. The form mirrors
  that by greying the field out and zeroing it in `toBody`, so it never builds a body the
  server would refuse.
- **A programme guide is two facts or none.** Exactly one of `epgSourceId`/`epgChannelKey` is
  refused with `validation_failed` naming **both** fields; both set or both empty are the only
  accepted answers. `hasEpg` on the read side is derived from the pair, never stored.
- **`status` is a field of both writes.** An operator chooses it on the add form (which starts
  at `Running`) and can enable or disable an existing stream on the edit form; the create
  endpoint keeps `Stopped` only as the answer for a body that names no status, and `PATCH`
  treats an absent value as "keep", which is what lets the bulk service patch other fields
  without reviving or halting a stream. `start`/`stop`/`restart` remain the three table
  transitions that write the same row fact. `activeSource` is never written by the service at
  all: the relaying process reports it and nothing else.
- **A name is unique within its category** (`UNIQUE(category_id, name)`), compared exactly and
  case sensitively like username and email. A lost race is caught and mapped to
  `409 video_stream_already_exists` naming `name`.
- **Both lists are replaced wholesale, never patched.** Absent keeps, empty clears, a list
  replaces in the order sent — which is what makes the order on the operator's screen the
  order the relay plays in, with no position arithmetic to get wrong. An empty `sources` is
  still refused for a URL kind ("add at least one source address"), matching the form's own
  whole-list rule.
- **No captcha, no attempt window** — the recorded admin-area departure: every write is behind
  an `AdminOnly` session plus the `EditStreams` permission and a fresh antiforgery pair, and
  `RejectsCsrfAsync()` runs before any body is read.
- **There is no stream position, in the schema or in a write.** `video_streams.sort_order`
  was dropped by `20261023121109_DropVideoStreamSortOrder`; the order a stream is shown in
  belongs to the list that shows it, and when bouquets arrive it belongs to the membership
  rather than to the stream row. A create therefore appends nothing, a category change moves
  nothing, and a playlist's order is not a display order — the list is ordered by the query.
  The migration drops `IX_video_streams_category_id_sort_order` with the column; the unique
  `IX_video_streams_category_id_name` already answers the category filter, so no replacement
  index is needed. `Down` recreates the column at `0` and the index, but cannot restore the
  positions that were there.
- **The list is paged** (`total` counted before paging, the same "the pager must not lie" rule
  the accounts and session histories follow), `search` is a case-insensitive substring of the
  name, and the sort fields are the column keys — `name`, `category`, `status` and `type` —
  parsed from plain text case-insensitively. `pageSize` defaults to 20 and is
  refused above **1000**, the same ceiling as every other paged table; that ceiling is also
  what lets the table's "select everything" checkbox hand one whole page to the bulk endpoint.

### Bulk operations over the streams

`POST /admin/video-streams/bulk` applies one of nine operations to the streams one page
holds: `start`, `stop`, `restart`, `delete`, `move-category`, `change-type`, `change-delay`,
`change-archive` and `change-restream`. It has a controller of its own
(`VideoStreamsBulkController`) because the request names a set the table assembled rather
than one addressed row and the answer is a per-identifier report; the policy is the same
`EditStreamsPolicy` the single-row writes carry.

- **A well-formed run answers `200 video_streams_bulk_completed`**, with `succeeded`,
  `failed` and one `items` entry per identifier. Each entry carries the very code the
  single-row endpoint would answer (`video_stream_started`, `video_stream_not_found`, …),
  `ok` read off the status that code maps to, and `errors` keyed by camelCase field name.
  A category move that collides with a name already used there refuses **that row alone**
  with `video_stream_already_exists`; the others still move. Partial success is the shape.
- **The body is validated before any row is read.** The action is matched
  case-insensitively and with hyphens (`Start`, `change-type`); identifiers must be 1..1000
  uuids and duplicates collapse; the value an action needs must be present and usable. A
  body that breaks any of those answers `400 validation_failed` naming every broken field at
  once. `RejectsCsrfAsync()` runs **before** the body is parsed, so a stale pair is
  `csrf_invalid` rather than a validation answer.
- **Every operation delegates to the single-row service.** Start, stop, restart and delete
  are the same writes the row buttons make; a move is `UpdateAsync` with only a
  `categoryId`. That is why the category-scoped name collision, an unusable category and a
  missing stream are all answered per row rather than aborting the run.
- **Three collective rules live in the bulk service.** A delay or an archive may only be
  written to `Permanent` streams, restream settings only to `Permanent`/`OnDemand`, and a
  bulk type change may only choose `Permanent`, `OnDemand` or `Redirect`. Any other kind
  answers `validation_failed` on that row (`delayMinutes`, `archiveDays`,
  `restreamUserAgent`) while the rest of the selection continues.
- **A bulk type change clears what the destination cannot carry instead of refusing the
  row.** Moving to `OnDemand`/`Redirect` clears a non-zero delay and archive; moving to
  `Redirect` also clears all four restream settings. The row still lands as
  `video_stream_updated`, and each clearance is a warning: `video_stream_delay_cleared`,
  `video_stream_archive_cleared`, `video_stream_restream_cleared`, each `{ code, message }`.
  A successful change that cleared nothing answers `warnings: null`; a refused row never
  carries a warning, because nothing was written.
- **Restream is a whole-value replacement, not a patch of one field.** All four settings are
  written on every run and a blank field clears its setting. Zero is a value for delay and
  archive, not an absence.
- **Restart is also a single-row action** (`POST /admin/video-streams/{id}/restart`): it
  writes `Running` and answers `video_stream_restarted`. It is not a separate status and it
  deliberately leaves `activeSource` alone, because that is the relaying process's report.
- **No schema change.** A bulk operation is the existing writes applied in a loop — no table,
  no queue, no background worker and no migration.

### The playlist import: `POST /admin/video-streams/import`

The other way to add streams: one M3U file in, a category's worth of rows out. It has a
controller of its own (`VideoStreamsImportController`) because the request is a file rather
than a JSON row and the answer counts a batch rather than describing one stream — the same
seam that gave the bulk endpoint its own controller.

- **The form is the add form without the two fields the file supplies.** `multipart/form-data`
  carries `playlist`, `type`, `status`, `categoryId`, `delayMinutes`, `archiveDays` and the
  four restream fields; there is no `name` and no `sources`, because each entry's title names
  its stream and each entry's address is its one source. `type` is limited to `Permanent`,
  `OnDemand` and `Redirect` (the three kinds that relay from a list of addresses); an absent
  `status` means `Stopped`, so a batch cannot start relaying by omission. `archiveDays` above
  zero is still refused on a non-`Permanent` kind, the same rule the add form follows.
- **`RejectsCsrfAsync()` runs before the form is read**, and the content type is checked
  before the form reader can throw its own exception. A `Content-Length` greater than the file
  limit plus a megabyte of multipart overhead is `413 video_stream_import_too_large` before
  anything is buffered; the file length itself is checked again after the form is parsed. A
  body that is not a form at all is `415 unsupported_media_type`; a URL-encoded body carries
  no file and is refused as a validation error.
- **The parser reads one `#EXTINF` and the absolute URL after it as one entry.** The name is
  the title after the first unquoted comma, then `tvg-name`, then `Stream N`; a URL with no
  directive still becomes an entry under the fallback name. BOM and CRLF are tolerated,
  unknown directives are ignored, and quoted attributes may contain commas. What it refuses,
  it refuses one line at a time: a dangling directive or an address that is not absolute
  becomes a problem on its line and the rest of the file is still read. An HLS playlist is
  refused whole — its `#EXT-X-` tags name variants of one stream, not separate channels.
- **The batch is partial success by design.** `imported + skipped + failed == total`, and
  every entry that did not land is in `problems` with its line, name, code and field errors.
  A name already used in the category or repeated within the file is **skipped**
  (`video_stream_already_exists`); an unusable title, source or parser line is **failed**
  (`validation_failed`). Existing streams are never updated: the only names the service reads
  are the ones it must refuse, and every surviving entry becomes a new row.
- **One save writes the batch.** The service stages every row through `AddRangeAsync` and
  calls `TrySaveAsync` once. A name another request took between the check and the save is
  `409 video_stream_import_conflict` and **nothing** from the batch is written; the check
  matches `IX_video_streams_category_id_name` by name, so any other unique violation is
  rethrown rather than announced as a taken name. No migration: the import writes the same
  three tables the single create writes.
- **Refusals are one code each**: `video_stream_import_empty`, `..._hls`,
  `..._too_many_entries` and `..._unreadable` are `400`, the size refusal is `413`, and a
  save-time name race is `409`; every other field problem is `400 validation_failed` with the
  field named. The file limit is 10 MiB and one import accepts 5000 entries.

The parser is covered by twelve unit tests; the endpoint by four integration tests — a mixed
playlist (one imported, two skipped, one failed) with its counts and stored rows, an empty
playlist and an HLS one refused whole, a missing file and a type the import cannot use, and a
request without the antiforgery pair refused before the file is read.

Layering follows the usual split: `VideoStream` in `Fluxy.Core/Streams/Models/`,
`VideoStreamPage` / `VideoStreamListItem` in `Fluxy.Core/Streams/ReadModels/`,
`NewVideoStream` / `VideoStreamPatch` / `VideoStreamWrite` / `BulkVideoStreamOperation` /
`M3uPlaylistEntry` / `NewVideoStreamImport` in `Fluxy.Core/Streams/Commands/`,
`VideoStreamOutcome` / `BulkVideoStreamOutcome` / `BulkVideoStreamItemResult` /
`M3uPlaylistOutcome` / `M3uPlaylistProblem` / `VideoStreamImportOutcome` /
`VideoStreamImportProblem` in `Fluxy.Core/Streams/Outcomes/`, `VideoStreamAction` /
`BulkVideoStreamAction` / `VideoStreamBulkWarning` / `VideoStreamSortField` /
`VideoStreamSortOrder` / `VideoStreamStatus` / `VideoStreamType` / `M3uPlaylistStatus` /
`VideoStreamImportStatus` in `Fluxy.Core/Streams/Enums/`, `VideoStreamLimits` /
`VideoStreamListLimits` / `VideoStreamBulkLimits` / `VideoStreamEpg` / `M3uPlaylistParser` /
`VideoStreamImportLimits` in `Fluxy.Core/Streams/Utils/`, plus `IVideoStreamService` /
`IVideoStreamBulkService` / `IVideoStreamImportService` in
`Fluxy.Core/Streams/Interfaces/`, `VideoStreamService` / `VideoStreamBulkService` /
`VideoStreamImportService` in `Fluxy.Application/Streams/Services/` and the
`VideoStreamRules` helper in `Fluxy.Application/Streams/Utils/`, the three entities and their
configurations in
`Fluxy.DataAccess/` (migrations `20261014125324_AddVideoStreams` and
`20261023121109_DropVideoStreamSortOrder`), the contracts in
`Fluxy.API/Contracts/Streams/` plus `BulkItemWarningResponse` in
`Fluxy.API/Contracts/Common/`, `VideoStreamsController`, `VideoStreamsBulkController` and
`VideoStreamsImportController` in `Fluxy.API/Controllers/Content/`.

### What running caught: a PATCH that failed on any stream with a list

`dotnet build` was clean and every rule above read correctly. Against a running instance, a
`PATCH` that renamed a stream — or changed anything at all — answered
`409 video_stream_already_exists` on a table holding exactly one row, under a name nobody else
held. Two separate mistakes were stacked, and each one hid the other:

- **`UpdateAsync` loaded the stream without the two collections.** `ReplaceSources`/
  `ReplaceFiles` decide what to delete by comparing the wanted list against what the row
  already holds — and an unloaded `ICollection` navigation reads as **empty**, never as null.
  So the comparison always differed, `RemoveRange` removed nothing, and the fresh rows were
  inserted straight over the ones still in the table, colliding on
  `IX_video_stream_sources_stream_id_position` (SQLSTATE 23505). Fixed with
  `.Include(entry => entry.StreamSources).Include(entry => entry.StreamFiles)` on that load.
  **The read path deliberately does not do this** — `GetAsync` and the page read the two lists
  as separate `AsNoTracking` queries, because a page of twenty streams would otherwise be two
  more collections per row for data the table never draws.
- **Every unique violation was reported as a name conflict.** `SaveAsync` returns the
  `DbUpdateException` for *any* 23505, and both `CreateAsync` and `UpdateAsync` answered it
  with `AlreadyExists(Name)`. Three unique indexes exist here and only one is about the name —
  the other two are the position columns of the two lists — so a bug in the list writer was
  announced as "that stream name is already taken", which sent the search to the uniqueness
  rule instead of to the write. Both now ask `IsNameIndexViolation`, which matches the
  constraint name `IX_video_streams_category_id_name` inside the message; anything else is
  **rethrown** rather than dressed up, so a future bug surfaces as the 500 it is instead of a
  plausible 409.

Both are worth remembering as one rule: a `DbUpdateException` carries *which* constraint
refused, and mapping all of them to the one outcome the code knows how to explain turns every
future bug into a confidently wrong answer.

Verified against a running instance: create → `201` with an honest `Location` and both lists
stored in the order sent; a reorder → `200` and the next read answers the new order; a shorter
list → wholesale replacement with no orphans; an empty list → `400` with the stored list
intact; a same-name `PATCH` → `200` (the exclusion that lets a rename keep its own name); a
rename to a free name → `200`; `start` twice → `200` both times; `archiveDays` on a
non-Permanent kind → `400` naming it; a half guide → `400` naming both fields; `delete` →
`200`, `delete` again → `404`.

## The audio streams API: `admin/audio-streams`

The second medium's streams, built as a **separate resource** rather than as a third kind of
the video one. The route is `admin/audio-streams`, the tables are `audio_streams` and
`audio_stream_sources`, and the permissions are `ViewAudioStreams` / `EditAudioStreams` —
deliberately **not** the video pair. A group that may read the video streams may not read the
audio ones, and the table's `editAudioStreams` gate is the only thing standing between "may
watch" and "may change".

| Endpoint | Auth | Answer |
| --- | --- | --- |
| `GET /admin/audio-streams` | `Permission.ViewAudioStreams` | page: `page`, `pageSize`, `search`, `categoryId`, `status`, `type`, `sortBy`, `sortOrder` |
| `GET /admin/audio-streams/{id}` | `Permission.ViewAudioStreams` | one stream with its `sources`; `404 audio_stream_not_found` |
| `POST /admin/audio-streams` | CSRF + `Permission.EditAudioStreams` | `201 audio_stream_created` + relative `Location` |
| `PATCH /admin/audio-streams/{id}` | CSRF + `Permission.EditAudioStreams` | `200 audio_stream_updated` |
| `POST /admin/audio-streams/{id}/start` / `…/stop` / `…/restart` | CSRF + `Permission.EditAudioStreams` | `audio_stream_started` / `audio_stream_stopped` / `audio_stream_restarted` |
| `POST /admin/audio-streams/bulk` | CSRF + `Permission.EditAudioStreams` | `200 audio_streams_bulk_completed`, one entry per identifier |
| `POST /admin/audio-streams/import` | CSRF + `Permission.EditAudioStreams` | `200 audio_streams_import_completed`, counts and per-line problems |
| `DELETE /admin/audio-streams/{id}` | CSRF + `Permission.EditAudioStreams` | `200 audio_stream_deleted`, `404` on a second |

### Two kinds, and the payload they share

`AudioStreamType` is **exactly two members**: `OnDemand = 1` and `Redirect = 2`. Both are
**URL kinds** — both relay from the same ordered `sources` list, so the type decides *how* the
stream runs and never *what* it carries. That single fact is what makes the audio resource
simpler than the video one, and every difference below follows from it:

- **There is no kind whose payload another kind can switch off.** So there is no delay, no
  archive, no VOD id, no series id and no EPG pair — and therefore **no bulk warning
  vocabulary** either. A video bulk run may answer `video_stream_delay_cleared` or
  `video_stream_archive_cleared` beside its refusals; an audio bulk answer carries **no
  warnings at all**, and the frontend's `useBulkAction` is given only a `partialTitleKey`.
- **A bulk type change may choose either kind** and clears nothing. The video menu had to
  leave three kinds out because their payload could not be invented; the audio menu offers
  both.
- **The source list is replaced wholesale, in the order sent**, exactly as the video one is —
  absent keeps, empty clears, a list replaces. An empty list is **refused** for a URL kind
  ("add at least one source address"), because a stream with no source could never play.
- **`LogoUrl`, `Enigma2Sid` and all four `Restream*` fields are kept**, and both kinds carry
  them, so nothing on the form is ever greyed out for a kind that cannot hold it.

`AudioStreamStatus` is `Stopped = 0` / `Running = 1`, and like `UserStatus` it is **not
ordered** — a check is for the exact member, never a comparison.

### What else is the same, and what is not

- **A name is unique within its category**, compared exactly and case sensitively like
  username and email: `UNIQUE(category_id, name)`, index `IX_audio_streams_category_id_name`.
  The lost race is caught and mapped to `409 audio_stream_already_exists` naming `name` —
  and, as with the video streams, **only that exact constraint name** counts. A violation of
  `IX_audio_stream_sources_stream_id_position` is a bug in the list writer and is rethrown,
  never dressed up as a taken name.
- **The update loads the sources collection** before replacing it
  (`.Include(entry => entry.StreamSources)`), for the reason the video `UpdateAsync` had to be
  fixed for: an unloaded `ICollection` navigation reads as **empty**, so the comparison always
  differed, nothing was removed and the fresh rows collided with the ones still in the table.
- **The category must exist and must be `ContentType.AudioStream`.** A category of another
  kind is refused — the audio streams are filed under audio categories, and the same table
  holds video categories.
- **`activeSource` is read-only.** No service writes it; the relaying process reports it.
- **The list is paged** (`total` counted before paging, the same "the pager must not lie"
  rule), `search` is a case-insensitive substring of the name, `pageSize` defaults to 20 and
  is refused above **1000**, and the search string is refused above **200**. The sort fields
  are `name`, `category`, `status` and `type`, parsed from plain text case-insensitively.
- **There is no `sort_order` column.** A stream has no position of its own — the order a
  stream is shown in belongs to the list that shows it, and a playlist's order is the order
  the relay plays in, not a display order.
- **No captcha and no attempt window** — the recorded admin-area departure: every write is
  behind an admin session, the `EditAudioStreams` permission and a fresh antiforgery pair,
  and `RejectsCsrfAsync()` runs before any body is read.

### The bulk menu: seven operations, not nine

`BulkAudioStreamAction` is `Start`, `Stop`, `Restart`, `Delete`, `MoveCategory`, `ChangeType`
and `ChangeRestream`. The video endpoint's five extra operations (`change-delay`,
`change-archive`, and the file-list and series/vod edits) have no audio counterpart, because
an audio stream has no such field to write. The restream operation replaces all four
settings as a group — a blank value clears its setting — which is the same whole-list rule the
single-row form follows, in the same request shape.

The body is validated **before any row is read**: the action is matched case-insensitively and
with hyphens, the identifiers must be 1..1000 uuids and duplicates collapse, and the value an
action needs must be present and usable. A body that breaks any of those answers
`400 validation_failed` naming every broken field at once. `RejectsCsrfAsync()` runs **before**
the body is parsed, so a stale pair is `csrf_invalid` rather than a validation answer.

A well-formed run answers `200 audio_streams_bulk_completed` with `succeeded`, `failed` and one
`items` entry per identifier; each entry carries the very code the single-row endpoint would
answer, `ok` read off the status that code maps to, and `errors` keyed by camelCase field
name. A category move that collides with a name already used there refuses **that row alone**
with `audio_stream_already_exists`; the others still move. Partial success is the shape.

### The playlist import

`POST /admin/audio-streams/import` is a multipart form with the file in `playlist` and the
settings beside it: `type`, `status`, `categoryId`, the four `restream*` fields,
`appendSourceToExisting` and `overwriteExistingSettings`. **The parser is shared with the
video import** (`M3uPlaylistParser`) and the entry limit is passed to it explicitly
(`AudioStreamImportLimits.MaxEntries` = 5000) rather than relying on its default; the file
limit is 10 MiB.

- **The kind offers only `OnDemand` and `Redirect`** — both relay from a list of addresses,
  which is exactly what a playlist supplies, so there is no kind the service would refuse.
- **Status starts at `Stopped`**, the endpoint's own default and the safe state for a batch of
  thousands.
- **There is no delay and no archive** on the form, for the same reason the kind menu is
  short.
- **The batch is partial success by design**: `imported + skipped + failed == total`, and
  every entry that did not land is in `problems` with its line, name, code and field errors. A
  name already used in the category or repeated within the file is **skipped**
  (`audio_stream_already_exists`); an unusable title, source or parser line is **failed**
  (`validation_failed`). Existing streams are never updated unless the operator opts in.
- **One save writes the batch.** The service stages every row through `AddRangeAsync` and calls
  `TrySaveAsync` once. A name another request took between the check and the save is
  `409 audio_stream_import_conflict` and **nothing** from the batch is written; the check
  matches `IX_audio_streams_category_id_name` by name, so any other unique violation is
  rethrown.

### The two permissions, and what they do not migrate

`UserPermission` grew `ViewAudioStreams = 4096` and `EditAudioStreams = 8192` — the two free
values above the stream pair at 1024 and 2048. **Adding a permission needs no migration**: the
catalog is code, and the base groups are reconciled with it at startup. The Administrators row
gains the two grants on the next boot; **an existing custom administrator group has to be
granted them by hand**, because the seeder only reconciles the three base groups. The values
`1` and `2` remain retired and unused.

`ContentCategoryRepository.CountStreamsAsync` now counts **both** media: the question the
caller is asking is "may this category go?", and a category holding an audio stream is as
much in use as one holding a video stream.

### Layering

`AudioStream` in `Fluxy.Core/Streams/Models/`, `AudioStreamListItem` / `AudioStreamPage` in
`ReadModels/`, `NewAudioStream` / `AudioStreamPatch` / `AudioStreamWrite` /
`BulkAudioStreamOperation` / `NewAudioStreamImport` / `AudioStreamBatchUpdate` in `Commands/`,
`AudioStreamType` / `AudioStreamStatus` / `AudioStreamAction` / `AudioStreamSortField` /
`AudioStreamSortOrder` / `BulkAudioStreamAction` / `AudioStreamImportStatus` in `Enums/`,
`AudioStreamOutcome` / `BulkAudioStreamItemResult` / `BulkAudioStreamOutcome` /
`AudioStreamImportOutcome` / `AudioStreamImportProblem` in `Outcomes/`, the four limits
classes in `Utils/`, and `IAudioStreamService` / `IAudioStreamBulkService` /
`IAudioStreamImportService` / `IAudioStreamRepository` in `Interfaces/` and `Repositories/`.
`AudioStreamService`, `AudioStreamBulkService` and `AudioStreamImportService` live in
`Fluxy.Application/Streams/Services/` beside the video ones, the two entities and their
configurations in `Fluxy.DataAccess/` (migration `20261023172343_AddAudioStreams`), the
contracts in `Fluxy.API/Contracts/Streams/`, and the three controllers in
`Fluxy.API/Controllers/Content/`.

**`AudioStreamOutcome` lives in `Outcomes/`, not in `Enums/`** — the one place the two media
disagree about where a thing sits. The video outcome is an enum in `Enums/`; the audio one is
a record carrying the action, the row identifier and the field errors, because the audio
service answers a refusal with the field names the form draws beside the inputs. Follow the
folder the type actually belongs to rather than the video one's precedent.

**`AudioStreamTextParser`** lives in `Fluxy.API/Controllers/Support/` beside
`VideoStreamTextParser`: the transport parses the form fields it can parse and refuses a value
that names no member, while whether the category is an audio category and whether each line can
become a stream are rules of the service.

Verified: `dotnet build` clean, **210** unit tests and **116** integration tests green, and
`dotnet ef migrations has-pending-model-changes` reports no changes.

## The video-on-demand API: `admin/video-on-demand`

The on-demand catalogue, built as its own domain (`Fluxy.Core/Vod`, `Fluxy.Application/Vod`)
rather than as a third kind of stream. A video on demand has one address and a body of
metadata a live stream has no place for, and its screen answers a different question: not
"what is broadcasting" but "what is published, and can I play or convert it".

| Endpoint | Auth | Answer |
| --- | --- | --- |
| `GET /admin/video-on-demand` | `Permission.ViewVideoOnDemand` | page: `page`, `pageSize`, `search`, `categoryId`, `status`, `type`, `sortBy`, `sortOrder` |
| `GET /admin/video-on-demand/{id}` | View | one video with its media and metadata; `404 video_on_demand_not_found` |
| `POST /admin/video-on-demand` | CSRF + `Permission.EditVideoOnDemand` | `201 video_on_demand_created` + relative `Location` |
| `PATCH /admin/video-on-demand/{id}` | CSRF + Edit | `200 video_on_demand_updated` |
| `DELETE /admin/video-on-demand/{id}` | CSRF + Edit | `200 video_on_demand_deleted`, `404` on a second |
| `POST /admin/video-on-demand/bulk` | CSRF + Edit | `200 video_on_demand_bulk_completed`, one entry per identifier |
| `POST /admin/video-on-demand/import` | CSRF + Edit | `200 video_on_demand_import_completed`, counts and per-line problems |
| `GET /admin/video-on-demand/{id}/play` | View | `302` for a redirect, the bytes for a relay |
| `GET /admin/video-on-demand/transcode-formats` | View | `{ available, formats }` |
| `POST /admin/video-on-demand/{id}/transcodes` | CSRF + Edit | `503 transcode_not_configured` in this pass |

- **`VodType` is two answers to "who carries the bytes".** `Relay` is served by this
  installation: a local path through `PhysicalFile` with range processing, or an absolute
  `http(s)` address streamed through the API with the `Range` header forwarded. `Redirect`
  answers `302` to the source and lets the browser fetch it. A redirect whose source is a
  local path, and a relay whose source is neither a URL nor a readable file, answer
  `409 video_on_demand_not_playable` rather than inventing a destination. The list response
  deliberately omits the source: the play button uses the endpoint, so a page of twenty rows
  does not carry twenty long paths.
- **The status is a row fact, not a process fact.** `Active`/`Inactive` controls what clients
  are offered; the play endpoint serves an inactive item too, because an operator previewing
  a file before publishing it is exactly who it serves. There is no start/stop action.
- **The metadata is a body of optional fields**, not a second resource: `imdbId`,
  `posterUrl`, `backdropUrl`, `plot`, `cast`, `director`, `genres`, `releaseDate`, `rating`
  and `countryCode`. `cast` and `genres` are PostgreSQL `text[]` columns because they are read
  with the row and never queried by position; a child table would buy a join and the
  "unloaded collection reads as empty" bug for an order the array already keeps. The date is
  a `date` (no time of day); the rating is `numeric(3,1)` validated to 0–10 with one decimal;
  the country is an upper-case ISO alpha-2, validated against the same runtime regions the
  login guard uses (the catalog now lives in `Core/GeoIp/Utils/CountryCodes.cs`).
- **A name is unique within its category** (`UNIQUE(category_id, name)`, index
  `IX_vod_items_category_id_name`), compared case sensitively like every other name here. A
  lost race is mapped to `409 video_on_demand_already_exists` only when that exact index was
  the one refused; any other unique violation is rethrown.
- **PATCH keeps and clears without a second field.** An absent text property keeps, an empty
  string clears; `cast`/`genres` follow the list rule (absent keeps, `[]` clears, a list
  replaces). `releaseDate` and `rating` travel as **text** on PATCH so that "keep" and
  "clear" are distinguishable; the create endpoint takes the date as text and the rating as a
  number. The form converts the rating to text only on the update call.
- **The list is paged, filtered and sorted server side**, for the same `total` reason every
  other table here is. Sort fields: `name`, `category`, `status`, `type`, `releaseDate`,
  `rating`; the two nullable ones group their nulls last in both directions. The response
  includes the category name and omits the source and metadata; the detail endpoint carries
  the rest.
- **Bulk operations are the single-row writes applied in a loop**: `change-status`,
  `move-category`, `change-type` and `delete`. A move that collides with a name already used
  in the destination refuses that row alone and the others still move.
- **The import reuses the M3U reader the streams use** (`M3uPlaylistParser`, with the
  on-demand entry limit passed explicitly). Each entry becomes one video: the title names it,
  its address is the source, `tvg-logo` is the poster when it fits. A name already in the
  category or repeated in the file is **skipped**; a line that cannot become a video is
  **failed**; the counts add up to `total`. One save writes the batch, and a name taken
  between the check and the save is `409 video_on_demand_import_conflict` with nothing
  written.
- **Transcoding is a stub in this pass, and the stub does not lie.** No encoder is
  configured; `GET /transcode-formats` says `available: false` with the five names,
  and `POST /{id}/transcodes` answers `503 transcode_not_configured` without writing a job.
  The screen draws the action disabled with that reason. A queued job no worker would ever
  pick up would be a worse answer than the refusal.
- **No captcha and no attempt window**, for the recorded admin-area departure: every write is
  behind an admin session, the edit permission and a fresh antiforgery pair, and
  `RejectsCsrfAsync()` runs before any body is read.
- **The permissions are `ViewVideoOnDemand = 16384` and `EditVideoOnDemand = 32768`.** The
  write key is one above `smallint`, which is why `user_group_permissions.permission` was
  widened to `integer` by `WidenUserGroupPermission`; before that the base group stored a
  wrapped negative grant and every endpoint answered 403. The startup seeder grants the pair
  to the base Administrators group; **an existing custom administrator group has to be
  granted them by hand**, the same note the audio pair carries.
- **A category cannot be deleted while a video is filed under it.**
  `ContentCategoryRepository.CountContentAsync` counts video streams, audio streams and
  on-demand videos, and the refusal says "item(s)" rather than "stream(s)".
- **Migrations**: `AddVideoOnDemand` creates `vod_items` (the arrays, the date, the numeric
  rating, the FK to `content_categories` with `RESTRICT`, the unique name index and the
  filter/sort indexes). No permission migration: the catalog is code.
- **Verified by running**: `dotnet build` clean, **210** unit tests and **126** integration
  tests green (10 of them for this resource: CRUD/metadata, name scope, field refusals,
  list/filter/sort/paging, bulk partial success, mixed import, category in-use, the
  transcode stub, play redirect and unplayable source, CSRF and permission), and
  `dotnet ef migrations has-pending-model-changes` reports no changes.

Layering follows the usual split: `VodItem` in `Fluxy.Core/Vod/Models/`,
`VodListItem`/`VodPage`/`VodTranscodeAvailability` in `ReadModels/`, the commands
(`NewVodItem`, `VodItemPatch`, `VodItemWrite`, `BulkVodOperation`, `NewVodImport`), outcomes,
enums (`VodStatus`, `VodType`, `VodAction`, `BulkVodAction`, the sort pair, the import status
and the transcode pair), limits in `Utils/`, and `IVodService`/`IVodBulkService`/
`IVodImportService`/`IVodTranscodeService`/`IVodRepository` in `Interfaces/` and
`Repositories/`; `VodService`/`VodBulkService`/`VodImportService`/`VodTranscodeService` and
`VodRules` in `Fluxy.Application/Vod/`; the entity, configuration and repository in
`Fluxy.DataAccess`; the contracts in `Fluxy.API/Contracts/Vod/`, and
`VideoOnDemandController` / `VideoOnDemandBulkController` / `VideoOnDemandImportController` /
`VideoOnDemandTranscodesController` in `Fluxy.API/Controllers/Content/`.

## The series API: `admin/series`

A series is its own aggregate: a catalogue row with a body of metadata, plus the episodes
filed under it. It is not a third kind of stream and it does not reuse the on-demand table: a
series has many episodes, each with a season and a number, while an on-demand item has one
source. `ContentType.Series` already existed for the category side, so only the rows, the
services and the endpoints are new.

| Endpoint | Auth | Answer |
| --- | --- | --- |
| `GET /admin/series` | `Permission.ViewSeries` | page: `page`, `pageSize`, `search`, `categoryId`, `status`, `sortBy`, `sortOrder` |
| `GET /admin/series/{id}` | View | one series with `seasonsCount` and `episodesCount` |
| `POST /admin/series` | CSRF + `Permission.EditSeries` | `201 series_created` + relative `Location` |
| `PATCH /admin/series/{id}` | CSRF + Edit | `200 series_updated` |
| `DELETE /admin/series/{id}` | CSRF + Edit | `200 series_deleted`, episodes cascaded |
| `POST /admin/series/bulk` | CSRF + Edit | `200 series_bulk_completed`, one entry per id |
| `GET /admin/series/{id}/episodes` | View | page: `page`, `pageSize`, `search`, `seasonNumber`, `status`, `type`, `sortBy`, `sortOrder` |
| `GET /admin/series/{id}/episodes/{episodeId}` | View | one episode in full |
| `POST /admin/series/{id}/episodes` | CSRF + Edit | `201 series_episode_created` |
| `PATCH /admin/series/{id}/episodes/{episodeId}` | CSRF + Edit | `200 series_episode_updated` |
| `DELETE /admin/series/{id}/episodes/{episodeId}` | CSRF + Edit | `200 series_episode_deleted` |
| `POST /admin/series/{id}/episodes/bulk` | CSRF + Edit | `200 series_episodes_bulk_completed` |
| `POST /admin/series/{id}/seasons` | CSRF + Edit | `201 series_season_created` with `addedCount` |
| `GET /admin/series/transcode-formats` | View | `{ available: false, formats }` |
| `GET`/`POST /admin/series/{id}/episodes/{episodeId}/transcodes` | View / Edit | availability / `503 transcode_not_configured` |

- **`SeasonsCount` is `MAX(season_number)`, not a stored column and not the number of distinct
  seasons.** `EpisodesCount` is `COUNT(*)`. Both are projected in the page and the detail query,
  so deleting the last episode of the highest season lowers the count without a second write.
- **A season is not a table.** It is the set of episodes carrying one `season_number`. The
  add-season form reads the top level of a folder, keeps the video extensions in
  `SeriesLimits.EpisodeFileExtensions`, orders the files naturally (`Episode 2` before
  `Episode 10`) and creates numbers `1..N` in one `AddRange` and one `SaveChangesAsync`. An
  existing season is refused with `409 series_season_already_exists`; adding to it is the
  single-episode form's job. A relative folder is resolved against the content root, like a
  stored on-demand file.
- **The generated name is the series name plus the selected tags**: `<Series> S01E02`,
  `<Series> S01`, `<Series> E02`, or `<Series>` when neither checkbox is on. It is cut to 200
  characters by shortening the base, never the tag.
- **A name is unique within its category** (`IX_series_category_id_name`); a season-and-number
  pair is unique inside a series (`IX_series_episodes_series_id_season_number_episode_number`).
  A violation from either exact index is the feature's `already_exists`; any other unique
  violation is rethrown.
- **Bulk operations are the single-row writes in a loop**: series `change-status`,
  `move-category`, `delete`; episodes `change-status`, `change-type`, `delete`. Partial success
  is the shape, the same as every other bulk endpoint here.
- **The transcode surface reuses `VodTranscodeFormat` and `VodTranscodeAction`.** The five
  containers are one list for both resources, and `SeriesTextParser.ParseFormatOrNull` delegates
  to `VodTextParser`. No encoder is configured, so availability is `false` and a request is a
  `503` that writes nothing.
- **The permissions are `ViewSeries = 65536` and `EditSeries = 131072`.** The startup seeder
  grants them to the base Administrators group; an existing custom administrator group has to be
  granted them by hand. The series count was added to
  `ContentCategoryRepository.CountContentAsync`, so deleting a category with a series is
  `409 content_category_in_use`.
- **`VideoStream.SeriesId` stays an opaque `uuid`.** Existing stream rows may name a series that
  was never recorded, and the delivery process resolves the reference rather than a foreign key.

Layering follows the usual split: `SeriesItem` / `Episode` in `Fluxy.Core/Series/Models/`, the
read models, commands, outcomes, enums and limits in their folders, `ISeriesService` /
`ISeriesBulkService` / `IEpisodeService` / `IEpisodeBulkService` / `ISeasonService` /
`IEpisodeTranscodeService` and the two repositories in `Interfaces/` and `Repositories/`; the
services, `SeriesRules`, `EpisodeRules` and `EpisodeFolderScanner` in `Fluxy.Application/Series/`;
the entities, configurations and repositories in `Fluxy.DataAccess` (migration `AddSeries`); the
contracts in `Fluxy.API/Contracts/Series/`; and the six controllers in
`Fluxy.API/Controllers/Content/Series*.cs`.

## The programme guide API: `admin/epg-sources`

The first piece of the `Epg` domain: the sources a schedule is fetched from, and the table that
shows them. The guide **updater does not exist yet** — nothing in this pass fetches a file,
parses it or writes a programme — so the domain is modelled, the source is managed, and the two
columns the updater owns are stored but never written by an endpoint.

| Endpoint | Auth | Answer |
| --- | --- | --- |
| `GET /admin/epg-sources` | `ViewEpgSourcesPolicy` | page: `page`, `pageSize`, `search`, `sortBy`, `sortOrder` |
| `GET /admin/epg-sources/{id}` | View | one source; `404 epg_source_not_found` |
| `GET /admin/epg-sources/{id}/channels` | View | page of the streams that use the source; `404` on an unknown id |
| `POST /admin/epg-sources` | CSRF + `EditEpgSourcesPolicy` | `201 epg_source_created` + relative `Location` |
| `PATCH /admin/epg-sources/{id}` | CSRF + Edit | `200 epg_source_updated` |
| `DELETE /admin/epg-sources/{id}` | CSRF + Edit | `200 epg_source_deleted`, `409 epg_source_in_use` |

- **A source is three facts an operator owns plus two the updater owns.**
  `epg_sources`: `id, name varchar(200) UNIQUE, url varchar(2048), days_keep integer,
  last_epg_updated_at timestamptz NULL, raw_data text NULL, created_at, updated_at`. `name` is
  unique across the installation (case sensitive like every other name here); `url` must be an
  absolute http(s) address; `days_keep` is 1..365 and defaults to **7 in the service** — no
  database default, per the project rule. `last_epg_updated_at` and `raw_data` are returned by
  no response: the raw guide can be megabytes and only the updater parses it, so every read
  projects it away.
- **`epg_data` is modelled now and written later.** `id, source_id FK → epg_sources ON DELETE
  CASCADE, channel_id varchar(200), title varchar(500), language varchar(35) NULL,
  start_date timestamptz, end_date timestamptz, description text, created_at, updated_at`,
  with the five indexes the domain asks for (`source_id`, `channel_id`, `language`,
  `start_date`, `end_date`). There is no repository, service or endpoint for it in this pass:
  rows are created and deleted by the updater alone, and a contract nobody calls would be a
  second place to keep in step with it. `EpgData` in `Fluxy.Core/Epg/Models/` is the shape that
  updater will write.
- **`channel_id` is the guide's own key, not a row of this database.** A stream names a source
  plus `epg_channel_key`, and `GET .../channels` is the join that answers "which streams read
  their schedule from here" - a derived, read-only projection of `video_streams`, not a second
  list that could disagree with it.
- **`video_streams.epg_source_id` is now a real foreign key (`RESTRICT`).** The other two
  cross-domain references (`series_id`, `vod_id`) stay opaque, because a stream may name a row
  that was never recorded; a guide source, by contrast, is fetched from this installation, so a
  dangling one is a schedule nothing can ever look up. `VideoStreamService` therefore checks
  `IEpgSourceRepository.ExistsAsync` on create and on patch and refuses with
  `validation_failed` on `epgSourceId` before the write. Deleting a source a stream still uses is
  `409 epg_source_in_use` (with the count in the server sentence); the service checks first so
  the answer is a code rather than a database failure, and `ON DELETE RESTRICT` is the backstop.
- **The migration `AddEpgSources` is hand-edited in one place.** It creates both tables, then
  clears dangling pairs before adding the foreign key:
  `UPDATE video_streams SET epg_source_id = NULL, epg_channel_key = NULL WHERE epg_source_id IS
  NOT NULL AND NOT EXISTS (…)`. Both halves are cleared because the pair rule says a guide is a
  source *and* a key — a key left behind would be a name nothing can look up. The index
  `IX_video_streams_epg_source_id` is EF's own, created for the foreign key.
- **"Reload EPG" is a UI stub and has no endpoint.** The owner asked for the button now and the
  updater later, so the toolbar draws it disabled with the reason attached (the `hinted`
  wrapper), the same "tell the truth before the click" rule the streams' play button follows.
  A stub endpoint answering 503 was considered and rejected: an action that would do nothing is
  better withdrawn than answered.
- **No captcha, no attempt window** — the recorded admin-area departure: every write is behind
  an `AdminOnly` session, the `EditEpgSources` permission and a fresh antiforgery pair, and
  `RejectsCsrfAsync()` runs before any body is read.
- **The table has no selection column and no bulk menu**, on the owner's call. It is paged,
  searched and sorted server side for the usual `total` reason; `search` covers the name and
  the address, and the sort fields are `name`, `url`, `daysKeep`, `lastEpgUpdatedAt` (the last
  grouping its nulls last in both directions, so "never refreshed" never opens the page).
- **The channel list is paged and read-only**: sort fields `streamName` and `channelKey`,
  `search` over both, `404 epg_source_not_found` on an unknown source — and an empty page is a
  different answer from a missing source.

Layering follows the usual split: `EpgSource` / `EpgData` in `Fluxy.Core/Epg/Models/`,
`EpgSourceView` / `EpgSourcePage` / `EpgSourceChannel` / `EpgSourceChannelPage` in
`ReadModels/`, `NewEpgSource` / `EpgSourcePatch` / `EpgSourceWrite` in `Commands/`,
`EpgSourceOutcome` in `Outcomes/`, the action/sort enums in `Enums/`, `EpgSourceLimits` /
`EpgSourceListLimits` / `EpgDataLimits` in `Utils/`, `IEpgSourceService` and
`IEpgSourceRepository`; `EpgSourceService` and `EpgSourceRules` in `Fluxy.Application/Epg/`;
the two entities, their configurations and `EpgSourceRepository` in `Fluxy.DataAccess`
(migration `AddEpgSources`); the contracts in `Fluxy.API/Contracts/Epg/`; and
`EpgSourcesController` in `Fluxy.API/Controllers/Content/`.

Verified: `dotnet build` clean, **232** unit tests and **143** integration tests green,
`dotnet ef migrations has-pending-model-changes` reports no changes, and the frontend passes
`typecheck`, `lint`, `test`, `build`, `probe:render`, `probe:i18n` and `probe:paths`.

## The subscriptions API: `admin/subscriptions`

The subscription is the thing an operator issues to a user: its owner, its services (one playlist,
one MAG, one Enigma2 device), its access parameters, its bouquets and its expiry. It is the second
piece of the `Subscriptions` domain and consumes the tariff plan that piece defines.

| Endpoint | Auth | Answer |
| --- | --- | --- |
| `GET /admin/subscriptions` | `Permission.ViewSubscriptions` | page: `page`, `pageSize`, `search`, `ownerId`, `creatorId`, `status`, `sortBy`, `sortOrder` |
| `GET /admin/subscriptions/{id}` | View | one subscription with derived values, whitelists and bouquets; `404 subscription_not_found` |
| `POST /admin/subscriptions` | CSRF + `Permission.EditSubscriptions` | `201 subscription_created` + relative `Location` |
| `PATCH /admin/subscriptions/{id}` | CSRF + Edit | `200 subscription_updated` |
| `DELETE /admin/subscriptions/{id}` | CSRF + Edit | `200 subscription_deleted`, `404` on a second |
| `POST /admin/subscriptions/playlist-id` | CSRF + Edit | `200` + `{ playlistId }` — the generator, writes nothing |
| `POST /admin/subscriptions/{id}/prolong` | CSRF + Edit | `200 subscription_prolonged` (code + sentence; the new expiry is read back from the detail) |
| `POST /admin/subscriptions/bulk` | CSRF + Edit | `200 subscription_bulk_completed`, one entry per identifier |

- **One table plus one junction.** `subscriptions` carries every parameter as its own column —
  `playlist_id`, `mag_mac`, `enigma2_mac` are nullable columns, not child tables, because each kind
  is at most one per subscription and "delete the playlist" (single or bulk) is a field clear.
  `subscription_bouquets` mirrors `tariff_plan_bouquets` (unique pair, cascade on both sides) and
  holds the subscription's *own* bouquet list. `play_token` is a nullable column that nothing here
  writes or reads: the streaming subsystem that will verify MAC access owns its lifecycle, and the
  column exists now so that subsystem finds it where it expects to.
- **A tariff plan is the single source of truth for what it owns.** With `tariff_plan_id` set, the
  plan decides `max_connections`, `is_restreamer`, `output_formats`, `is_stb_lock`,
  `is_country_lock` (= `!plan.AllowMultipleCountries`; `is_isp_lock` has no plan counterpart and is
  always the operator's own), `is_trial` and which services may exist; the request's values for
  those fields are not written and are not even validated. Clearing the plan nulls the reference
  and the subscription's own stored values apply again. A plan may be attached only when its user
  groups include the owner's group ("available to the owner").
- **Expiry is computed, never guessed — and prolongable.** With a plan: `expire_at = created_at + DurationValue` in
  the plan's unit (`AddMonths`/`AddYears` are calendar-aware), computed when the plan is **set or
  changed** in a save — deliberately from `created_at`, so the answer is deterministic even when it
  lands in the past and the row shows `expired`. A save that keeps the plan keeps the stored expiry:
  recomputing on *every* save would silently wipe a prolongation. **Prolong** is its own operation
  (`POST .../{id}/prolong`, bulk `prolong` delegating to it): the new expiry is
  `max(now, current expiry) + plan duration`, so remaining time is kept and a short plan can never
  shorten what is left; without a plan there is no duration to add and the row is refused on
  `tariffPlanId`. The list item carries `tariffPlanDurationValue` / `tariffPlanDurationUnit`
  (absent without a plan) so the admin UI can preview the new expiry in its confirmation dialog
  without a second request. Without a plan the expiry is the operator's own moment — the contract accepts an
  ISO 8601 date-time and a bare `yyyy-MM-dd` (that day's midnight), `null` meaning "never expires".
  The status itself is never stored: `SubscriptionStatusComposition` computes `expired` (expiry in
  the past) > `blocked` (the owner's effective status, own or group) > `trial` > `active`, and the
  list filter translates into the same predicate before `CountAsync`, so the tag and the filter
  cannot disagree.
- **Services: dropped, not refused, and at least one must survive.** A service the effective plan
  does not support is cleared on save (the video streams' "a payload the chosen kind cannot use is
  dropped" rule), and a save that would leave no service at all is refused with the error keyed
  under `playlistId`, `magMac` *and* `enigma2Mac`. Without a plan the bouquet list is written and
  at least one bouquet is required; with a plan the stored list lies dormant and is re-activated —
  all bouquets preselected when it is empty — the moment the plan goes.
- **The three identifiers are unique and system-generated where they are generated.** A playlist id
  is 32 hex characters from `RandomNumberGenerator`; the generator endpoint loops against
  `PlaylistIdExistsAsync` until it finds an unused one, and the create form auto-generates one only
  when the selected plan supports playlists and nothing else. A MAC is canonicalised to uppercase
  `XX:XX:XX:XX:XX:XX` (input may use `-` or either case). The three partial unique indexes
  (`IX_subscriptions_playlist_id`, `IX_subscriptions_mag_mac`, `IX_subscriptions_enigma2_mac`,
  each `WHERE ... IS NOT NULL`) are matched **by name** and answer `409 subscription_already_exists`
  naming the field; anything else is rethrown. The duplicate probe runs **after** the plan-clearing,
  so a value the plan drops is never refused as a duplicate.
- **The owner is chosen once; the creator is provenance.** `user_id` is picked at creation from all
  users and never changes (`ON DELETE CASCADE` — an account's subscriptions go with the account);
  `reseller_id` is stamped with the acting admin/reseller and may be reassigned only to an Admin or
  Reseller role (`ON DELETE SET NULL`, the column is nullable).
- **Whitelists travel as `text[]`**, like VOD `cast`/`genres`: 20 entries each, an IP entry must
  parse as an IPv4/IPv6 address or CIDR range, a user-agent entry is free text capped at 200
  chars, and an empty list means no restriction. PATCH list semantics are the usual ones: absent
  keeps, `[]` clears, a list replaces.
- **Bulk is the eight single-row writes in a loop**, behind its own controller
  (`SubscriptionsBulkController`, the request names a set the table assembled): `change-tariff`,
  `change-creator`, `prolong`, `delete`, `delete-playlist`, `delete-mag`, `delete-enigma2`,
  `block-owner`.
  The body is validated before any row is read (1..1000 uuids, duplicates collapse), the answer is
  the partial-success report every other bulk endpoint answers, and a plan change that cleared a
  service reports it as a warning (`subscription_playlist_cleared` / `..._mag_cleared` /
  `..._enigma2_cleared`) — warnings are not failures. `block-owner` calls
  `IUserAdminService.SetBlockedAsync`, so it blocks the account and revokes its sessions exactly
  as `POST /admin/users/{id}/block` does; it is idempotent and `cannot_block_self` refuses the
  acting user's own row. Clearing an already-empty service field succeeds as a no-op.
- **`POST /admin/subscriptions/playlist-id` is a recorded REST departure**, in the list above: it
  mints a value rather than writing a resource, the same reasoning as `POST /auth/csrf`, and the
  form fills its read-only field from it. The alternative — a `PATCH` flag that meant "generate
  something" — would have made a field write answer with a value the request did not name.
- **The permissions are `ViewSubscriptions = 16777216` and `EditSubscriptions = 33554432`**, the
  two free flag values. As with the tariff pair, no grant migration is strictly needed (the seeder
  reconciles the base groups), but `20261029152000_GrantSubscriptionPermissions` exists anyway,
  mirroring `GrantTariffPlanPermissions`, for an installation that migrates without restarting; a
  custom administrator group still has to be granted the pair by hand.
- **Deleting a tariff plan a subscription uses is refused**: `409 tariff_plan_in_use` with
  `SubscriptionsUsing` on the outcome (`TariffPlanAction.InUse`), the `epg_source_in_use` shape.
  The FK is `RESTRICT` and `TariffPlanService` therefore reads `ISubscriptionRepository` —
  Application to a Core interface, which is the layering rule. Without the check, deleting a plan
  would silently re-mean every derived parameter the plan was the only record of.
- **`FluxyDbContext` now takes a `TimeProvider`.** The expiry is `created_at + duration` while
  `created_at` is stamped by the context, and two clocks measuring one instant disagree — a 9.6 s
  gap was measured before the seam. The context stamps its audit columns from the injected
  provider, the service derives from the same one, and the design-time factory passes
  `TimeProvider.System`. `CreateAsync` reads the clock once for the derivation and the model
  stamps; the residual microsecond skew to the context's own `created_at` is documented in its
  remarks. A composition root that calls `AddDataAccess` must have registered a `TimeProvider`
  (`AddApplicationServices` does).

**Codes:** `subscription_created|updated|deleted|not_found|already_exists|prolonged`, plus
`subscription_playlist_cleared|mag_cleared|enigma2_cleared` as bulk warnings and
`tariff_plan_in_use` on the plans side — all translated in both locale bundles.

Layering follows the usual split: `Subscription` in `Fluxy.Core/Subscriptions/Models/`,
`SubscriptionListItem` / `SubscriptionDetail` / `SubscriptionPage` / `SubscriptionDerivedValues`
in `ReadModels/`, `SubscriptionPlanContext` / `SubscriptionOwnerContext` in `Rows/` (the two
projections the service reads before it writes), `NewSubscription` / `SubscriptionPatch` /
`BulkSubscriptionOperation` in `Commands/`, `SubscriptionOutcome` / `BulkSubscriptionOutcome` /
`BulkSubscriptionItemResult` in `Outcomes/`, `SubscriptionStatus` / `SubscriptionAction` /
`BulkSubscriptionAction` / `SubscriptionBulkWarning` / `SubscriptionOutputFormat` and the sort
pair in `Enums/`, `SubscriptionStatusComposition` / `SubscriptionLimits` /
`SubscriptionListLimits` / `SubscriptionBulkLimits` in `Utils/`, `ISubscriptionService` /
`ISubscriptionBulkService` and `ISubscriptionRepository`; `SubscriptionService` /
`SubscriptionBulkService` and `SubscriptionRules` in `Fluxy.Application/Subscriptions/`; the two
entities, their configurations and `SubscriptionRepository` in `Fluxy.DataAccess` (migrations
`AddSubscriptions`, `GrantSubscriptionPermissions`); the contracts in
`Fluxy.API/Contracts/Subscriptions/`; and `SubscriptionsController` +
`SubscriptionsBulkController` in `Fluxy.API/Controllers/Subscriptions/`.

Verified: `dotnet build` clean, **426** unit tests and **196** integration tests green (the
subscriptions suite covering CRUD, plan derivation and clearing, expiry arithmetic and the
kept-plan rule that preserves a prolongation, prolongation itself, the min-one-service and
min-one-bouquet refusals, uniqueness mapping and rethrow, list search/filter/sort/paging refusals,
every bulk action with partial success and warnings, CSRF and permissions), `dotnet ef migrations
has-pending-model-changes` reports no changes, and the frontend passes
`typecheck`, `lint`, `test`, `build`, `probe:render`, `probe:i18n` and `probe:paths`.

## Conventions

- Block-scoped namespaces (`namespace X { ... }` with the type indented inside) and `using` directives at the top of the file — not file-scoped namespaces. Current root namespace is `Fluxy.API`; the old `Refluxy.*` rename is complete.
- **API contracts are grouped by feature**: `Fluxy.API/Contracts/<Feature>/` holds the request and response types one controller speaks, and the folder name is the namespace segment — `Fluxy.API.Contracts.Authentication`, `Fluxy.API.Contracts.Users`, and so on. The few types more than one feature names — `MessageResponse`, the bulk answers, `FieldErrorKeys` — live in `Fluxy.API.Contracts.Common`. A contract belongs in `Common/` the moment a second feature needs it, not in whichever feature happened to define it first.
- `ImplicitUsings` and `Nullable` are enabled in all 6 projects.
- **One type per file**, in every layer: the file name is the type name and nothing else. A type that only one other type uses is still a file of its own.
- **Comments in project files are written in English** — code, config files and this rule itself. Russian stays where it belongs: this document and anything that is prose for humans, not source. XML doc comments (`///`) are preferred over inline comments.
- **Commit messages are written in English and kept short** — one line for the subject, a body of a few lines at most, and only when the change genuinely needs it.
- Identifier naming is standard .NET (`FluxyDbContext`, not `FluxyContext`/`DBContext`); folder names are `Configurations/`, `Entities/`, `Context/`, `Migrations/`, `Repositories/`.
