> Part of the FluxyBackend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).

## Database backups: `admin/backups`

A directory of PostgreSQL archives on the API host, a scheduler that fills it, and one endpoint
that replaces the database with one of them.

| Endpoint | Auth | Answer |
| --- | --- | --- |
| `GET /admin/backups` | `viewSettings` | `{ "items": [ … ] }` newest first, unpaged |
| `POST /admin/backups` | CSRF + `editSettings` | `201 backup_created` + `Location: /admin/backups/{fileName}` |
| `POST /admin/backups/{fileName}/restore` | CSRF + `editSettings` | `200 backup_restored` |
| `DELETE /admin/backups/{fileName}` | CSRF + `editSettings` | `200 backup_deleted` |

Every read carries `Cache-Control: no-store`; every write checks the antiforgery pair **before**
it reads the body, the path or the directory.

### The two ratified decisions this surface rests on

**The settings grants are reused.** `ViewSettings` (`1L << 34`) admits the read of the directory,
`EditSettings` (`1L << 35`) admits every write - create, restore, delete. No `UserPermission`
member was added, no catalog changed, no migration written, and the frontend permission mirror
did not move. It is the same reuse the stream-problem journal and the MAG event journal state for
their own parents: **this surface is the settings surface's own safety net**, so the grants that
open the settings open its backups.

**The three backup settings are edited only on `/admin/settings`.** `backupEnabled`,
`backupPeriod` and `backupRotationCount` are read here and written nowhere: `PATCH /admin/settings`
is a full write of the whole row, so a second editing surface would send seventeen values it never
showed and clobber them. `/admin/backups` echoes the three read-only and links to the page that
owns them. There is no `PATCH /admin/backups/settings` and there must not be one.

### There is no metadata row, on purpose

An archive is a file whose **name** carries the instant it was taken. "Newest first" is a name
order, "has this period been backed up" is derived from the newest file, and "which files may be
rotated" is a pattern match. Nothing about the directory lives in the database.

That is not laziness about persistence - it is the only shape that survives the feature's own
operations:

- **A restore replaces the database.** A `backups` table would be rolled back to the state of the
  archive, so the record of every file written after it would vanish while the files stayed. The
  screen would then list nothing and the rotation would delete nothing.
- **The files outlive the process.** An archive is copied, moved and restored by hand; the name is
  the only description that travels with it.
- **There is nothing to reconcile.** One record, in one place, that cannot disagree with itself.

The cost is accepted and visible: `FileInfo.CreationTime` is **never** read. A copy rewrites it, a
remounted volume rewrites it, and an archive restored onto a fresh host would claim to be new.

### Naming

```
fluxy-yyyyMMddTHHmmssZ.dump
```

UTC, second precision, a literal `Z`. Lexicographic order equals chronological order, which is the
property the rotation and the "newest first" list both stand on.

- A collision inside one second is not an error: `-2` … `-9` is appended and the tenth attempt in
  the same second answers `backup_failed` rather than overwriting a file.
- `TryParse` reads the first sixteen characters of the stem as the instant and accepts a `-2`…`-9`
  suffix after it, ignoring the suffix for the timestamp. A suffixed name therefore still reports
  its own second, and a name that is not this format is not this feature's file.
- **One accept-set.** The list, the rotation, the restore and the delete all accept exactly the
  names `TryParse` recognises. A well-formed `.dump` an operator copied into the directory is not
  listed and not rotated, and addressing it answers the same `404 backup_not_found` an unknown
  name does - a file the feature did not write is never restored over the database or deleted.
- **Durability**: the tool writes `<finalName>.tmp`, the service renames it only after the tool has
  exited zero, and a failed run deletes the temporary file. The list and the rotation see only
  pattern-matching names, so a `.tmp` is invisible to both - a crash in the middle never looks like
  a backup.

### Rotation

After every successful create - scheduled or manual, one rule - the newest
`backupRotationCount` pattern-matching files are kept and the rest are deleted. `0` keeps every
archive. **A name that does not match the naming rule is never touched**: an operator's own copy,
a stray file, a hand-renamed dump. The rotation runs only once the new file is durable.

The order is ordinal descending. Because the collision suffix sits *before* the extension, a
suffixed name compares *below* the unsuffixed name of its own second (`.` is 0x2E, `-` is 0x2D) -
so within one second the plain name is kept first. That is a consequence of the ratified format,
not a second ordering rule.

