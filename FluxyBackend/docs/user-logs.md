> Part of the FluxyBackend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).

## The user log: the audit trail behind every account action

The record of what was attempted on an account and what came of it - a registration, a sign-in, a
password reset, a profile change, a logout. The table, the vocabulary, the permission and the
fail-open write path arrived in the first phase; this phase hooks every flow into it and adds the
read surface an administrator uses.

### Stored shape — `user_logs`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `uuid` | Client-generated, from `AuditableEntity`. |
| `user_id` | `uuid NULL` | FK → `users.id` **`ON DELETE SET NULL`**, never cascade. |
| `record_type` | `smallint NOT NULL` | `UserLogRecordType`, `HasConversion<short>()`. |
| `is_success` | `boolean NOT NULL` | A stored fact, **not derived** from the record type. |
| `description` | `text NOT NULL` | The English sentence; never a password, code or token. |
| `ip` | `varchar(45) NULL` | Longest textual IPv6 form. |
| `isp` | `varchar(255) NULL` | The GeoIP organization. |
| `country_code` | `varchar(2) NULL` | Upper-case ISO 3166-1 alpha-2. |
| `user_agent` | `varchar(512) NULL` | Truncated at the write path. |
| `created_at`, `updated_at` | `timestamptz NOT NULL` | Stamped by the context, as everywhere. |

Indexes: `(created_at DESC, id DESC)` for the default order and its tie-break,
`(user_id, created_at)` for one account's history, and `(record_type)` for the record-type filter
and the second sortable column. No column carries `HasDefaultValue`.

**The account's key nulls rather than cascades.** The log is evidence about what was attempted
against an account; deleting the account may make the subject unknown, it may not erase the
evidence. The description already names the account as it read at write time, so the row stays
legible with a null `user_id`.

### Record types — the wire vocabulary

`Fluxy.Core/Users/Enums/UserLogRecordType.cs` holds ten members; the wire (filter and response)
name is snake_case and parsed case-insensitively as plain text:

| Member | Wire | Value |
| --- | --- | --- |
| `RegistrationSubmit` | `registration_submit` | 0 |
| `RegistrationConfirm` | `registration_confirm` | 1 |
| `Login` | `login` | 2 |
| `PasswordResetRequest` | `password_reset_request` | 3 |
| `PasswordResetConfirm` | `password_reset_confirm` | 4 |
| `UsernameChange` | `username_change` | 5 |
| `EmailChange` | `email_change` | 6 |
| `PasswordChange` | `password_change` | 7 |
| `LoginGuardChange` | `login_guard_change` | 8 |
| `Logout` | `logout` | 9 |

`Fluxy.Core/Users/Utils/UserLogRecordTypes.cs` is the single table between the two spellings
(`TryParseWire`, `WireName`) and is what a refusal names. The values are schema: they are already
written into rows and must not be renumbered.

### The permission

`UserPermission.ViewUserLogs = 268435456` (2^28), owned by `UserRole.Admin`, appended to
`UserPermissionCatalog.All` and the catalog's owner table. `AuthenticationExtensions` adds
`ViewUserLogsPolicy` ("Permission.ViewUserLogs") carrying **both** `RoleRequirement(Admin)` and
`PermissionRequirement(ViewUserLogs)`. There is no write permission: the log is written by the
flows it describes, never by an operator. **The read policy gates both endpoints** - reading the
log and wiping it - because the wipe destroys the same evidence, and the owner ratified that
single-grant decision rather than inventing an `editUserLogs` nobody asked to reason about.

**No permission backfill migration.** The catalog is code, and the seeder reconciles the
Administrators base group against it on every boot, so an installation that restarts receives the
new bit without a migration and without a frontend permission mirror changing.

### The description

`Fluxy.Core/Users/Utils/UserLogDescriptions.cs` owns both directions of the sentence:

- shape `"{FixedPhrase}: {subject}"` or `"{FixedPhrase}: {subject} | {detail}"`;
- the fixed phrases are `const`s on this type, so the vocabulary is closed and a phrase never
  contains `": "` and never interpolates user text before the separator;
- the subject is the identifier at write time - an account's login name, the address a
  confirmation was keyed by, or the identifier an unknown account typed;
- `Sanitize` strips control characters and `|`, trims, truncates to `SubjectMaxLength = 64`, and
  never answers an empty string (`(empty)`);
- `TryGetSubject` is the exact inverse: text after the first `": "` up to the first `" | "` or the
  end. The round trip is pinned by unit tests;
- `DescriptionMaxLength = 500`; the detail is shortened first, then the subject.

The phrases are grouped by flow: `registration_submit`, `registration_confirm`, `login`,
`password_reset_request`, `password_reset_confirm`, the three profile changes by kind
(`username`/`email`/`password`), `login_guard_change` and `logout`, each with a success phrase and
one phrase per refusal. The service that knows the local reason picks the phrase; the HTTP answer
stays what it always was (`invalid_credentials`, `invalid_code`, `already_exists`, ...).

