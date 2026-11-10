> Part of the FluxyBackend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).

## The stream problem journal: what the producer reported, which stream, and when

The record of a stream failure the producer reported. **The capture belongs to the future stream
producer and is not part of this capability.** What exists here is the storage shape, a read surface
an administrator uses, one whole-table clear, the one write-path contract the producer will call, and
a Development seeder that gives an empty table something to draw. The write-path fence in the middle
of this file is the point: nothing in this pass records a problem over HTTP.

### Stored shape — `stream_problems`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `uuid` | Client-generated, from `AuditableEntity` (`ValueGenerated.Never`). |
| `stream_id` | `uuid NOT NULL` | **A real foreign key to `video_streams`, `ON DELETE CASCADE`.** Not an opaque identifier. |
| `error_message` | `varchar(1000) NOT NULL` | The producer's own sentence, trimmed and cut to the column at the write boundary. |
| `created_at`, `updated_at` | `timestamptz NOT NULL` | Stamped by the context, as everywhere. |

Two indexes, one per question the screen asks: `(created_at DESC, id DESC)` for the default order and
the tie-break every page turn depends on, and `(stream_id, created_at)` for "this stream's problems,
newest first" - the drill-down from a stream and the shape the `streamId` filter runs on. No column
carries `HasDefaultValue`.

**The stream is a real reference, and no name is snapshotted.** A problem is evidence about one
configured stream of this installation, so `stream_id` must name an existing row and the cascade
removes a stream's journal with it. There is no `stream_name` column on purpose: the list resolves the
name through a join on read, so a renamed stream draws its current name on every page rather than the
name it carried when the failure happened. The refusal log makes the opposite choice deliberately; it
has no parent resource to point at, so it stores what it can never resolve.

**Every width comes from `StreamProblemListLimits`** (`ErrorMessageMaxLength = 1000`,
`DefaultPageSize = 20`, `MaxPageSize = 1000`, `MaxSearchLength = 200`), and the write boundary cuts a
message to the same constant - so a column width and the rule that shortens a value to fit cannot
drift apart.

### The permission — the parent resource's own pair, split

`GET /admin/stream-problems` requires `viewStreams` (`UserPermission.ViewStreams = 1024`);
`DELETE /admin/stream-problems` requires `editStreams` (`UserPermission.EditStreams = 2048`) plus the
antiforgery pair. Both already exist and neither the catalog nor the frontend mirror changes - reading
a stream's history is the parent resource's read, and wiping the record of its failures is a change to
what the streams surface holds.

**No permission backfill migration.** The journal adds no permission at all, and `DefaultUserSeeder`
reconciles the base groups against the catalog on every boot anyway.

### The write-path fence

**This capability contains no HTTP write, and that absence is the fence.** A `POST`, a `PUT`, a
`PATCH` or a `DELETE {id}` on this resource would be a route with no rule behind it, so those routes
are not declared at all - and every method the collection does not own answers `405
method_not_allowed` through the shared status-page envelope, proven at the wire by an integration
test.

- `IStreamProblemService.RecordAsync(NewStreamProblem)` **is the future producer's contract.** It
  trims the message, refuses a blank one with `ArgumentException` rather than storing an empty row,
  cuts an oversized one to the column instead of losing the record, stages through
  `IStreamProblemRepository.AddAsync` and commits with its own `IUnitOfWork.SaveChangesAsync`. The
  rules live together because a producer's sentence is not a form field a client typed, and losing
  the record over its length would lose the fact that the stream failed at all.
- **A failed record is not swallowed.** Unlike `IUserLogService`, which records something that has
  already happened and must never change the outcome it describes, the row here is the whole
  operation - retry, backoff and what to do when the journal is unreachable are the future producer's
  policy, not this service's.
- `StreamProblemEntity.ToModel`/`FromModel` exist and are **reserved for the future producer**; their
  remarks say so and nothing calls them today. They stay beside each other because a mapping written
  later would be a second opinion about what the columns mean.