### The dump: `docker exec`, and why

`pg_dump` is **not** on the host PATH. The container's client binaries match the server's major
version, which is the one constraint that matters: `pg_dump` refuses a server newer than itself and
`pg_restore` of an archive written by a newer client is unsupported. `postgres:18.6` ships 18.6
clients, so the host needs nothing but `docker.exe`.

```
docker exec <ContainerName> pg_dump -U <user> -d <database> -Fc \
  --exclude-table-data=user_logs --exclude-table-data=credit_logs \
  --exclude-table-data=connection_logs --exclude-table-data=api_request_failures \
  --exclude-table-data=stream_problems --exclude-table-data=mag_events
```

- The user and the database are parsed from the **application's own connection string**
  (`NpgsqlConnectionStringBuilder`), because a dump of `postgres` would be a backup of nothing and
  the mistake would not appear until the day it was needed.
- **No password is ever passed or logged.** `docker exec` reaches the server over the container's
  unix socket, whose `pg_hba.conf` entry is `trust`; the TCP entry is the password-protected one and
  is deliberately not used.
- The archive is read from `stdout.BaseStream` and written straight to the file. A string read
  would decode and re-encode a binary format and corrupt it.
- `ContainerName` is `Backups:ContainerName` (default `postgres`). A missing `docker`, an
  unreachable daemon, a missing container or a run past its timeout is `backup_tool_unavailable` -
  **an explicit refusal, never a silent skip**. A non-zero `pg_dump` exit is `backup_failed`.

**The six excluded tables keep their rows out of every dump; the tables themselves stay in it**
and are recreated empty. Their data is the largest and least valuable part of the database - a
connection, a credit movement or a refused request that has already been handled - while the
catalogues beside them are the installation. `refresh_tokens`, `pending_changes`,
`user_login_guard_rules`, `tickets`, `ticket_replies`, `epg_data`, `__EFMigrationsHistory` and
every catalogue **are** in the dump.

**`pg_dump` silently ignores an `--exclude-table-data` name that matches no table, with exit 0.**
That is why two tests exist: a unit test pins the catalogue to exactly six arguments, and an
integration test asks the live schema whether all six names exist.

### The restore: a ratified departure, and an accepted outage

`POST /admin/backups/{fileName}/restore` is a **state transition of the whole database**, not a
write to a resource. `PUT {fileName}` meaning "make the database this file" would dress
destruction-and-replacement as an update of an archive the client can never edit. The reasoning
family is `POST {id}/confirm-registration|block|unblock` under `admin/users`.

The sequence, in order:

1. the single-flight gate (a held gate answers `409 backup_busy`);
2. the name is validated and resolved - an invalid name, a traversal spelling and a name that
   resolves to nothing all answer the same `404 backup_not_found`;
3. `NpgsqlConnection.ClearAllPools()` inside the tool - `pg_restore --clean` has to drop objects no
   live session still holds, and this process's pooled connections are exactly that;
4. `docker exec -i <ContainerName> pg_restore -U <user> -d <database> --no-owner --clean --if-exists --single-transaction`,
   the archive on **stdin**, so it never has to be copied into the container;
5. a scope is opened and `IDatabaseInitializer.MigrateAsync` runs - the archive's schema is from the
   moment it was taken, so the pending migrations make it current again;
6. the settings snapshot is reloaded, because the settings row was inside the dump.

**The outage is accepted and global.** Requests served during the run fail; the caller of the
restore is answered only once the database is current again.

**Redis is not restored, and sessions roll back to the backup instant.** `refresh_tokens` is in the
dump, so everyone who signed in after the backup was taken is effectively signed out - possibly the
operator who pressed the button. The attempt counters and the revoked-session identifiers in Redis
stay where they were. Both facts are documented rather than mitigated, and the screen says them
before the click.

### Scheduling

`BackupScheduler` is a `BackgroundService` registered in `Program.cs`. It is inert in two ways and
one rule decides when it acts:

- **`Testing` returns before the loop.** The integration suite's host never takes a dump.
- **`BackupEnabled == false` skips the tick.** The setting is the operator's switch.
- **Due means "this period bucket holds no archive"**, read from the newest file's own name -
  never an interval, never a stored "last run". Hourly = same UTC date + hour, Daily = same UTC
  date, Weekly = same ISO week-year + ISO week (`ISOWeek`, so the Dec/Jan boundary is correct),
  Monthly = same year + month. A host that was down over midnight takes today's backup on its first
  tick; one that restarts three times still takes one.
- The delay is awaited through the injected `TimeProvider`, and each tick is wrapped: an exception
  out of `ExecuteAsync` stops the host in .NET 6+, so a failed dump is logged and the next tick
  retries.
- `Backups:TickSeconds` (default `60`) is how long one tick waits.

A manual create and a scheduled one share one rule: both write a file, both rotate, and the
scheduler counts a manual archive as this period's. There is no third place that remembers
anything.

### The codes

| Code | Status | Meaning |
| --- | --- | --- |
| `backup_created` | 201 | the archive was written and rotation ran |
| `backup_restored` | 200 | the database was replaced and migrated |
| `backup_deleted` | 200 | the file was removed |
| `backup_not_found` | 404 | the name is not one this feature writes, or is not here - deliberately indistinguishable |
| `backup_busy` | 409 | another write holds the gate |
| `backup_tool_unavailable` | 500 | docker, the daemon or the container could not be reached |
| `backup_failed` | 500 | the tool ran and failed, or a file could not be written |
| `csrf_invalid` | 400 | the antiforgery pair did not validate |
| `auth_required` | 401 | no session |
| `permission_denied` | 403 | the group holds neither `viewSettings` nor `editSettings` |
| `method_not_allowed` | 405 | a method the resource does not own |

### The 405 fences

Two actions declare the methods the resource does not own, and neither reads the identifier or the
body:

- the **collection**: `PUT` and `PATCH` (there is no set to replace);
- a **file**: `GET`, `PUT`, `PATCH` and `POST {fileName}` without `/restore` (an archive is produced
  by the tool; it is never read, replaced or edited, and its only two transitions are the restore
  and the delete, each with a route of its own).

### The script

`scripts/backup-db.ps1` is a **thin caller**: `POST /auth/admin-login` → `GET /auth/csrf` →
`POST /admin/backups`, with `FLUXY_ADMIN_PASSWORD` for the password (the frontend probes'
convention) and the antiforgery token sent as `X-CSRF-Token`. It holds no naming rule and no
rotation logic; both live on the server and a second copy here would drift.

```powershell
$env:FLUXY_ADMIN_PASSWORD = '<the administrator password>'
powershell -ExecutionPolicy Bypass -File scripts\backup-db.ps1
```

It takes `-BaseUrl` (default `http://localhost:5159`) and `-Username` (default `admin`).

### Where the code lives

`Fluxy.Core/Backups/` — `Options/BackupsOptions`, `ReadModels/BackupFileInfo`,
`Enums/BackupFailure`, `Outcomes/BackupOutcome`, `Interfaces/IBackupService`,
`Utils/BackupFileName`, `Utils/BackupFileNameRules`, `Utils/BackupTableExclusions`.

`Fluxy.Core/Persistence/` — `Interfaces/IDatabaseBackupTool`, `Outcomes/BackupToolResult`, beside
`IDatabaseInitializer`: the mechanism is the database lifecycle, not a repository.

`Fluxy.Application/Backups/` — `Services/BackupService` (singleton, the gate), `Services/BackupScheduler`
(`BackgroundService`), `Utils/BackupRotation`, `Utils/BackupDuePolicy`.

`Fluxy.DataAccess/Infrastructure/Backups/PostgresBackupTool` — the only place that names `docker`,
`pg_dump`, `pg_restore` and Npgsql's `ClearAllPools`.

`Fluxy.API/Contracts/Backups/` and `Fluxy.API/Controllers/Admin/BackupsController`; the options
binding and the hosted-service registration in `Program.cs`; the section in `appsettings.json`.

The frontend: `src/features/backups/` (`api.ts`, `types/`, `hooks/`, `size.ts`),
`src/pages/admin/BackupsPage.tsx`, and the `backups` item of the admin `overview` group in
`src/utils/navigation.ts`.

### Verified by running

