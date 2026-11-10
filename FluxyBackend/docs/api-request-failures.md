> Part of the FluxyBackend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).

## The API request failure log: every public-API request that was refused

The record of a request the public API turned away - which subscription data asked for what
content, why it was refused, and from where. **The capture belongs to the future public API and is
not part of this capability.** What exists here is the storage shape, the closed vocabulary, the
single permission, a read surface an administrator uses, one whole-table clear, and a Development
seeder that gives an empty table something to draw. The write-path fence in the middle of this file
is the point: nothing in this pass records a refusal.

### Stored shape — `api_request_failures`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `uuid` | Client-generated, from `AuditableEntity` (`ValueGenerated.Never`). |
| `subscription_data` | `varchar(512) NULL` | Free-form access parameter. **Never a subscription foreign key.** |
| `content_kind` | `smallint NULL` | `ContentType`, `HasConversion<short>()` (`VideoStream` = 1 … `Series` = 4). |
| `content_id` | `uuid NULL` | Opaque identifier. **No foreign key** - the row may name content this installation never recorded. |
| `content_name` | `varchar(255) NULL` | Captured at write time; never resolved or recomputed on read. |
| `reason` | `smallint NOT NULL` | `ApiRequestFailureReason`, `HasConversion<short>()`. |
| `ip` | `varchar(45) NOT NULL` | Longest textual IPv6 form. |
| `isp` | `varchar(255) NULL` | The GeoIP organization, as stored. |
| `country_code` | `varchar(2) NULL` | Upper-case ISO 3166-1 alpha-2. |
| `description` | `varchar(1000) NOT NULL` | The English sentence a support call reads. |
| `created_at`, `updated_at` | `timestamptz NOT NULL` | Stamped by the context, as everywhere. |

Five indexes, one per question the screen asks: `(created_at DESC, id DESC)` for the default order
and the tie-break every page turn depends on, `(reason)` for the filter and the second sortable
column, and `(ip)`, `(country_code)`, `(isp)` for the three exact network filters the shared cell
writes. `ip`, `isp` and `country_code` are the same three facts the connection log stores, and the
indexes make each exact filter one index read rather than a scan of a table that grows forever. No
column carries `HasDefaultValue`.

**The content snapshot is a group: a writer sets `content_kind`, `content_id` and `content_name`
together or leaves all three empty**, so a refusal that named no content draws a dash rather than an
empty gap. Neither the content columns nor `subscription_data` are foreign keys, and they are not
foreign keys on purpose: a request naming an identifier this installation has no subscription for,
or content it never recorded, is exactly the refusal worth keeping. Deleting the content later
cannot touch the row, and the stored name keeps it legible.

**Every width comes from `ApiRequestFailureListLimits`** (`SubscriptionDataMaxLength = 512`,
`ContentNameMaxLength = 255`, `DescriptionMaxLength = 1000`, `IpMaxLength = 45`,
`IspMaxLength = 255`, `CountryCodeLength = 2`), and the query parser refuses an oversized filter
value against the same constants - so a column width and the rule that refuses a value which cannot
fit cannot drift apart.

### Reasons — the wire vocabulary

`Fluxy.Core/Requests/Enums/ApiRequestFailureReason.cs` holds six members. The numeric values are
**schema and append-only**: they are already written into rows as a `smallint`, so a member is
added at the end and never renumbered. The wire name - the filter and the response spelling - is
snake_case, parsed case-insensitively and whitespace-tolerantly:

| Member | Wire | Value |
| --- | --- | --- |
| `UnknownId` | `unknown_id` | 0 |
| `GeoIpViolation` | `geo_ip_violation` | 1 |
| `AuthFailed` | `auth_failed` | 2 |
| `TooManyConnections` | `too_many_connections` | 3 |
| `SubscriptionExpired` | `subscription_expired` | 4 |
| `UserBlocked` | `user_blocked` | 5 |

`Fluxy.Core/Requests/Utils/ApiRequestFailureReasons.cs` is the single table between the two
spellings (`TryParseWire`, `WireName`, `All`, and the `UnknownReasonHint` built from `All` rather
than written out). The reason table lives in Core, not in the transport, because the unit test
project references Core and Application only. **These six names and values are the fixed vocabulary
the future public-API writer consumes rather than re-decides**: introducing a capture path must not
rename `geo_ip_violation` or renumber `user_blocked`.