- **The only production writers are the future producer and the Development seeder** (the
  integration suite's `TestDataSeeder` plants through the same contract). An operator typing a
  problem by hand would make the journal evidence of nothing.

### Reading it — `admin/stream-problems`

| Endpoint | Auth | Accepts / answers |
| --- | --- | --- |
| `GET /admin/stream-problems` | `viewStreams` | `page`, `pageSize`, `search`, `streamId`, `dateFrom`, `dateTo`, `sortBy`, `sortOrder` → `{ items, total, page, pageSize }` |
| `DELETE /admin/stream-problems` | CSRF + `editStreams` | `200 stream_problems_cleared`; wipes the whole table |

`StreamProblemQuery` / `StreamProblemQueryText` resolve, in Core so they are testable without a host,
every camelCase query parameter. The read carries `Cache-Control: no-store` through
`ApiControllerBase.NoStore()` - a page of an installation's stream failures is not something a shared
cache may keep.

- `page` (default 1, at least 1), `pageSize` (default 20, 1..1000), `search` (at most 200 characters,
  case-insensitive substring of `error_message` **only** - the stream's name is resolved by a join and
  is deliberately not part of the stored predicate, because a count built over a join is a different
  query shape). The shared `ListQueryErrors.Common` bound and the parser's own check are both applied
  and both keyed `search`; the parser is merged last, so the trimmed value's judgment is the sentence
  the caller hears. The value the query stores is trimmed;
- `streamId` is a uuid as text; a value that is not one is refused under `streamId` rather than read
  as "no filter", because a silently absent filter answers a list wider than the caller asked for. A
  blank value is "no filter";
- `dateFrom` / `dateTo`, one range over `created_at`, **both inclusive and each optional**, an ISO
  8601 date or date-time parsed with the invariant culture and an absent offset read as UTC
  (`2026-05-01`, `2026-05-01T10:00`, `2026-05-01t10:00:00z`, `2026-05-01T10:00:00+03:00`). An
  unparseable bound is refused under its own name; a `dateFrom` later than `dateTo` is a valid query
  describing an empty result (200, not 400), because swapping the bounds would answer a different
  question from the one asked. Inclusive because the screen's picker names whole days: an end the
  operator picked as "the 1st" that dropped everything written during the 1st would answer a list
  narrower than the control shows;
- `sortBy` is `createdAt` and nothing else (default `createdAt`), parsed from plain text
  case-insensitively - enum binding would refuse `desc` while accepting `Descending`. The message is
  free text nobody groups a journal by, and an order on the joined stream name would order a page the
  server had already cut;
- `sortOrder` (`asc` | `desc`, default `desc`), parsed the same way.

**Nothing is clamped.** The page bounds are the caller's to break and the parser's to refuse; a broken
parameter is answered `400 validation_failed` keyed by its camelCase query name (merged with
`ListQueryErrors.Common` for `page`/`pageSize`/`search`), never silently narrowed. Every parameter is
judged before the answer is given, so one request that broke two of them is told about both at once.
`total` is counted under the same predicate the page is cut from and before `Skip`/`Take`, on
`stream_problems` alone - the join that fills the stream's name is one stream per problem and cannot
change the answer - and the order is tie-broken by `id` in the same direction, so consecutive page
turns are disjoint with one constant `total`.

**The response row** (`AdminStreamProblemItemResponse`): `id` (text), `streamId` (text),
`streamName` (resolved by the join), `errorMessage`, `createdAt`. `id` and `streamId` are text because
a JavaScript client parses a uuid as a number otherwise; they are unrelated - the first is this
record's own key, the second the stream the problem is about.

**The clear ignores the screen's filters on purpose.** The button says "clear the logs", so the
whole table goes whatever the reader had narrowed it to; a delete that obeyed a filter the caller did
not send would wipe a different set from the one it named. It is not paged and no identifier is
passed. An empty table answers `200` with `0`, because the second press of a button that already did
its job is not an error. `ExecuteDelete` is immediate and outside the unit of work, which the
interface's remarks say; `IStreamProblemRepository.ClearAllAsync` is what performs it. The antiforgery
pair is checked **first**, before anything else, because this action destroys an installation's record
of stream failures.

**Two REST departures, both owner-approved and recorded in the controller's remarks and in
AGENTS.md**: the read and the wipe sit on the parent resource's existing grant *pair* rather than one
permission, and the wipe is a `DELETE` on the collection that removes every row regardless of the
query string.

### The Development seeder

`DevelopmentStreamProblemSeeder` fills an empty journal with **twelve** trial rows, rotating through
up to three streams so the `streamId` filter has more than one value to tell apart. The messages are
the mix the screen needs: short sentences and long ones, a refused connection, a timeout, a codec, a
missing source - so the long one wraps and the search has something to narrow.

- **Development only.** It is gated on `IHostEnvironment.IsDevelopment()`; the Testing host the
  integration suite runs on is not Development, so the seeder is a no-op there rather than a second
  seeder the fixtures would have to undo. Production is a no-op for the same gate.
- **Empty table only.** It refuses a table that already holds any row (`CountAsync` is unfiltered by
  design): trial rows dropped on top of a journal somebody has actually used would be invented
  evidence.
- **It never invents a stream.** `stream_id` is a real foreign key, so the seeder reads the
  installation's own streams through `IVideoStreamRepository` and names what it found; without at
  least one stream there is nothing a problem could be about, and it logs one line and writes
  nothing.
- It runs from `SeedDatabaseAsync` after the user, connection-log and refusal-log seeders, stages
  through `AddAsync` and commits with one `IUnitOfWork.SaveChangesAsync`; every trial row therefore
  carries the same `created_at`, which is the case the `(created_at, id)` tie-break exists for.

### The admin screen

The page is `src/pages/admin/StreamProblemsPage.tsx` at `/admin/stream-problems`, reached from the
one menu item appended after the `videoStreams` group's four generated entries (key `streamProblems`,
icon `WarningOutlined`). **The address mirrors the backend's own route - `stream-problems`, not
`video-streams/stream-problems`** - the credit-logs/api-request-failures treatment: the page, the API
and the menu entry name one resource the same way. The item key is deliberately not prefixed with the
section's own (`streamProblems`, not `videoStreamsProblems`), which keeps the `videoStreams*` prefix
assertion the four generated entries rest on exactly as it is.

