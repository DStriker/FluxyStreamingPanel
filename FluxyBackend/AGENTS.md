# AGENTS.md — FluxyBackend

ASP.NET Core **10** (`net10.0`) backend for the `Refluxy` monorepo. Git root is **one level up** (`..\`); sibling app is `..\FluxyFrontend` (has its own `AGENTS.md` — read it before touching anything frontend-facing). Monorepo `README.md` is a single line (`# Fluxy`) — no help there.

## Commands

```bash
dotnet build FluxyBackend.slnx                                 # 4 projects, ~4s, 0 warnings
dotnet run --project Fluxy.API --launch-profile http          # http://localhost:5159
dotnet run --project Fluxy.API --launch-profile https         # https://localhost:7221 + http://localhost:5159, opens /swagger
docker_run.bat                                                # generates .env if missing, then == docker compose up -d (run from FluxyBackend)
powershell -ExecutionPolicy Bypass -File scripts\init-secrets.ps1   # (re)generate secrets in .env; -Force regenerates all
```

- Needs the .NET 10 SDK (installed: 10.0.401). Solution uses the new XML `.slnx` format.
- **No test project, no `.editorconfig`, no lint/format config, no CI, no `Directory.Build.props`/`Directory.Packages.props` (no central package management).** Verification is `dotnet build` plus hitting the endpoint.
- Both launch profiles hardcode `ASPNETCORE_ENVIRONMENT=Development`, so the dev-only middleware is on even outside Visual Studio. Content root is `Fluxy.API/`, not the repo root — run docker from `FluxyBackend/`, not from the project folder.
- Single branch, commit directly. Don't open branches or PRs.
- `git` 2.43.0 is on PATH via the **user** PATH entry `C:\Users\user\Tools\Git\cmd` (a copy of the portable git that ships inside `C:\Program Files\BeefLang\bin\Git`; `msys64` has no git). It works on the repo one level up. New shells pick it up; a shell started before the change needs a restart.

## Intended architecture — the 3 layer projects are still almost empty

`FluxyBackend.slnx` contains **4** projects. The `ProjectReference` graph below is already wired, but `Fluxy.Core` and `Fluxy.Application` still have **zero `.cs` files**. `Fluxy.DataAccess` holds the `DbContext` and the `AddDataAccess` DI extension; `Fluxy.API` holds the `Program.cs` wiring and `Configuration/DotEnvConfigurationExtensions.cs`.

The dependency graph (confirmed by the owner — this is the contract, not a guess):

```
Fluxy.API ──┬──> Fluxy.Application ──┐
            │                        ├──> Fluxy.Core
            └──> Fluxy.DataAccess ───┘
```

- **Fluxy.API** — controllers, request/response DTOs (contracts), configuration.
- **Fluxy.Application** — services and business logic. References Core **and** DataAccess.
- **Fluxy.Core** — immutable business models and interfaces. Depends on nothing.
- **Fluxy.DataAccess** — PostgreSQL infrastructure: `Context/FluxyDbContext.cs`, `ServiceCollectionExtensions.cs` (`AddDataAccess`), plus the planned `Configurations/` (table configs), `Entities/`, `Migrations/`, `Repositories/` (data-access logic). Folders are declared explicitly as `<Folder Include>` in the csproj, because empty folders are invisible to git.

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
- `Fluxy.DataAccess/Context/FluxyDbContext.cs` — plain `DbContext`, only a `DbContextOptions<FluxyDbContext>` constructor, and `OnModelCreating` that applies all `IEntityTypeConfiguration` from its own assembly. **No `DbSet` and no entity types yet** (`Model.GetEntityTypes()` is empty), so there is no schema and `Fluxy.DataAccess/Migrations` is still empty.
- `Fluxy.DataAccess/ServiceCollectionExtensions.cs` — `services.AddDataAccess(configuration)` reads `ConnectionStrings:Postgres` and calls `UseNpgsql`. It **throws `InvalidOperationException` at startup** when the connection string is missing, with a message naming the three ways to provide it. That is the intended fail-fast, not a bug to silence.
- `Program.cs` calls `builder.Services.AddDataAccess(builder.Configuration)` — the API layer never names Npgsql or EF Core.

**Where the connection string comes from.** There is no `ConnectionStrings` section in either appsettings file. `Configuration/DotEnvConfigurationExtensions.cs` loads the compose `.env` and **composes** `ConnectionStrings:Postgres` from its `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` keys (`POSTGRES_HOST` / `POSTGRES_PORT` are honoured, defaulting to `localhost:5432`, which is right for a host process and wrong for a container — that would need the service name `postgres`). Consequences worth remembering:

- The file is searched **upwards from the content root, 3 levels** (content root is `Fluxy.API/`, the file is next to `compose.yaml`), and a missing file is not an error — the app only logs a warning.
- The source is **inserted at position 0** of `builder.Sources`, not appended. In .NET configuration the **last** provider that knows a key wins, so appending would have made `.env` the *highest* priority source and it would have overridden user-secrets. Verified both ways.
- Therefore anything in `appsettings.json`, user-secrets or environment variables wins, including a hand-written `ConnectionStrings:Postgres`. `scripts\init-secrets.ps1 -SeedUserSecrets` writes exactly that key, so after running it the stored secret overrides the value derived from `.env` — after a rotation either re-run the script or remove the key with `dotnet user-secrets --project Fluxy.API remove "ConnectionStrings:Postgres"`.
- An incomplete set of `POSTGRES_*` keys produces **no** connection string at all, on purpose: connecting with an empty password would be worse than the descriptive startup failure.
- The password is not quoted/escaped. It works because `init-secrets.ps1` generates hex; a secret containing `;` or `=` would need quoting.
- Deriving from the file also means `POSTGRES_*` supplied as real environment variables are **not** picked up — configure `ConnectionStrings:Postgres` directly in that setup.
- `dotnet ef` is **not installed** (`dotnet tool install --global dotnet-ef` is required before the first migration), and redis is still not connected from the app — no StackExchange.Redis reference, nothing reads `ConnectionStrings:Redis`.

Verifying the wiring without touching the database: `dotnet build` plus resolving the context (`provider: Npgsql.EntityFrameworkCore.PostgreSQL`, `NpgsqlConnection` in state `Closed`) is enough — a `DbContext` opens no connection on construction. No request currently uses the context, so the running app proves nothing about it on its own.

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
- There is no schema yet: EF Core migrations in `Fluxy.DataAccess/Migrations` are the intended path.

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