### The permission

`UserPermission.ViewApiRequestFailures = 536870912` (2^29), owned by `UserRole.Admin`, appended to
`UserPermissionCatalog.All` and the catalog's owner table. `AuthenticationExtensions` adds
`ViewApiRequestFailuresPolicy` ("Permission.ViewApiRequestFailures") carrying **both**
`RoleRequirement(Admin)` and `PermissionRequirement(ViewApiRequestFailures)`; the controller also
carries the area-wide `AdminPolicy`.

**One grant gates both endpoints, and that is the deliberate departure** - the user-logs argument
restated rather than assumed. Reading the log is `viewApiRequestFailures`; wiping it destroys the
same evidence, so it carries the same grant plus a fresh antiforgery pair rather than inventing an
`editApiRequestFailures` nobody asked to reason about. There is no write permission for this log at
all: a row is written by the public API's future capture path, never by an operator, and the only
write this surface performs is the total erase.

**No permission backfill migration.** The catalog is code, and `DefaultUserSeeder` reconciles the
base groups against it on every boot (`ReconcilePermissionsAsync`), so an installation that
restarts receives the new bit without a migration. The frontend's own mirror in
`src/features/user-groups/permissions.ts` (`ALL_PERMISSIONS`, `OWNER`) and `src/types/UserPermission.ts`
is updated in the same change, because a permission the server knows and the form cannot offer is a
grant nobody can see.

### The write-path fence

**This capability contains no production write, and that absence is the fence.** A `POST`, a
`PUT` or a `PATCH` on this resource would be a route with no rule behind it, so those routes are not
declared at all - and each of them answers `405 method_not_allowed` through the shared status-page
envelope, proven at the wire by an integration test.

- `ApiRequestFailureEntity.ToModel`/`FromModel` exist and are **reserved for the future capture
  path**; their remarks say so and nothing calls them today (the `ConnectionLogEntity`
  treatment). They stay beside each other because a mapping written later would be a second
  opinion about what the columns mean.
- `IApiRequestFailureRepository.AddRangeAsync` is the one insert intent, and its **only production
  caller is the Development seeder** (the integration suite's `TestDataSeeder` plants through the
  same contract). The application layer may not reach an internal persistence type, so the seeder
  stages through this contract and commits through `IUnitOfWork`.
- `IApiRequestFailureService` has no `Record`/`Save` method: adding one would be an invitation to
  build the endpoint the fence exists to prevent.

The future public-API writer consumes the fixed wire names, the append-only reason values and the
column widths recorded above; it does not re-decide them, and it arrives with its own rules about
which request was refused.

### Reading it — `admin/api-request-failures`

| Endpoint | Auth | Accepts / answers |
| --- | --- | --- |
| `GET /admin/api-request-failures` | `viewApiRequestFailures` | `page`, `pageSize`, `search`, repeated `reasons`, `ip`, `country`, `isp`, `dateFrom`, `dateTo`, `sortBy`, `sortOrder` → `{ items, total, page, pageSize }` |
| `DELETE /admin/api-request-failures` | CSRF + `viewApiRequestFailures` | `200 api_request_failures_cleared`; wipes the whole table |

`ApiRequestFailureQuery` / `ApiRequestFailureQueryText` resolve, in Core so they are testable
without a host, every camelCase query parameter. The read carries `Cache-Control: no-store`
through `ApiControllerBase.NoStore()` - the log is evidence about visitors' requests and a shared
cache holding a page of it would hand one operator's reading to the next.

- `page` (default 1, at least 1), `pageSize` (default 20, 1..1000), `search` (at most 200
  characters, trimmed, case-insensitive substring of `description`, `subscription_data` and
  `content_name` only - `ip`, `isp` and `country` have exact filters instead);
