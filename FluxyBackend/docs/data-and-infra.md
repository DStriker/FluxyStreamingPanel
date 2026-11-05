> Part of the FluxyBackend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).

## PostgreSQL is wired up (EF Core 10, Npgsql provider)

The app talks to the compose postgres, to the already existing `fluxy` database, through EF Core:

- `Fluxy.DataAccess` references `Npgsql.EntityFrameworkCore.PostgreSQL` **10.0.3**, `Microsoft.EntityFrameworkCore` **10.0.12** (pinned explicitly) and `Microsoft.EntityFrameworkCore.Design` **10.0.12** (`PrivateAssets=all`). The explicit EF reference is not redundant: the provider only requires EF `>= 10.0.4`, so without it NuGet resolved 10.0.4 against the 10.0.12 of Design and the build failed with **MSB3277**. Never use the 11.x line of the provider — it targets EF Core 11.
- **The MSB3277 trap now belongs to `Fluxy.API`.** Because Design is `PrivateAssets=all`, the Relational 10.0.12 it drags into DataAccess does not flow to consumers, so the API would resolve Relational 10.0.4 against the 10.0.12 of `Fluxy.DataAccess.dll`. Fixed by pinning `Microsoft.EntityFrameworkCore.Relational` **10.0.12** explicitly in `Fluxy.API.csproj`. `Fluxy.Application` references Core only and has no EF package. **Any new project that references `Fluxy.DataAccess` must carry the same pin**, otherwise it builds with MSB3277 and then throws a confusing `ServiceProvider` failure at runtime.
- `Fluxy.DataAccess/Context/FluxyDbContext.cs` — plain **internal** `DbContext`, one `DbContextOptions<FluxyDbContext>` constructor, its own `DbSet`s, both `SaveChanges` overloads stamping the audit columns, and `OnModelCreating` that applies all `IEntityTypeConfiguration` from its own assembly and then the entity conventions (see "Domain rules" in AGENTS.md). Internal is what makes "Application cannot reach EF" a compiler rule rather than a habit.
- `Fluxy.DataAccess/Context/FluxyDbContextFactory.cs` — internal `IDesignTimeDbContextFactory`, so `dotnet ef` works without starting the application, and still discovers the factory when it is not public. The connection string only selects the provider and is never used to connect; it reads `ConnectionStrings__Postgres` and otherwise falls back to a placeholder. Set that variable for `dotnet ef database update`, otherwise the command tries the placeholder credentials.
- `Fluxy.DataAccess/ServiceCollectionExtensions.cs` — `services.AddDataAccess(configuration)` reads `ConnectionStrings:Postgres`, calls `UseNpgsql`, and registers every repository plus `IUnitOfWork` and `IDatabaseInitializer` Scoped. It **throws `InvalidOperationException` at startup** when the connection string is missing, with a message naming the three ways to provide it — the intended fail-fast, not a bug to silence.
- `Program.cs` calls `builder.Services.AddDataAccess(builder.Configuration)` — the API layer never names Npgsql or EF Core, and never names a repository implementation.

### Redis is now connected (StackExchange.Redis 3.3.1)

Only `Fluxy.DataAccess` references **StackExchange.Redis 3.3.1**. `Fluxy.Application` knows redis only through the `IAttemptThrottle` and `ITokenRevocationStore` interfaces in Core; implementations live in `Fluxy.DataAccess/Infrastructure/Redis/`.

- `Fluxy.DataAccess/ServiceCollectionExtensions.cs` has `AddRedis(configuration)`, which reads `ConnectionStrings:Redis`, parses it at startup (`ConfigurationOptions.Parse`) so a malformed value fails loudly, and registers a **singleton** `IConnectionMultiplexer` when the string exists. It forces `AbortOnConnectFail = false`, so a redis that is not running does **not** prevent the host from starting.
- `AddRedis` also registers `RedisConnection` and both stores **whether or not a connection string exists**. `RedisConnection` is the one place that holds the nullable multiplexer and answers `TryGetDatabase`; a nullable constructor parameter on each store would not be optional to Microsoft.Extensions.DependencyInjection, so without this wrapper a missing connection string would fail on the first request that resolves a store instead of degrading.
- `AddDerivedRedisConnectionString` in `DotEnvConfigurationExtensions.cs` mirrors `AddDerivedPostgresConnectionString`: it composes `ConnectionStrings:Redis` from `REDIS_USER` / `REDIS_PASSWORD` / `REDIS_HOST` / `REDIS_PORT` in the `.env` file, defaulting to `localhost:6379`. The `user=` segment is **mandatory** because compose runs redis with `user default off`.
- `RedisAttemptThrottle` (DataAccess/Infrastructure/Redis) uses a Lua script (`INCR` + `PEXPIRE` on first) so several instances share the window. It **falls back to a `ConcurrentDictionary` in process memory** when redis is unreachable, so registration keeps working even with redis down.
- `RedisTokenRevocationStore` (same folder) keeps revoked session identifiers for `Auth:AccessLifetime` plus the 30 s clock skew and **fails open** when redis is unreachable.
- **The `Redis:Required` config key (`true`/`false`) controls whether a missing connection string is fatal.** It defaults to non-fatal; `false` leaves the stores registered with a null connection, which is the documented degraded state.

### The GeoIP databases live in the data access layer (MaxMind.GeoIP2 6.1.0)

Only `Fluxy.DataAccess` references **MaxMind.GeoIP2 6.1.0**. The reader of a data source is a mechanism, not a business rule, so `MaxMindGeoIpResolver` and `GeoIpOptions` live in `Fluxy.DataAccess/Infrastructure/GeoIp/`, behind `IGeoIpResolver` from Core — the same shape as the redis stores.

- `Fluxy.DataAccess/ServiceCollectionExtensions.cs` has `AddGeoIp(configuration)`, which binds the `GeoIp` section and registers the resolver as a **singleton**: readers are opened once and on first use rather than at startup, so a host without the files still starts and every address resolves as unknown.
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

## Infrastructure (compose.yaml) and secrets

`docker compose up -d` from `FluxyBackend/` starts postgres (5432), pgadmin (5050) and redis (6379). All three publish their ports on `0.0.0.0` on purpose — reachable from the LAN, so every service authenticates with its own strong password.

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
