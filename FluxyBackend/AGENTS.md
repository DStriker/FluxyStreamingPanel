# AGENTS.md — FluxyBackend

ASP.NET Core **10** (`net10.0`) backend of the `Refluxy` monorepo. Git root is **one level up** (`..\`). Sibling `..\FluxyFrontend` has its own `AGENTS.md` — read it before touching anything frontend-facing. Monorepo `README.md` is one line (`# Fluxy`) — no help there.

## Commands

```bash
dotnet build FluxyBackend.slnx                                 # 6 projects, ~5s, 0 warnings
dotnet test --project tests\Fluxy.UnitTests                     # 526 tests, no Docker
dotnet test --project tests\Fluxy.IntegrationTests              # 228 tests, Docker required
dotnet test --solution FluxyBackend.slnx                        # both projects, 754 tests
dotnet run --project Fluxy.API --launch-profile http          # http://localhost:5159
dotnet run --project Fluxy.API --launch-profile https         # https://localhost:7221 + http://localhost:5159, opens /swagger
dotnet run --project Fluxy.API --launch-profile lan           # http://0.0.0.0:5159, reachable from other machines
dotnet ef migrations add <Name> --project Fluxy.DataAccess    # scaffolds into Fluxy.DataAccess/Migrations
docker_run.bat                                                # generates .env if missing, then == docker compose up -d (run from FluxyBackend)
powershell -ExecutionPolicy Bypass -File scripts\init-secrets.ps1   # (re)generate secrets in .env; -Force regenerates all
```

- .NET 10 SDK required (installed: 10.0.401; `global.json` pins the 10.0.4xx feature band with `latestPatch`). Solution uses the XML `.slnx` format.
- Unit tests need only the SDK. Integration tests need Docker Desktop — Testcontainers starts `postgres:18.6` and `redis:8.6`. They drive the real composition root, migrations and HTTP pipeline, reset the database and Redis before each test, and run sequentially in one xUnit collection; the host runs in `Testing`, so the production error envelope is what is asserted.
- **No `.editorconfig`, no lint/format config, no `Directory.Build.props`/`Directory.Packages.props` (no central package management).** Verification is `dotnet build`, `dotnet test`, and hitting the endpoint. `dotnet format` is deliberately not a gate yet.
- `dotnet-ef` is pinned in `.config/dotnet-tools.json`; run `dotnet tool restore` once after cloning.
- All three launch profiles hardcode `ASPNETCORE_ENVIRONMENT=Development`, so dev-only middleware is on even outside Visual Studio — and `lan` therefore serves Swagger and the developer exception page to the whole network. Content root is `Fluxy.API/`, not the repo root — run docker from `FluxyBackend/`, not the project folder.
- Single branch, commit directly. No branches or PRs.
- `git` 2.43.0 on PATH via the **user** PATH entry `C:\Users\user\Tools\Git\cmd` (copy of the portable git inside `C:\Program Files\BeefLang\bin\Git`; `msys64` has no git). Works on the repo one level up. New shells pick it up; a shell started earlier needs a restart.

## Tests and CI

Two xUnit v3 projects (Microsoft Testing Platform) under `tests/`, part of the solution:

- **`Fluxy.UnitTests`** — pure unit tests for Core/Application rules (login guard, GeoIP address handling, status composition, permissions, registration, time zones, one-time codes, token signing) plus architecture checks keeping Application free of EF Core/DataAccess/Redis/MaxMind and Core free of project references. No Docker, no database, ~1 s.
- **`Fluxy.IntegrationTests`** — `WebApplicationFactory<Program>` drives the real composition root over HTTP. Testcontainers starts PostgreSQL 18.6 and Redis 8.6 once per run; Respawn truncates the database and Redis is flushed before every test, then the three base groups are recreated. Tests run sequentially in one `[Collection]`. `IEmailSender`, `IRecaptchaValidator`, `IGeoIpResolver`, `TimeProvider` and `IUserSeeder` are replaced with deterministic fakes; the host runs in `Testing`, so assertions see the production status-code/error-envelope pipeline. Docker Desktop must be running.
- **Tests never call internal persistence types.** Data is planted through Core repository interfaces (`TestDataSeeder`), except a few deliberate SQL fixtures (e.g. an expired registration code).
- **Do not change API behaviour to make a test pass.** If a scenario disagrees with the code, the test documents the actual contract or the code has a bug worth reporting separately.
- **CI**: `.github/workflows/backend.yml` runs on pushes/PRs touching `FluxyBackend/**` (and on demand). Restores, builds Release with `-warnaserror`, runs both projects separately with a minimum test count and their own Cobertura coverage file, verifies `dotnet ef migrations has-pending-model-changes`, and fails if `dotnet list package --vulnerable` reports any package. Test results and coverage upload as the `backend-test-results` artifact.
- Coverage has no threshold yet; the baseline is collected so one can be chosen deliberately later. `dotnet format` is not a gate because there is no `.editorconfig`.

## Architecture — the layers are isolated

`FluxyBackend.slnx` = **4 production projects + 2 test projects**. Dependency graph (confirmed by the owner — the contract, not a guess):

```
Fluxy.API ──┬──> Fluxy.Application ──┐
            │                        ├──> Fluxy.Core
            └──> Fluxy.DataAccess ───┘
```

- **Fluxy.API** — controllers, request/response DTOs (contracts), HTTP concerns: DI wiring, middleware, options, security handlers.
- **Fluxy.Application** — services and business logic. References Core **only**; no Entity Framework, StackExchange.Redis or MaxMind.GeoIP2 package, cannot see `Fluxy.DataAccess`.
- **Fluxy.Core** — immutable business models, contracts, shared rules. Depends on nothing.
- **Fluxy.DataAccess** — persistence: PostgreSQL via `Context/FluxyDbContext.cs` + `Context/FluxyDbContextFactory.cs`, `ServiceCollectionExtensions.cs` (`AddDataAccess`), `Entities/`, `Configurations/`, `Migrations/`, `Repositories/`; Redis via `Infrastructure/Redis/` (shared connection, attempt throttle, revoked-session store); MaxMind `.mmdb` reader via `Infrastructure/GeoIp/`. `Infrastructure/` also holds `UnitOfWork` and `DatabaseInitializer`. Empty folders are declared as `<Folder Include>` in the csproj — empty folders are invisible to git.

**The graph is wired in the csproj files** — a class added to a layer is visible to the consumers named by the arrows, not to the layer above. Adding `Application → DataAccess` back would defeat the isolation this split exists for.

## Intended architecture — the layers are isolated

`Fluxy.Core` holds `<Domain>/<Kind>/` (model, contracts, shared rules, domain first, kind second); `Fluxy.Application` mirrors it (`<Domain>/<Kind>/`, services + helpers + the configuration they read) plus its DI extension; `Fluxy.API` holds `Program.cs` wiring, top-level `Extensions`/`Middleware`/`Options`/`Security`, `Contracts/<Feature>/`, `Controllers/<Area>/`.

## The core layer is organised by domain, then by kind

Domain-first tree; namespace mirrors the folder exactly — `Fluxy.Core.<Domain>.<Kind>` — so a file's path is its namespace.

```
Fluxy.Core/
  Persistence/       Base 1 · Exceptions 1 · Interfaces 2 · Utils 1
  Security/          Models 1 · Interfaces 2 · Enums 1
  Email/             Commands 1 · Interfaces 1 · Enums 1
  GeoIp/             ReadModels 1 · Interfaces 1 · Utils 2
  Users/             Models 5 · Rows 4 · ReadModels 8 · Commands 9 · Outcomes 9 · Enums 18 · Utils 8 · Interfaces 8 · Repositories 4 · Exceptions 1
  Authentication/    Models 1 · Rows 5 · ReadModels 7 · Commands 1 · Outcomes 3 · Enums 5 · Utils 3 · Interfaces 7 · Repositories 1 · Options 1
  Content/           Models 2 · Commands 7 · Enums 5 · ReadModels 3 · Utils 2 · Interfaces 2 · Repositories 2
  Streams/           Models 2 · Rows 2 · ReadModels 4 · Commands 13 · Outcomes 11 · Enums 17 · Utils 10 · Interfaces 6 · Repositories 2
  Vod/               Models 1 · Rows 1 · ReadModels 3 · Commands 5 · Outcomes 6 · Enums 9 · Utils 4 · Interfaces 4 · Repositories 1
  Series/            Models 2 · ReadModels 7 · Commands 9 · Outcomes 8 · Enums 12 · Utils 7 · Interfaces 6 · Repositories 2
  Epg/               Models 2 · ReadModels 4 · Commands 3 · Outcomes 1 · Enums 5 · Utils 3 · Interfaces 1 · Repositories 1
  Blacklists/        Models 1 · ReadModels 2 · Commands 3 · Outcomes 1 · Enums 4 · Utils 2 · Interfaces 1 · Repositories 1
  Subscriptions/     Models 3 · Rows 2 · ReadModels 9 · Commands 6 · Outcomes 4 · Enums 14 · Utils 7 · Interfaces 5 · Repositories 3
```

**Domain is decided by what the type is about, not who reads it.** `IGeoIpResolver` is GeoIP (a question about an address); `ILoginGuardService` is Users (allow lists belong to an account; GeoIP is only where the answer comes from); `ITokenRevocationStore` is Authentication (it ends a session, not "security").

**Kind is decided by the role the type plays**; each kind answers one question:

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

**`Models/` holds the business model and nothing else** — **fifteen** types, one per persisted business thing: `User`, `UserGroup`, `LoginGuardRule`, `PendingChange`, `LoginGuardSettings` (Users), `RefreshToken` (Authentication), `ContentCategory` (Content), `VideoStream`, `AudioStream` (Streams), `SeriesItem`, `Episode` (Series), `EpgSource`, `EpgData` (Epg), `AttemptPolicy` (Security), `ConnectionLog` (Subscriptions). Anything else is a projection, read model, command or result and lives in the folder that says so. The `Stored` prefix means exactly *a projection shaped for a single query* and survives only in `Rows/`.

**One type per file.** Twelve files once held two or three types each; they were split, so the file name is the type name and nothing else.

**The API layer is organised by area, not by file kind.** Tree and the rule for a new type:

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

**A controller lives in the area its route starts with** — `auth` in `Auth/`, `admin/users|user-groups|sessions` in `Admin/`, `admin/content-categories|video-streams|audio-streams` in `Content/` — namespace mirrors the folder (`Fluxy.API.Controllers.<Area>`). Split a file by a **seam** (separate resource or request shape), not by length: `ProfileController` gave session history to `SessionsController`; `UsersController`/`UserGroupsController` gave bulk endpoints their own controllers; one-resource controllers stay whole. `ApiControllerBase` carries only what every area needs (antiforgery, `NoStore`); `AuthControllerBase` adds only `ClientAddress`; the two guest checks compose as `AttemptLimiter`/`CaptchaGuard` into the four controllers that spend them (registration, authentication, password reset, profile), so `SessionsController`, `Admin` and `Content` never inject an unused throttle. Contracts follow the same split in `Contracts/<Feature>/`; `Common/` is for a type a second feature names.

**The envelope is API-owned, including the framework's own refusals.** Every action answers `{ code, message }`; the two paths that never reach an action use the same shape: an unhandled exception is `500 server_error` through `ApiExceptionHandler`, a bodyless 404/405/415 is `not_found` / `method_not_allowed` / `unsupported_media_type` through `UseFluxyStatusCodePages()`. Both write `Cache-Control: no-store`; the exception is logged, never sent to the client. Development keeps `UseDeveloperExceptionPage` for 500s; the handler sits only on the other branch.

- **`AddFluxyExceptionHandling()` also calls `AddProblemDetails()`.** Parameterless `UseExceptionHandler()` refuses to start without a problem-details service even with an `IExceptionHandler` registered — found by booting in Production; the compiler says nothing. The handler returns `true` first, so problem details are a container dependency, not the answer.
- **`SuppressMapClientErrors = true` is what lets 415 reach the envelope.** With `[ApiController]`, MVC turns the bodyless `UnsupportedMediaTypeResult` into a `ProblemDetails` body before `UseStatusCodePages` sees it; suppressing leaves it bodyless, so the status pages describe 404/405/415 while a controller with something to say still returns its own `ObjectResult`.
- **The status filter is deliberate.** `UseStatusCodePages` only touches a body-less response; the explicit 404/405/415 mapping leaves every `auth_required`, `permission_denied` and feature-level `*_not_found` body as its owner wrote it. A 404 from a real action stays `user_not_found` / `session_not_found`, not the generic `not_found`.

## The application layer is organised by domain, then by kind

`Fluxy.Application` mirrors `Fluxy.Core` — domain senior, kind second, namespace mirrors folder exactly (`Fluxy.Application.<Domain>.<Kind>`). Difference is what it holds: Core holds model + contracts; Application holds the services that use them + helpers.

```
Fluxy.Application/
  ServiceCollectionExtensions.cs   the assembly's DI surface: AddApplicationServices
  Authentication/  Services 6 · Utils 2
  Blacklists/      Services 1 · Utils 1
  Content/         Services 2 · Utils 2
  Email/           Services 1 · Options 1
  Epg/             Services 1 · Utils 1
  Security/        Services 1 · Options 1
  Series/          Services 6 · Utils 3
  Streams/         Services 6 · Utils 2
  Subscriptions/   Services 5 · Utils 4
  Users/           Services 9 · Utils 8 · Options 1
  Vod/             Services 4 · Utils 1
```

**The domain is the same domain as in Core.** A service that ends a session is Authentication even though the row is a refresh token; a service that replaces an account's allow lists is Users even though the answer is GeoIP. A type's domain never depends on the layer.

**Three kinds, each decided by what the type is for.**

| Kind | What it holds | The test |
| --- | --- | --- |
| `Services/` | the business services | it implements a contract of `Fluxy.Core/<Domain>/Interfaces/` |
| `Utils/` | the helpers the services share — a normalizer, a policy, a memo, the frame of a message | it is not a business operation: no contract, no outcome |
| `Options/` | the configuration the services read | it is bound from a configuration section |

**A service is a service because it implements a contract.** `ProfileService`, `RegistrationService`, `PasswordResetService`, `UserAdminService`, `UserGroupService`, `ContentCategoryService`, `VideoStreamService`, `VideoStreamBulkService`, `VideoStreamImportService`, `AuthenticationService`, `JwtTokenService` and the three session readers implement a `Fluxy.Core` interface; the API names that interface, never the class. `AccessChecker` is the one service without its own — a helper the authorization handlers call, not an operation a client asks for — and sits beside them because it is a service in the ordinary sense.

**A file that is not a service does not live in `Services/`.** `CodeMessage` (the frame of a message carrying a code), `DevelopmentGuardBypass` (lets a local address past the login guard in Development), `GeoIpMemo` (the lookups of one request, one per address), `OneTimeCode`, `EmailNormalizer`, `RegistrationPolicy`, `LoginGuardPolicy`, `LoginGuardNormalizer`, `TimeZonePolicy` and `VideoStreamRules` are helpers; a helper in `Services/` is a service that answers nothing.

**Two Core domains have no folder here, on purpose.** `GeoIp` — the `.mmdb` reader is a mechanism, and mechanisms belong to the layer that owns data sources (`Fluxy.DataAccess/Infrastructure/GeoIp/`); Application knows only the `IGeoIpResolver` contract. `Persistence` — the transaction and database lifecycle are Core contracts implemented in DataAccess; Application calls `IUnitOfWork` without ever naming a `DbContext`. `Persistence` would also be the wrong word: a service does not persist, it asks for a change to be committed.

**`ServiceCollectionExtensions.cs` sits at the project root**, namespace `Fluxy.Application`: the assembly's DI surface, belonging to no domain. `Program.cs` calls `AddApplicationServices` and nothing else of this layer.

**The two internal collaborators stayed concrete.** `PendingChangeService` and `UserBulkService` are not in Core: same-layer callers only, no client to answer, so no contract and no reason for the API to know them. `UserBulkService` is the exception that proves the rule — the API *does* call it, so it has `IUserBulkService`, and `BulkAsync` is no longer a method of `IUserAdminService`. A bulk request names a set a table assembled on screen and answers one entry per row of that set — a different shape from one outcome per row of the single-account methods; a client asking for the second must not have to know which single-row method it is built on.

## The rules that are written once

Application had four rules written more than once; each now lives where the rule belongs:

| Rule | Was | Now |
| --- | --- | --- |
| the Development bypass of the login guard | three copies of `BypassesGuard` | `Users/Utils/DevelopmentGuardBypass.cs` |
| the frame of a message carrying a code | three copies of `BuildCodeBody` | `Users/Utils/CodeMessage.cs` |
| the memo of the GeoIP lookups of one request | three dictionaries, one per service | `Authentication/Utils/GeoIpMemo.cs` |
| the shape of a refused write | six `try`/`catch` blocks around `SaveChangesAsync` | `Core/Persistence/Utils/UnitOfWorkExtensions.cs` |

**`TrySaveAsync` is where the unique-violation rule lives.** The overload without an index list hands the violation back so the caller can ask `IsConstraint` about the index it knows; the overload that takes the list returns only a violation from one of them and **rethrows anything else** — the repository section's "a violation from an index the caller does not recognise is not a name conflict" rule, written once. It is why a bug in a list writer cannot be announced as "that name is already taken".

**Two seams came out of the deduplication.** `PendingChangeService` holds the machinery of a change that waits for a one-time code — staging the row, hashing the code, checking a code against the row that holds it, removing the spent row — because profile change and password reset each wrote their own copy of those three steps. The flows keep the wording of the message, where it is sent, and what happens after the code is accepted. `PendingCodeStatus` (in `Core/Users/Enums/`) is the verdict of a code — "wrong", "expired" and "valid" are the same three answers wherever a code is checked.

## Interfaces belong in Core, implementations in their layer

**Every interface lives in `Fluxy.Core`; the implementation lives in the layer that owns the mechanism.** Business services implement `Fluxy.Core/<Domain>/Interfaces/` and live in `Fluxy.Application/<Domain>/Services/`; persistence interfaces live in `Fluxy.Core/<Domain>/Repositories/` and are implemented in `Fluxy.DataAccess/Repositories/`. External data sources are the third case: `IGeoIpResolver` lives in `Fluxy.Core/GeoIp/Interfaces/`, its MaxMind implementation in `Fluxy.DataAccess/Infrastructure/GeoIp/` beside the redis stores. `IUnitOfWork` and `IDatabaseInitializer` live under `Fluxy.Core/Persistence/Interfaces/` and are implemented in `Fluxy.DataAccess/Infrastructure/` — a transaction and the database lifecycle, not a repository; the owner asked for that separation explicitly.

Practical rule: if the API needs to *call* something it references the `Fluxy.Core` interface, and `Program.cs` wires it. `Fluxy.API` also references `Fluxy.DataAccess` for `AddDataAccess`/`AddRedis`/`AddGeoIp`, but never names a repository class, a `DbContext` or any EF type.

Why: it keeps the graph honest — Core depends on nothing, Application on Core only, DataAccess on Core, API on Application and DataAccess. A repository interface leaked into DataAccess would let the API reach infrastructure abstractions directly and the layering would stop meaning anything.

### Repository rules

- **A repository speaks Core models.** Reads return Core models or page models (`AdminUserPage`, `VideoStreamDetail`, …); writes are intent methods (`SetStatusAsync`, `ReplaceSourcesAsync`, `RemoveAsync`). An `IQueryable` never leaves DataAccess.
- **Business rules stay in Application** — role visibility, status transitions, anti-lock, which permission a write needs, the meaning of a uniqueness conflict. The repository owns SQL shape, tracking, paging, row storage.
- **One `SaveChangesAsync` per business operation**, through `IUnitOfWork`. Repositories stage; the service calls the single save after every rule passed — so a new group and its permission rows, or an account and its guard rows, commit together.
- **`ExecuteUpdate`/`ExecuteDelete` are immediate** and not rolled back by the unit of work. Each says so in remarks; used only for token revocation and cleanup. Login-guard replacement loads old rules through the tracker, so deletion and new rows commit with the account in the same `SaveChanges`.
- **Unique violations are matched by exact constraint name** (`IX_user_groups_name`, `IX_content_categories_content_type_name`, `IX_video_streams_category_id_name`, …) and mapped to the feature's `already_exists`; any other violation is rethrown — a future bug must not be reported as a plausible name conflict.
- **Paging, search and sort are server side** in the repository because `total` must be counted under the same predicate; applying them after paging would make the pager lie.
- **`FluxyDbContext` is `internal`** — as are the repository classes and `DbContextExtensions`, so nothing outside DataAccess can name the persistence model. The design-time factory is internal too; `dotnet ef migrations list --no-connect` still discovers it (verified).

**MSB3277 trap.** Any project referencing `Fluxy.DataAccess` builds against EF Core 10.0.12 but receives Relational 10.0.4 transitively, because the 10.0.12 that `Microsoft.EntityFrameworkCore.Design` brings is `PrivateAssets=all`. Such a project must pin `Microsoft.EntityFrameworkCore.Relational` 10.0.12 explicitly (see the PostgreSQL section of `docs/data-and-infra.md`). `Fluxy.API` is that project and carries the pin; `Fluxy.Application` deliberately does not reference DataAccess, so it needs neither reference nor pin.

## Domain rules — every entity, every row

Two rules apply to **every** persisted entity, enforced by code:

1. The primary key is a **`uuid`**, generated on the client (`Guid.NewGuid()` in the `AuditableEntity` constructor, `ValueGenerated.Never` in the model convention). Never `gen_random_uuid()`, never an `int`.
2. The row stores **`created_at`** and **`updated_at`** (`timestamp with time zone`, mapped from `DateTimeOffset`), stamped by the context, never by the entity.

`Fluxy.Core/Persistence/Base/AuditableEntity.cs` supplies the three properties — public getters, **private** setters, so EF can write them and nothing else can. Two constructors: one generates the key, one rehydrates a row (used by `UserEntity.FromModel`).

`FluxyDbContext` does the rest:

- `ApplyAudit()` runs from **both** `SaveChanges` overloads. `Added` gets `CreatedAt = UpdatedAt = DateTimeOffset.UtcNow` (equal, so a fresh row is not born with a newer `UpdatedAt`); `Modified` gets a fresh `UpdatedAt`. One `now` per call, so reading `UpdatedAt` twice for one save is safe.
- `ApplyAuditableEntityConvention()` walks every entity type and **throws** `InvalidOperationException` when a non-owned, non-shared type lacks a single-column `Guid` primary key, or has a nullable `CreatedAt`/`UpdatedAt`. Columns come from the base class; the convention validates the shape.
- Column and table names are **explicit** (`.HasColumnName(...)` in each `IEntityTypeConfiguration`) — snake_case, plural table. `UseSnakeCaseNamingConvention` is **not available** in `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3; it lives in the separate `EFCore.NamingConventions` package, which this project does not reference.
- `HasDefaultValue` is used **nowhere**, on purpose: a value the database invents when the application forgot a column hides the mistake instead of surfacing it.

Verified by running: an insert leaves `created_at = updated_at`; a later update keeps `created_at` and moves `updated_at` forward.

## API design aims at REST; every deviation is agreed explicitly

Reach for REST first: **plural noun resources**; the method that describes the operation (`GET` read-only, `POST` create, `PATCH` partial update, `DELETE` remove); the status code that describes the outcome (`201` on create — with a `Location` naming the new resource when it has a URI the client can address right away — `204` where there is nothing to say back, `404` for a resource that is not there). A route that reads as a verb (`/do-something`) is a smell — check whether it is really an operation on a resource.

**Any departure is a decision, not an accident, and the owner signs off before it is written.** Existing departures are approved and carry their reason in the XML docs of the owning controller; a new one is agreed with the owner first, then recorded in the endpoint's remarks and in this file. Already agreed:

- **Three per-role sign-in paths** (`POST /auth/client-login|reseller-login|admin-login`) — one path per entrance, so a stale URL cannot point at an audience it was not built for.
- **RPC-style session actions** (`POST /auth/refresh`, `POST /auth/logout`, `GET /auth/csrf`, `GET /auth/me`) — no resource to name: refresh mints tokens, logout ends state across two stores, `csrf` and `me` are questions rather than things.
- **`GET /auth/me` beside `GET /auth/profile`** — two readings of "who am I": `me` is the cheap session identity the guest guard polls on every navigation; `profile` is the full account row with the login guard. Merging would put the guest guard's round trip behind a database read it does not need.
- **Three admin account actions as `POST {id}/…`** (`confirm-registration`, `block`, `unblock` under `admin/users`) — each is a state transition on an existing row; alternatives were worse: `PATCH {id}` with a status would carry all transition rules (restore `Unregistered` vs `Registered`, clear `registered_at`, revoke sessions) in the caller's payload, and `DELETE`/`PUT` cannot mean "confirm". Owned actions also let each answer its own code (`registration_confirmed`, `user_blocked`, `user_unblocked`) and refusal (`invalid_status`) instead of overloading `user_updated`. The rest of the surface is plain REST — see `docs/admin-users.md`.
- **`POST /admin/subscriptions/playlist-id`** mints a value rather than writing a resource (same reasoning as `POST /auth/csrf`); the form fills its read-only field from it. A `PATCH` flag meaning "generate something" would make a field write answer with a value the request did not name.

The May 2026 REST pass collapsed five `POST /auth/profile/*` change endpoints into one `PATCH /auth/profile` and renamed `register`/`password` into `registrations`/`password-resets`. `POST /auth/registrations` answers **201 — deliberately without `Location`**: the row has no client-addressable URI yet — `/auth/profile` answers 401 for its own owner until the mailed code confirms it, and a pending-registration URI addressable by email would be the enumeration oracle the refusal policy exists to avoid (reasoning in the action's remarks; RFC 9110 makes the header a SHOULD and this is the case it leaves open).

Every private read (`me`, `profile`, `sessions`, `active-sessions`, `geo/lookup`, `csrf`) carries `Cache-Control: no-store` through `ApiControllerBase.NoStore()` — a shared cache keeping one of those bodies would hand one visitor's account to the next.

**Exactly one change kind per PATCH** — the staged-confirmation flow holds one pending change at a time, and the time zone (applied at once, no captcha, its own attempt window) must never share a request with a password change (mailed code, revokes sessions): the endpoint refuses a body naming two kinds with `validation_failed` rather than guessing an order. Contract shape: `{ currentPassword?, username? | email? | newPassword? | loginGuard? | timeZone? }`; absent means "leave it alone", an empty `timeZone` clears the preference.

## Conventions

- Block-scoped namespaces (`namespace X { ... }` with the type indented inside), `using` directives at the top — not file-scoped. Root namespace is `Fluxy.API`; the old `Refluxy.*` rename is complete.
- **API contracts are grouped by feature**: `Fluxy.API/Contracts/<Feature>/` holds the request/response types one controller speaks; the folder name is the namespace segment — `Fluxy.API.Contracts.Authentication`, `Fluxy.API.Contracts.Users`, and so on. Types more than one feature names — `MessageResponse`, the bulk answers, `FieldErrorKeys` — live in `Fluxy.API.Contracts.Common`. A contract belongs in `Common/` the moment a second feature needs it.
- `ImplicitUsings` and `Nullable` are enabled in all 6 projects.
- **One type per file**, in every layer: the file name is the type name. A type only one other type uses is still its own file.
- **Comments in project files are English** — code, config, and this rule. Russian stays where it belongs: this document and human prose, not source. Prefer XML doc comments (`///`) over inline.
- **Commit messages are English and short** — one-line subject, a few lines of body at most, only when the change genuinely needs it.
- Identifier naming is standard .NET (`FluxyDbContext`, not `FluxyContext`/`DBContext`); folder names are `Configurations/`, `Entities/`, `Context/`, `Migrations/`, `Repositories/`.

## Topic files — read on demand

Do not read these speculatively. Open exactly the file(s) whose trigger matches the task. Each lives in `docs\`; each links back here.

| File | Read when |
|---|---|
| [docs/auth.md](docs/auth.md) | auth, JWT/cookies/token rotation, Redis sessions, login guard/GeoIP allow lists, User entity/passwords, first admin |
| [docs/sessions-api.md](docs/sessions-api.md) | `GET /auth/sessions` or `GET /auth/active-sessions` (+ its two revokes), or their frontend pages |
| [docs/data-and-infra.md](docs/data-and-infra.md) | EF Core/DbContext/migrations, Redis, GeoIP databases, compose.yaml/services, `.env`/secrets, credential rotation, container verification |
| [docs/registration.md](docs/registration.md) | registration/confirm endpoints, registration error codes, reCAPTCHA, email/SMTP, "decisions worth not re-litigating" |
| [docs/http-pipeline.md](docs/http-pipeline.md) | middleware order/Program.cs, CSRF, CORS, HTTPS/forwarded-proto, clients off this machine, dev-mode behavior, rate limiting/throttling |
| [docs/admin-users.md](docs/admin-users.md) | `admin/users` endpoints, account CRUD, block/unblock/confirm-registration, admin-side login-guard edits |
| [docs/admin-user-groups.md](docs/admin-user-groups.md) | `admin/user-groups`, permissions/roles |
| [docs/admin-content-categories.md](docs/admin-content-categories.md) | `admin/content-categories` CRUD, `PUT /admin/content-categories/order`, category uniqueness per `ContentType` |
| [docs/admin-video-streams.md](docs/admin-video-streams.md) | `admin/video-streams`, stream bulk operations, M3U playlist import |
| [docs/admin-audio-streams.md](docs/admin-audio-streams.md) | `admin/audio-streams` (two kinds, bulk menu, import, its permissions) |
| [docs/admin-video-on-demand.md](docs/admin-video-on-demand.md) | `admin/video-on-demand` CRUD/metadata, `play` redirect vs relay, transcode stub, VOD M3U import |
| [docs/admin-series.md](docs/admin-series.md) | `admin/series` and `/episodes` CRUD, add-season folder scan (`series_season_created`), series/episode bulk |
| [docs/admin-epg-sources.md](docs/admin-epg-sources.md) | `admin/epg-sources` programme guide source CRUD, `/channels` stream join, `days_keep`/`epg_source_in_use` |
| [docs/admin-subscriptions.md](docs/admin-subscriptions.md) | `admin/subscriptions` CRUD, tariff-plan derivation and clearing, expiry/prolong arithmetic, subscription bulk |
| [docs/admin-connection-logs.md](docs/admin-connection-logs.md) | `admin/connection-logs` list/filters, clear-all `DELETE`, `activeConnectionCount` on subscriptions |