A rename carries its two sides once it has happened:
`UserLogDescriptions.Renamed` writes `Describe(phrase, subject, $"{before} → {after}")`, and
the `Applied` and `Confirmed` rows of a username or email change pass the value the account
held before the change and the value it now holds. The `requested` row does not: the new value
is staged rather than written, so the sentence names the account and nothing else. No password,
one-time code or token is ever a subject or a detail: a permanent audit trail holding a credential
would be a credential nobody can rotate. **The profile flows never pass the current or the new
password to the builder** - the phrase names the change, the subject names the account.

### Writing it — where each flow hooks in

`IUserLogService.RecordAsync(NewUserLog)` is the one write path. It resolves the GeoIP snapshot
through `GeoIpMemo`/`IGeoIpResolver` (`country_code` = `CountryCode` upper-cased, `isp` =
`Organization`), truncates the optional strings to their column widths, stages the row through
`IUserLogRepository.AddAsync` and commits it with **its own** `IUnitOfWork.SaveChangesAsync`.

| Flow | Site | Rows |
| --- | --- | --- |
| H1 registration submit | `RegistrationService.RegisterAsync` | submitted, reuse-pending, and every refusal (`invalid input`, `already exists`, `mail not configured`, `email delivery`) |
| H1 duplicate race | `RegistrationController.Register` catch of `DuplicateRegistrationException` | one `already exists` row; the service threw before it could answer an outcome |
| H2 registration confirm | `RegistrationService.ConfirmAsync` | confirmed, wrong code, expired code |
| H3 login | `AuthenticationService.AuthenticateAsync` | succeeded; refused by no such account, wrong password, blocked, not registered, wrong role, guard |
| H4 password reset request | `PasswordResetService.RequestAsync` | requested, and every refusal (`mail not configured`, `invalid input`, `unknown pair`, `email delivery`) |
| H5 password reset confirm | `PasswordResetService.ConfirmAsync` | confirmed, wrong code, expired code |
| H6 profile change | `ProfileService.RequestAsync` + `ConfirmAsync` + `ChangeLoginGuardAsync` | one row per outcome, typed by the pending change's kind |
| H7 logout | `AuthenticationController.Logout` | always a success, `UserId` from the session claims |
| H8 login guard change | `ProfileService.ChangeLoginGuardAsync` + `ConfirmLoginGuardAsync` | user-side only; `UserAdminService` writes nothing |

- **The controllers pass the network.** `NewRegistration`, the two reset calls and the four
  profile calls carry `clientAddress`/`userAgent`; `NewCredentials` already did. `AuthControllerBase`
  exposes `ClientUserAgent` beside `ClientAddress` so the two are read the same way everywhere.
- **`Login` composes its description inside the service**, where the local reason is known, while
  the HTTP answer stays `invalid_credentials` for every refusal. The service also switched its
  direct `IGeoIpResolver` call to the request-scoped `GeoIpMemo`, so a guarded sign-in and its log
  row do one lookup.
- **`groupIds` matches the account's current group**, not the one it held at write time - a group
  is a property of the account.
- **A password reset's guard clear writes no `login_guard_change` row.** It is part of the reset,
  not a second decision the account made.
- **A time-zone change writes nothing.** It is a display preference, not a fact about the account
  (owner decision).
- **Only attempts that reach these services/controllers are logged.** CSRF, captcha and 429
  refusals happen before the service and write nothing (owner decision).
- **A concurrent staging request writes one row too.** The request that lost the unique index still
  reached the service and still sends the visitor to a working code, so it is recorded like the one
  that won - two attempts is what happened.

**An audit failure never changes the HTTP result.** The flows that call it have already committed
the operation being described; refusing a successful sign-in because the log table was unreachable
would turn an observability feature into an outage - the same fail-open answer the GeoIP lookup,
Redis and the mailer already give. The one exception is a caller-cancelled request, which is
rethrown because there is no response left to protect.

**A failed audit save drops its staged row.** A save that fails leaves the staged entity in the
request's change tracker, and the profile flow saves again after the service answers (it mints a
fresh token pair) - so without a cleanup the second save would retry the insert and turn a
successful change into a 500. `RecordAsync` calls `IUserLogRepository.DiscardStaged()` from its
catch; the repository detaches the row it staged. That is what makes "call `RecordAsync` at the
end of a flow" a convention rather than a correctness requirement.

### Reading it — `admin/user-logs`

| Endpoint | Auth | Answer |
| --- | --- | --- |
| `GET /admin/user-logs` | `viewUserLogs` | page: `page`, `pageSize`, `search`, repeated `userIds`/`groupIds`/`recordTypes`, `ip`, `country`, `isp`, `dateFrom`, `dateTo`, `sortBy`, `sortOrder` |
| `DELETE /admin/user-logs` | CSRF + `viewUserLogs` | `200 user_logs_cleared`; wipes the whole table |

`UserLogQuery` / `UserLogQueryText` resolve, in Core so they are testable without a host:

- `page` (default 1), `pageSize` (default 20, ceiling 1000), `search` (200 characters);
- `userIds`, `groupIds`, `recordTypes` as **repeated** query parameters, each element parsed on its
  own; one bad element refuses the whole filter under that parameter's name, and
  `MaxFilterValues = 100` is refused rather than trimmed;