Three columns in this order: stream (the joined current name), error (the stored message) and created
date; `createdAt` is the only sortable header, default `createdAt desc`; the starting widths sum to
1190 (`features/stream-problems/layout.ts`). The toolbar reads: auto-refresh toggle (30 s, **on** by
default, because problems arrive continuously while streams run), search, stream picker, width reset,
clear-all. The date range is the one control too wide for the card head, so it is a row of its own in
the card body above the tag row and the table - the `ConnectionLogsPage` placement.

The stream's name is a link into that stream's own edit form (`/admin/video-streams/{id}`) for an
operator holding `editStreams`, and plain text for a reader without it. It is the same address the
connection log's stream cell opens, built from the streams table's menu item rather than a copied
segment - no query parameter and no change to the streams page itself.

A visitor without `viewStreams` draws the card's title and the `noPermission` alert in place of the
toolbar, filters and table, and the journal query is disabled so the screen does not spend a request
the server would answer 403. Clear-all is gated on `editStreams` and never by data state: greying it
out because the filtered slice is empty would be lying about a journal that still holds rows. It
confirms through `modal.confirm` with copy stating that the whole journal goes.

### Decisions worth not re-litigating

- **The stream foreign key cascades and no name is snapshotted.** A journal line whose subject no
  longer exists is what the operator asked to remove; the join on read keeps every surviving line
  showing the stream's current name.
- **The read and the clear use the parent resource's split grant pair** (`viewStreams` /
  `editStreams`) rather than one permission or a third invented bit. Reusing `viewStreams` for both
  would let a read-only grant destroy rows, and a dedicated `viewStreamProblems` would split one
  screen's access into three grants nobody asked to reason about. This is the departure from the
  other logs, which gate both verbs with one grant because they have no parent resource.
- **The collection `DELETE` clears the whole journal regardless of the query string** - the same
  collection-level clear the user and refusal logs use, not a filtered bulk delete wearing the same
  verb.
- **Search covers `error_message` only.** The stream's name is joined for display; searching it would
  mean a second predicate over a join and a count that no longer reads one table.
- **The naming is fixed**: table `stream_problems`, clear code `stream_problems_cleared`, route
  `admin/stream-problems`, permission pair `viewStreams` / `editStreams`. The clear code follows
  `user_logs_cleared` / `api_request_failures_cleared`, so a client that knows one knows this one.
- **The search bound is the shared one, applied twice on purpose**: `ListQueryErrors.Common` judges
  the raw value and `StreamProblemQuery` re-judges the trimmed one, both keyed `search` - the
  api-request-failures shape. The parser stores the trimmed value, so the predicate never searches
  for surrounding whitespace.

Layering follows the usual split: `StreamProblem` in `Fluxy.Core/Streams/Models/`,
`NewStreamProblem` in `Commands/`, `StreamProblemListItem` / `StreamProblemPage` in `ReadModels/`,
`StreamProblemSortField` / `StreamProblemSortOrder` in `Enums/`, `StreamProblemListLimits` /
`StreamProblemQuery` / `StreamProblemQueryText` in `Utils/`, `IStreamProblemService` /
`IStreamProblemSeeder` in `Interfaces/`, `IStreamProblemRepository` in `Repositories/`;
`StreamProblemService` and `DevelopmentStreamProblemSeeder` in `Fluxy.Application/Streams/Services/`;
the entity, configuration and repository in `Fluxy.DataAccess`; the contracts in
`Fluxy.API/Contracts/Streams/`; `StreamProblemsController` in `Fluxy.API/Controllers/Admin/`;
migration `20261110122825_AddStreamProblems`.

### Verified

**841** unit tests green (the query defaults and every refusal, the limit constants, and the write
boundary's trim / blank refusal / column cut / un-swallowed failed commit) and **294** integration
tests green (`StreamProblemTests` pins the default newest-first order with its identifier tie-break,
every broken parameter refused under its own name, the stream filter's composition with the date
range and the search, the inclusive range with each side optional, the message-only search, the
split grant pair with `403` for the missing grant and `403 auth_role_changed` for the other roles,
the base Administrators group holding both grants after seeding, the clear with its CSRF refusal, its
filter-ignoring behaviour and its empty-table answer, every other method on the collection answering
`405`, the cascade that removes a stream's problems with it, and the record path that trims, cuts and
stamps one row). Frontend `typecheck`, `lint`, **261** unit tests, `build` and the three static probes
are green; `dotnet build` clean (0 warnings), `dotnet build -c Release -warnaserror` clean, and `dotnet
ef migrations has-pending-model-changes` reports no changes.
