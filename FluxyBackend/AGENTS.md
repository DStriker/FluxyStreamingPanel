# AGENTS.md — FluxyBackend

ASP.NET Core **10** (`net10.0`) backend for the `Refluxy` monorepo. Git root is **one level up** (`..\`); sibling app is `..\FluxyFrontend` (has its own `AGENTS.md` — read it before touching anything frontend-facing). Monorepo `README.md` is a single line (`# Fluxy`) — no help there.

## Commands

```bash
dotnet build FluxyBackend.slnx                                 # 4 projects, ~4s, 0 warnings
dotnet run --project Fluxy.API --launch-profile http          # http://localhost:5159
dotnet run --project Fluxy.API --launch-profile https         # https://localhost:7221 + http://localhost:5159, opens /swagger
dotnet ef migrations add <Name> --project Fluxy.DataAccess    # scaffolds into Fluxy.DataAccess/Migrations
docker_run.bat                                                # generates .env if missing, then == docker compose up -d (run from FluxyBackend)
powershell -ExecutionPolicy Bypass -File scripts\init-secrets.ps1   # (re)generate secrets in .env; -Force regenerates all
```

- Needs the .NET 10 SDK (installed: 10.0.401). Solution uses the new XML `.slnx` format.
- **No test project, no `.editorconfig`, no lint/format config, no CI, no `Directory.Build.props`/`Directory.Packages.props` (no central package management).** Verification is `dotnet build` plus hitting the endpoint.
- Both launch profiles hardcode `ASPNETCORE_ENVIRONMENT=Development`, so the dev-only middleware is on even outside Visual Studio. Content root is `Fluxy.API/`, not the repo root — run docker from `FluxyBackend/`, not from the project folder.
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

## The API currently exposes zero endpoints

`Fluxy.API/Controllers/` is **empty**. `Program.cs` still calls `AddControllers()` and `MapControllers()`, but there are no controllers, so:

- Every path 404s, including `/weatherforecast/` and `/api/Test`. Verified against a running instance.
- `GET /openapi/v1.json` returns 200 with `"paths": {}` — the Swagger UI at `/swagger` loads but is **empty**. An empty `paths` object is expected, not a bug.
- `Fluxy.API/Fluxy.API.http` is **stale** — it requests `/weatherforecast/`, which no longer exists. Update it when you add the first endpoint.
- `Fluxy.API.csproj` still carries `<Folder Include="Controllers\" />`, and `.vs/` holds a stale `RefluxyBackend.slnx` alongside the current one. Both are harmless leftovers.

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

**Where the connection string comes from.** There is no `ConnectionStrings` section in either appsettings file. `Configuration/DotEnvConfigurationExtensions.cs` loads the compose `.env` and **composes** `ConnectionStrings:Postgres` from its `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` keys (`POSTGRES_HOST` / `POSTGRES_PORT` are honoured, defaulting to `localhost:5432`, which is right for a host process and wrong for a container — that would need the service name `postgres`). Consequences worth remembering:

- The file is searched **upwards from the content root, 3 levels** (content root is `Fluxy.API/`, the file is next to `compose.yaml`), and a missing file is not an error — the app only logs a warning.
- The source is **inserted at position 0** of `builder.Sources`, not appended. In .NET configuration the **last** provider that knows a key wins, so appending would have made `.env` the *highest* priority source and it would have overridden user-secrets. Verified both ways.
- Therefore anything in `appsettings.json`, user-secrets or environment variables wins, including a hand-written `ConnectionStrings:Postgres`. `scripts\init-secrets.ps1 -SeedUserSecrets` writes exactly that key, so after running it the stored secret overrides the value derived from `.env` — after a rotation either re-run the script or remove the key with `dotnet user-secrets --project Fluxy.API remove "ConnectionStrings:Postgres"`.
- An incomplete set of `POSTGRES_*` keys produces **no** connection string at all, on purpose: connecting with an empty password would be worse than the descriptive startup failure.
- The password is not quoted/escaped. It works because `init-secrets.ps1` generates hex; a secret containing `;` or `=` would need quoting.
- Deriving from the file also means `POSTGRES_*` supplied as real environment variables are **not** picked up — configure `ConnectionStrings:Postgres` directly in that setup.
- `dotnet ef` **10.0.12** is installed as a global tool (`dotnet tool install --global dotnet-ef --version 10.0.12`). Scaffolding works with no connection string at all thanks to the design time factory; redis is still not connected from the app — no StackExchange.Redis reference, nothing reads `ConnectionStrings:Redis`.

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

- **Only postgres is connected from the app** (see the section above). Redis still has no StackExchange.Redis reference and nothing reads `ConnectionStrings:Redis`; pgadmin is manual tooling. Don't assume a cache exists when writing code.
- compose bind-mounts `../2. Init Database` into `/docker-entrypoint-initdb.d`, but **that folder does not exist** in the repo, so Docker silently creates an empty directory in the parent and Postgres gets no bootstrap SQL.
- `UserSecretsId` is set — use `dotnet user-secrets --project Fluxy.API set "..."` rather than committing connection strings to appsettings. The app prefers it over the `.env`-derived value.
- Ports are on `0.0.0.0`, so the LAN can attempt every service. Password strength is the only control; nothing here is hardened for the public internet.

## Dev-mode behavior (verified by running)

- `MapOpenApi()` + Swagger UI (doc at `/openapi/v1.json`) + developer exception page + HTTP logging are **all inside `if (app.Environment.IsDevelopment())`**. Outside Development there is no API doc and no detailed error page.
- `app.UseHttpsRedirection()` is unconditional, but the `http` profile configures no HTTPS port, so the middleware logs "Failed to determine the https port for redirect" and passes http requests straight through (200, not a 307).
- **No CORS is configured**, and the frontend has no Vite dev proxy (it calls relative `/api/*`). Browser requests from `localhost:5173` will be blocked until a CORS policy is added here.

## Frontend contract that does not exist yet

`FluxyFrontend` already calls, none of which the backend implements:

- `POST /api/auth/{action}` with `{ username, password, captchaAction, captchaToken, email? }`, `credentials: 'include'`, headers `Content-Type: application/json`, `X-Recaptcha-Token`, `X-CSRF-Token`. Errors are read from `data.message`.
- `GET /api/auth/csrf` — the token is read from the `XSRF-TOKEN` cookie first, falling back to a JSON body `token` / `csrfToken`.
- The `action` in the path is **kebab-case** (`client-login`) while the page passes **snake_case** (`client_login`) as `captchaAction`. Keep both in mind.

See `FluxyFrontend/src/lib/api.js` and `csrf.js` for the exact shapes before inventing new ones.

## Conventions

- Block-scoped namespaces (`namespace X { ... }` with the type indented inside) and `using` directives at the top of the file — not file-scoped namespaces. Current root namespace is `Fluxy.API`; the old `Refluxy.*` rename is complete.
- `ImplicitUsings` and `Nullable` are enabled in all 4 projects.
- **Comments in project files are written in English** — code, config files and this rule itself. Russian stays where it belongs: this document, commit messages and anything that is prose for humans, not source. XML doc comments (`///`) are preferred over inline comments.
- Identifier naming is standard .NET (`FluxyDbContext`, not `FluxyContext`/`DBContext`); folder names are `Configurations/`, `Entities/`, `Context/`, `Migrations/`, `Repositories/`.