The unit suite (`Backups/BackupFileNameTests`, `BackupFileNameRulesTests`, `BackupRotationTests`,
`BackupDuePolicyTests`, `BackupTableExclusionsTests`, `BackupSchedulerTests`) and the integration
suite (`Backups/BackupsTests`: the anonymous refusals, the list's shape and order, the three
successes, the traversal/unknown-name matrix, the well-formed-but-foreign file that is not
addressable, `backup_busy`, `backup_tool_unavailable`, `backup_failed` with no leftover file, CSRF
on every write, all 405 fences, the read-vs-write grant enforcement, and the six-table existence
guard). `dotnet ef migrations has-pending-model-changes` reports no changes: the feature adds no
entity.

### Manual smoke record

Recorded on the developer machine, `http` profile (`http://localhost:5159`), against the live
`postgres` container (PostgreSQL 18.6, `pg_dump (PostgreSQL) 18.6`):

1. **Create.** `scripts/backup-db.ps1 -Username probe_temp` (the throwaway administrator of
   `..\FluxyFrontend\docs\probes.md`) answered `Created fluxy-20261123T134534Z.dump`; the file
   appeared in `Fluxy.API/backups/` at 126,694 bytes and its first five bytes were `PGDMP` -
   the custom-format header, so the stdout capture is byte-exact and not text-mangled.
2. **Restore, into a scratch database - never over `fluxy`.** The archive was copied into the
   container and restored with `pg_restore --no-owner --exit-on-error` into a freshly created
   `fluxy_scratch`: exit 0. The catalogue came through (`users` 10, `video_streams` 9) and the
   six excluded logs were present but empty (`user_logs`, `credit_logs`, `connection_logs`,
   `api_request_failures`, `stream_problems`, `mag_events` - all 0), while the live database held
   156 / 1 / 6 / 12 / 12 / 3 rows of the same tables. That is the exclusion rule proved end to
   end, not just asserted against the catalogue.
3. **Cleanup.** `fluxy_scratch` dropped, the container's `/tmp/smoke.dump` removed, the throwaway
   account deleted, the smoke archive removed from `Fluxy.API/backups/` (only `.gitkeep` remains).
4. **Banner and dialog.** `/admin/backups` was opened in headless Edge against the running dev
   server as the throwaway administrator and captured under `.tmp-runtime/`. The capture showed
   the card title, the toolbar's "Create backup now", the read-only echo (Off / Daily / 10 with
   the "0 keeps every backup" hint), the link to the settings page, and one row with its name,
   `124 KB`, a local timestamp and the two icon actions. The restore dialog was captured too: it
   names all four facts (the database is replaced, everyone signed in after the backup is signed
   out, log rows written since are lost, a short interruption).
5. **The archive-on-stdin path was proved separately**, because that is the half the fake tool
   never exercises: `docker exec postgres pg_dump … > file` and then
   `docker exec -i postgres pg_restore -U fluxy -d fluxy_stdin --no-owner --clean --if-exists
   --single-transaction < file` both exited 0 and the scratch database came up with its rows.
   **No `docker cp` fallback is needed**: stdin is usable here, so the implementation keeps the
   simpler shape (bypassing the file copy also means no temporary archive in the container).

Validation output of that run:

- `dotnet build FluxyBackend.slnx` - 0 warnings, 0 errors.
- `dotnet test --project tests\Fluxy.UnitTests` - 1314 passed, 0 failed.
- `dotnet test --project tests\Fluxy.IntegrationTests` - 420 passed, 0 failed.
- `dotnet ef migrations has-pending-model-changes --project Fluxy.DataAccess` - "No changes have
  been made to the model since the last migration."
- `npm run typecheck`, `npm run lint`, `npm test` (356 passed), `npm run build`,
  `npm run probe:render` ("all render checks passed"), `npm run probe:i18n` (all 1483 literal
  keys resolve in both locales), `npm run probe:paths` (both prefixes agree with the proxy).

> **Environment note.** This machine has no usable `10.0.4xx` SDK - the `10.0.401` folder is a lone
> `Roslyn` stub and `dotnet --list-sdks` reports only `5.0.416` and `11.0.100` - so
> `FluxyBackend/global.json` carries its `10.0.401` pin with
> `rollForward: latestMajor`, and the SDK resolves to `11.0.100` here. That is part of the
> repository, not a temporary edit - the backend commands above were re-run against the file as
> committed, with the same results.