- `reasons` as a **repeated** query parameter (`?reasons=auth_failed&reasons=user_blocked`), each
  element parsed on its own, case-insensitively; duplicates deduplicated in the order sent; more
  than `MaxFilterValues = 100` refused rather than trimmed; a parameter that is absent (or carries
  no values at all) means no filter; one element that names nothing - including a blank one -
  refuses the whole filter under `reasons`. The controller reads it through
  `HttpRequest.Query` because model binding cannot keep each element's own refusal;
- `ip` and `isp` exact, **case included** - the row is a snapshot of what the request sent, and a
  value longer than its column (`45` / `255`) is refused under its own name because it can never
  equal a stored row and an empty page would look like a filter that simply matched nothing;
  `country` canonicalised to upper-case ISO 3166-1 alpha-2 (`de` → `DE`, through
  `CountryCodes.Normalize`) and a value naming no code refused under `country`. A blank value is
  "no filter" for all three, and a null column never matches a bound;
- `dateFrom` / `dateTo`, one range over `created_at`, **both inclusive and each optional**, an ISO
  8601 date or date-time parsed with the invariant culture and an absent offset read as UTC
  (`2026-05-01`, `2026-05-01T10:00`, `2026-05-01t10:00:00z`, `2026-05-01T10:00:00+03:00`). An
  unparseable bound is refused under its own name; a `dateFrom` later than `dateTo` is a valid
  query describing an empty result (200, not 400), because swapping the bounds would answer a
  different question from the one asked;
- `sortBy` (`createdAt` | `reason`, default `createdAt`) and `sortOrder` (`asc` | `desc`, default
  `desc`), parsed from plain text case-insensitively - enum binding would refuse `desc` while
  accepting `Descending`.

**Nothing is clamped.** The page bounds are the caller's to break and the parser's to refuse; a
broken parameter is answered `400 validation_failed` keyed by its camelCase query name (merged with
`ListQueryErrors.Common` for `page`/`pageSize`/`search`), never silently narrowed. `total` is
counted under the same predicate the page is cut from and before `Skip`/`Take`, and the order is
tie-broken by `id` in the same direction, so consecutive page turns are disjoint with one constant
`total`.

**The response row** (`AdminApiRequestFailureItemResponse`): `id` (text), `subscriptionData`,
`contentKind` (the `ContentType` member name, `"VideoStream"` …), `contentId` (text),
`contentName`, `reason` (the snake_case wire name), `ip`, `isp`, `countryCode`, `description`,
`createdAt`. `id` and `contentId` are text because a JavaScript client parses a uuid as a number
otherwise. Nothing is resolved from another table: the row answers what it stored, and a deleted
content still reads with its captured name.

**The clear ignores the screen's filters on purpose.** The button says "clear the log", so the whole
table goes whatever the reader had narrowed it to; a delete that obeyed a filter the caller did not
send would wipe a different set from the one it named. It is not paged and no identifier is passed.
An empty table answers `200` with `0`, because the second press of a button that already did its job
is not an error. `ExecuteDelete` is immediate and outside the unit of work, which the interface's
remarks say; `IApiRequestFailureRepository.ClearAllAsync` is what performs it.

**Two REST departures, both owner-approved and recorded in the controller's remarks and in
AGENTS.md**: one permission gates the read and the wipe (there is no write permission at all), and
the wipe is a `DELETE` on the collection that removes every row regardless of the query string.

### The Development seeder

`DevelopmentApiRequestFailureSeeder` fills an empty log with **twelve** trial rows - every one of
the six reasons, three providers across three countries, and some rows with no subscription data and
no content snapshot, because the reason select and the two content states have to be exercised.

- **Development only.** It is gated on `IHostEnvironment.IsDevelopment()`; the Testing host the
  integration suite runs on is not Development, so the seeder is a no-op there rather than a second
  seeder the fixtures would have to undo. Production is a no-op for the same gate.
- **Empty table only.** It refuses a table that already holds any row (`CountAsync` is unfiltered by
  design): trial rows dropped on top of a log somebody has actually used would be invented
  evidence.
- **It invents nothing the schema has to point at.** Every column is a snapshot and no foreign key
  points out of the table, so no prerequisite is read first - unlike the connection-log seeder. The
  content identifiers are freshly generated opaque values, which is exactly what the column is for.
