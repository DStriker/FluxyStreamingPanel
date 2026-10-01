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

## Auth is not set up at all

The old inline `BasicAuthHandler` (hardcoded `admin`/`admin`) has been **deleted**. Current `Program.cs`:

- Never calls `AddAuthentication` and registers no authentication scheme.
- `app.UseAuthentication()` is commented out; `app.UseAuthorization()` is still called (its services come implicitly from `AddControllers()`).
- Auth is opt-in per controller via `[Authorize]`, but with no scheme registered an `[Authorize]` endpoint will fail at request time instead of returning 401. Add a scheme first, then authorize.

Don't assume any auth, cookies, or sessions exist.

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
- **No super-admin by default.** `-RedisAdmin` adds `dev-admin` with `+@all` for manual `redis-cli` work; local dev machine only.
- Lockout recovery: `docker compose up -d --force-recreate redis` rebuilds the ACL from `.env`.
- The dead `redis-conf.conf/` folder was deleted.

### pgadmin (`dpage/pgadmin4:9.10`)

- `PGADMIN_CONFIG_SERVER_MODE` is `"True"` **because the port is LAN-exposed**: in desktop mode (`"False"`) pgAdmin serves `/browser/` with no login form, so `PGADMIN_DEFAULT_PASSWORD` protects nothing. Verified: anonymous `GET /browser/` → 302 to `/login`.
- The entrypoint creates the admin user only when `/var/lib/pgadmin/pgadmin4.db` does not exist, so `PGADMIN_DEFAULT_EMAIL`/`PGADMIN_DEFAULT_PASSWORD` are silently ignored on an existing volume. After changing them: `docker compose rm -f pgadmin`, `docker volume rm fluxybackend_pgadmin-data`, `docker compose up -d pgadmin`. The volume name is prefixed by the compose project name, which follows the folder name.
- `PGADMIN_CONFIG_MASTER_PASSWORD_REQUIRED: "False"` — the admin login is the access control.
- **The server is pre-registered from `pgadmin/servers.json`** (`PGADMIN_SERVER_JSON_FILE` + `PGADMIN_REPLACE_SERVERS_ON_STARTUP="True"`), so the list is rebuilt from that file on every start and a misconfigured entry cannot stick. The DB password is deliberately absent from the file — pgAdmin prompts for it, and you paste `POSTGRES_PASSWORD` from `.env`.
- **Host must be `postgres`, not `127.0.0.1`.** pgAdmin and postgres are separate containers, each with its own network namespace, so `127.0.0.1` inside pgAdmin *is pgAdmin* and the error is `Connection refused` while postgres is plainly running. `postgres` is the compose service name and resolves on the shared `postgres` network (172.18.x.x). Same rule anywhere else: from a container use the service name, from the host use `localhost` (the ports are published).

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

Built-in `IAntiforgery`. The framework cookie name is **not** changed. A readable `XSRF-TOKEN` cookie is written manually on every `GET /auth/csrf` response, containing `tokenSet.RequestToken`. The request header is `X-CSRF-Token` (`options.HeaderName`). The frontend reads the cookie first and falls back to the JSON body — both paths yield the same value.

**The token is checked by calling `IAntiforgery.ValidateRequestAsync` in each POST, not with `[ValidateAntiForgeryToken]`.** The attribute resolves to the MVC filter `ValidateAntiforgeryTokenAuthorizationFilter`, which is registered by **`AddControllersWithViews` only** — this project calls `AddControllers` because it is a JSON API with no views, so the attribute resolves to a type that is not in the container and every protected request fails with **500 `No service for type ... ValidateAntiforgeryTokenAuthorizationFilter has been registered`**. Verified against a running instance. Pulling the Razor view engine in to satisfy an attribute is the wrong trade for an API that has no views.

The explicit call is also strictly better on the response: the attribute answers a bad token with a 400 and **no body at all**, which the frontend renders as "something went wrong". Asking the service directly produces the same verdict with `{ code: "csrf_invalid", message: ... }`. Both POSTs call `RejectsCsrfAsync()` first, before the captcha and before any database work.

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

**What `lan` exposes.** Both launch profiles pin `ASPNETCORE_ENVIRONMENT=Development`, so `lan` also serves Swagger UI, `/openapi/v1.json` and the developer exception page to the network, and the backend has **no authentication scheme at all** (see "Auth is not set up at all"). Only `/auth/csrf`, `/auth/register` and `/auth/register/confirm` exist. That is acceptable on a trusted LAN and is not a public deployment.

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
- **Comments in project files are written in English** — code, config files and this rule itself. Russian stays where it belongs: this document, commit messages and anything that is prose for humans, not source. XML doc comments (`///`) are preferred over inline comments.
- Identifier naming is standard .NET (`FluxyDbContext`, not `FluxyContext`/`DBContext`); folder names are `Configurations/`, `Entities/`, `Context/`, `Migrations/`, `Repositories/`.