- `ip`, `country`, `isp` - the three network filters, with the connection log's names and
  semantics, because `ConnectionNetworkCell`'s `onFilter` writes the same keys on either screen.
  `ip` and `isp` keep the stored value as it reads, case included (the row is a snapshot of the
  request, not a value to fold); a value longer than its column (`IpMaxLength = 45`,
  `IspMaxLength = 255`) is refused under its own name, because it can never equal a stored row and
  an empty page would look like a filter that simply matched nothing. `country` is canonicalised to
  upper-case ISO 3166-1 alpha-2 (`de` → `DE`, through `CountryCodes.Normalize`) and a value naming
  no code is refused under `country`. A blank value is "no filter" for all three; a null column
  never matches a bound;
- `dateFrom` / `dateTo`, ISO 8601, invariant culture, an absent offset read as UTC, either side
  optional, **both inclusive**. An unparseable bound is refused under its own name; a range whose
  `from` is later than its `to` is a valid query describing an empty result (200, not 400);
- `sortBy` (`createdAt` | `recordType`, default `createdAt`) and `sortOrder` (`asc` | `desc`,
  default `desc`).

Nothing is clamped. `total` is counted under the same predicate as the page, the order is
tie-broken by `id`, and paging/search/sort run in the repository.

**The response row** (`AdminUserLogItemResponse`): `id`, `accountId`, `username`, `recordType`,
`isSuccess`, `description`, `ip`, `isp`, `countryCode`, `createdAt`. `recordType` is the snake_case
wire name. `username` is resolved in three steps and the order is the contract:

1. the account's **current** login name from the left-joined `users` row, when the log row still
   names one - so a renamed account reads as its new name;
2. otherwise the identifier the writer typed, recovered from `description` through
   `UserLogDescriptions.TryGetSubject` - that is what keeps a refused sign-in for an unknown name
   readable as "someone tried `ghost`";
3. otherwise null.

`accountId` is null for a row that never named an account and for one whose account has been
deleted; `username` still answers in that case, from step 2.

**The clear ignores the screen's filters on purpose.** The button says "clear the user log", so the
whole table goes whatever the reader had narrowed it to; a delete that obeyed a filter the caller
happened not to send would wipe a different set from the one it named. An empty table answers `200`
with `0`, because the second press of a button that already did its job is not an error.
`ExecuteDelete` is immediate and outside the unit of work, which the interface's remarks say.
`IUserLogRepository.ClearAllAsync` is what performs it.

**Two REST departures, both owner-approved and recorded in the controller's remarks and in
AGENTS.md**: one permission gates the read and the wipe (there is no write permission at all), and
the wipe is a `DELETE` on the collection that removes every row regardless of the query string.

Layering follows the usual split: `UserLog` in `Fluxy.Core/Users/Models/`, `NewUserLog` in
`Commands/`, `UserLogListItem` / `UserLogPage` in `ReadModels/`, `UserLogRecordType` /
`UserLogSortField` / `UserLogSortOrder` in `Enums/`, `UserLogDescriptions` / `UserLogRecordTypes` /
`UserLogListLimits` / `UserLogQuery` / `UserLogQueryText` in `Utils/`, `IUserLogService` in
`Interfaces/`, `IUserLogRepository` in `Repositories/`; `UserLogService` in
`Fluxy.Application/Users/Services/`; the entity, configuration and repository in `Fluxy.DataAccess`;
the contracts in `Fluxy.API/Contracts/Users/`; `UserLogsController` in
`Fluxy.API/Controllers/Admin/`; migration `20261108082228_AddUserLogs`.

### Verified

**731** unit tests green (the descriptions, the query refusals including the three network filters,
the record-type wire round trip, the limit table, the numeric permission table, and the write path
including the detached row of a failed save) and **270** integration tests green (`UserLogTests`
pins a success and a failure row for every flow, the concurrent staging request's own row, the
`old → new` detail of a confirmed rename, the unknown-account row with a null `accountId` and the
typed identifier in the description, the absence of any password anywhere in the table, the default
newest-first order, the page/pageSize/search refusals under their own names, a bad filter element
refused under `userIds`/`groupIds`/`recordTypes`, an over-long `ip`/`isp` and an unknown
`country` refused under their own names, the exact `ip` and `isp` filters with their case preserved,
the canonicalised `country` with a null column never matching, the three network filters composed
with the existing ones, the inverted range as a valid empty query, a stable `total` across page
turns with disjoint pages, the composing filters including `groupIds` against the current group
only, the clear with its CSRF refusal and its empty-table answer, the permission matrix - Admin with
the grant reads, Admin without it gets `403 permission_denied`, Client and Reseller get
`403 auth_role_changed` - the purge of the guard log on a password reset, the absence of a
login-guard row for an admin edit, and a forced audit-write failure leaving the sign-in,
registration and profile answers untouched). `dotnet build` clean (0 warnings), `dotnet ef
migrations has-pending-model-changes` reports no changes.