- It runs from `SeedDatabaseAsync` after the user and connection-log seeders, stages through
  `AddRangeAsync` and commits with one `IUnitOfWork.SaveChangesAsync`; every trial row therefore
  carries the same `created_at`, which is the case the `(created_at, id)` tie-break exists for.

### The admin screen

The page is `src/pages/admin/ApiRequestFailuresPage.tsx` at `/admin/api-request-failures`, reached
from the one menu item appended after the three blacklist entries in the «Чёрные списки» group
(key `apiRequestFailures`, icon `WarningOutlined`). **The address mirrors the backend's own route -
`api-request-failures`, not `blacklists/api-request-failures`** - the credit-logs/tariff-plans
treatment: the page, the API and the menu entry name one resource the same way.

Five columns in this order: subscription, content (stored name with its muted opaque identifier),
reason (a tag labelled from the six wire values), network (the shared `ConnectionNetworkCell`,
whose click-to-filter writes `ip`/`country`/`isp` exactly as on the connection and user logs) and
created date. `createdAt` and `reason` are sortable, default `createdAt desc`; the starting widths
sum to 1190 (`features/api-request-failures/layout.ts`). The toolbar reads: auto-refresh toggle
(30 s, **on** by default, because refusals arrive continuously), search, reason multi-select, width
reset, clear-all. The date range is the one control too wide for the card head, so it is a row of
its own in the card body above the tag row and the table - the `ConnectionLogsPage` placement.

A visitor without `viewApiRequestFailures` draws the card's title and the `noPermission` alert in
place of the toolbar, filters and table, and the log query is disabled so the screen does not spend
a request the server would answer 403. Clear-all is disabled by permission only, never by data
state: greying it out because the filtered slice is empty would be lying about a log that still
holds rows. It confirms through `modal.confirm` with copy stating that the whole log goes.

Layering follows the usual split: `ApiRequestFailure` in `Fluxy.Core/Requests/Models/`,
`NewApiRequestFailure` in `Commands/`, `ApiRequestFailureListItem` / `ApiRequestFailurePage` in
`ReadModels/`, `ApiRequestFailureReason` / `ApiRequestFailureSortField` /
`ApiRequestFailureSortOrder` in `Enums/`, `ApiRequestFailureListLimits` /
`ApiRequestFailureQuery` / `ApiRequestFailureQueryText` / `ApiRequestFailureReasons` in `Utils/`,
`IApiRequestFailureService` / `IApiRequestFailureSeeder` in `Interfaces/`,
`IApiRequestFailureRepository` in `Repositories/`; `ApiRequestFailureService` and
`DevelopmentApiRequestFailureSeeder` in `Fluxy.Application/Requests/Services/`; the entity,
configuration and repository in `Fluxy.DataAccess`; the contracts in `Fluxy.API/Contracts/Requests/`;
`ApiRequestFailuresController` in `Fluxy.API/Controllers/Admin/`; migration
`20261109151922_AddApiRequestFailures`.

### Verified

**800** unit tests green (the reason wire round trip, the numeric permission table, the limit
constants, and every query refusal: page/pageSize/search bounds, an unknown reason element, an
oversized `ip`/`isp`, an unknown `country`, an unparseable date bound, the inverted range as a valid
empty query, and the sort spellings) and **282** integration tests green (`ApiRequestFailureTests`
pins the default newest-first order with its identifier tie-break, every broken parameter refused
under its own name, all six wire names accepted in any case and composed with the other filters, the
exact case-preserved `isp` filter, the inclusive date range with each side optional, the search over
the three free-text columns, the row contract's stored snapshot and wire reason, the null content
group, the clear with its CSRF refusal, its filter-ignoring behaviour and its empty-table answer,
the permission matrix - Admin with the grant reads, Admin without it gets `403 permission_denied`,
Client and Reseller get `403 auth_role_changed` - the base Administrators group holding the grant
after seeding, and every other method on the collection answering `405`). Frontend `typecheck`,
`lint`, **253** unit tests, `build` and the three static probes are green; `dotnet build` clean
(0 warnings), `dotnet build -c Release -warnaserror` clean, and `dotnet ef migrations
has-pending-model-changes` reports no changes.
