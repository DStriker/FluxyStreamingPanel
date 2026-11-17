> Part of the FluxyBackend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).

## The support tickets: a conversation each account opens with the operator

One resource read from two entrances. `/auth/tickets` is always the caller's own queue and thread;
`/admin/tickets` is every conversation the caller's grants reach, with the transitions only an
operator may make. Both sides read the same row, the same messages and the same derived wire status,
and the difference between them is scope, not shape.

### Stored shape — `tickets`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `uuid` | Client-generated, from `AuditableEntity` (`ValueGenerated.Never`). |
| `user_id` | `uuid NOT NULL` | **A real foreign key to `users`, `ON DELETE CASCADE`.** The account that opened the conversation. |
| `subject` | `varchar(200) NOT NULL` | Trimmed at the service; a blank or over-long one is refused, never cut. |
| `status` | `smallint NOT NULL` | `1` open, `2` closed. The numbers are the schema (`TicketStatus`). |
| `admin_unread` | `boolean NOT NULL` | Whether the operator has something it has not read. |
| `user_unread` | `boolean NOT NULL` | Whether the account has something it has not read. |
| `created_at`, `updated_at` | `timestamptz NOT NULL` | Stamped by the context, as everywhere. |

Two indexes, one per question the screens ask: `(user_id, created_at DESC)` for one account's own
list, newest first; `(created_at DESC, id DESC)` for the operator's default order and the tie-break
every page turn depends on. No column carries `HasDefaultValue`.

**No login name is snapshotted.** The operator's list and both detail reads resolve the owner's name
(and each author's name) through a join on `users`, so a renamed account draws its current name. The
account's foreign key cascades: deleting an account removes its conversations, and the messages'
own cascade removes the text with them.

### Stored shape — `ticket_replies`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `uuid` | Client-generated, from `AuditableEntity`. |
| `ticket_id` | `uuid NOT NULL` | **Foreign key to `tickets`, `ON DELETE CASCADE`.** |
| `author_id` | `uuid NULL` | **Foreign key to `users`, `ON DELETE SET NULL`.** Null once the author is deleted. |
| `message` | `varchar(4000) NOT NULL` | Trimmed; blank refused, over-long refused under `message`. |
| `is_admin_reply` | `boolean NOT NULL` | Which side wrote it — a fact of the message, not of the author's current role. |
| `read_at` | `timestamptz NULL` | When the **other** side read it; null while unread. |
| `created_at`, `updated_at` | `timestamptz NOT NULL` | Stamped by the context. |

One index: `(ticket_id, created_at)` — the conversation oldest first, the shape both the detail read
and the newest-message computation run on. `author_id` also carries the index its `SET NULL` foreign
key builds.

**Deleting the author does not delete the message.** A conversation keeps what was said; the side
stays legible and the author's identifier and name arrive as `null` on the wire.

### The derived status — closed beats the viewer's flag, which beats read

A row stores the lifecycle and the two flags. The wire word is derived per viewer by
`TicketStatuses`, so a filter and a row cannot disagree about what "unread" means.

| Lifecycle | Viewer's own flag | Wire word |
| --- | --- | --- |
| `Closed` | either | `closed` |
| `Open` | `true` (the viewer's side) | `unread` |
| `Open` | `false` | `read` |

- **Closed first.** Closing ends the conversation, so a flag left standing by the message that closed
  it must not keep drawing the row as waiting for an answer.
- **The two sides are independent.** `ForAdmin` reads `admin_unread` and `ForUser` reads
  `user_unread`; a screen cannot accidentally read the other side's state.

| Operation | Lifecycle | `admin_unread` | `user_unread` |
| --- | --- | --- | --- |
| create (account) | `Open` | `true` | `false` |
| reply by the operator | unchanged | `false` | `true` |
| reply by the account | unchanged | `true` | `false` |
| `POST /admin/tickets/{id}/read` | unchanged | `false` | unchanged |
| `POST /auth/tickets/{id}/read` | unchanged | unchanged | `false` |
| `POST /admin/tickets/{id}/unread` | unchanged | `true` | unchanged |
| `POST /admin/tickets/{id}/close` | `Closed` | `false` | unchanged |
| `POST /admin/tickets/{id}/reopen` | `Open` | `false` | unchanged |
| reply on a closed ticket | — | — | — (refused, nothing staged) |

### `read_at` — a receipt per message, stamped by the reader

A message's `read_at` is the moment **the side that did not write it** read the thread. A POST read
clears only the caller's own flag, idempotently, and stamps `read_at = now` on every counterpart
message whose mark is still null:

- the operator's read stamps the account's messages (`is_admin_reply = false`);
- the account's read stamps the operator's messages (`is_admin_reply = true`).

The rows are loaded through the tracker and committed in the operation's single save, so the flag and
the receipts cannot commit apart. A second read finds the flag already clear and no unmarked message
and answers the same word.

**`POST /admin/tickets/{id}/unread` raises the operator's flag and clears the mark of the latest
account message only.** Raising the flag means "there is something new here", not "the whole thread
is unread again"; the latest message is the newest `is_admin_reply = false` row by `created_at` and
identifier, which is the order the thread is drawn in. Nothing else is touched.

### Computed `lastReplyAt`, and the fallback that keeps it one fact

`lastReplyAt` is **never stored**: it is the newest message's `created_at` with the ticket's own
`created_at` as the fallback when the thread is empty. The same expression fills the page row and is
what the default order reads, so the drawn value and the order cannot disagree, and a ticket nobody
has answered yet is still placed by when it arrived.

The denormalised alternative — a `last_reply_at` column maintained by every reply — was rejected: it
would be a second source of truth for a fact the messages already hold, it would need a backfill and
a trigger (or an application rule every writer must remember), and a ticket deleted with its thread
would leave a value nothing recomputes. The cost of computing it is one correlated `MAX` over an
indexed `(ticket_id, created_at)`, inside the page's own query.

### The query contract — both lists

`TicketQuery` / `TicketQueryText` live in Core, so every rule about a value that names nothing is
testable without a host; the controllers keep the HTTP half.

- `page` (default 1, at least 1), `pageSize` (default 20, 1..1000), `search` (at most 200
  characters). The shared `ListQueryErrors.Common` bound and the parser's own check are both applied
  and both keyed by their parameter; the parser is merged last.
- `search` is a case-insensitive substring. The account's list searches the `subject` only, because
  every row is that account's and its own name adds nothing. The operator's list searches the subject
  **or the account's current login name**, resolved by the join.
- `status` is viewer-relative and is **not** a stored enum: `unread` and `read` are read against the
  flag of the side whose page is being cut (and both exclude a closed ticket), `closed` names the
  lifecycle. Any other word is refused under `status`.
- `sortBy` — `subject`, `username`, `createdAt`, `lastReplyAt`; default `lastReplyAt`. `status` is
  **not** a sort field: the word is derived per viewer, so it is not a column the page can be ordered
  by before it is cut. Naming it is refused under `sortBy`.
- `sortOrder` — `asc` | `desc`, default `desc`. Both are parsed from plain text case-insensitively;
  enum binding would refuse `desc` while accepting `Descending`.
- **Nothing is clamped.** One request that broke two parameters is told about both at once, and a
  broken parameter is `400 validation_failed` keyed by its camelCase query name.
- `total` is counted under the same predicate the page is cut from, before `Skip`/`Take`. On the
  operator's list the **role scope is part of that predicate** — `UserPermissionCatalog.VisibleRoles`
  is applied before the count, so the pager never advertises tickets the caller was not going to be
  shown. Every order ends with `id` in the same direction, so page turns are disjoint.

`username` is accepted by both lists' parser because the grammar is one contract, but on the
account's own list every row belongs to the same account, so ordering by the name orders nothing —
the self controller's remarks say so.

### The permission — the account grants, reused, and the pair that was rejected

| Endpoint | Policy | Grant |
| --- | --- | --- |
| `GET /admin/tickets`, `GET /admin/tickets/{id}` | `ViewTicketsPolicy` | **any of** `viewClients`, `viewResellers`, `viewAdmins` |
| every other `/admin/tickets` route (and the read marks) | `EditTicketsPolicy` | **any of** `editClients`, `editResellers`, `editAdmins` |
| `/auth/tickets…` | `[Authorize]` | the account, by ownership only |

`ViewTicketsPolicy` / `EditTicketsPolicy` are built on `PermissionAnyRequirement`, the "any one of"
requirement beside the historical all-of `PermissionRequirement`. The policies are deliberately
coarse; `TicketService` narrows them to the row's role through
`UserPermissionCatalog.CanView`/`CanEdit`, and answers `403 permission_denied` for an existing
conversation whose owner's role the grants do not reach (a missing row stays `404 ticket_not_found`).
`ApiAuthorizationResultHandler.ExplainAsync` reads both requirement shapes so a refusal on these
routes carries `permission_denied` rather than the role-changed word and its sign-in redirect.

**No new `UserPermission` member, no catalog change, no migration, and therefore no backfill.**
Reading a client's conversation is the same question as reading the client, and changing it is the
same question as changing the client; the base Administrators group already holds all six grants.

**The dedicated-grant alternative was rejected.** A `viewTickets`/`editTickets` pair would need two
free bits in `UserPermission`, and when this decision was taken the enum was `int`-based: the
highest member was `ViewApiRequestFailures = 1 << 29`, so only `1 << 30` was left before the sign
bit. A pair could not fit, and widening the enum to `long` would have made the stored grant column
`bigint` and forced a migration of every group row — a schema change bought only to let a new
screen duplicate a grant the operator already has. Reuse keeps the two questions ("may I read this
account", "may I change this account") answered in one place.

> **Reversal (2026-11-16).** The enum *has* since widened to `long` and the column to `bigint`,
> because the transcoding-profile pair needed both `1L << 30` and `1L << 31` - see
> [admin-transcoding-profiles.md](admin-transcoding-profiles.md). The reversal does not reopen this
> decision: the ticket pair is still rejected on its own merits (the account grants already answer
> the same question), and the widened bits went to the profiles instead.

### Reading and writing — `admin/tickets`

| Endpoint | Auth | Accepts / answers |
| --- | --- | --- |
| `GET /admin/tickets` | `view*` | `page`, `pageSize`, `search`, `status`, `sortBy`, `sortOrder` → `{ items, total, page, pageSize }` |
| `GET /admin/tickets/{id}` | `view*` | one ticket and its thread; `404 ticket_not_found`, `403 permission_denied` out of scope |
| `POST /admin/tickets/{id}/replies` | CSRF + `edit*` | `{ message }` → `201 ticket_reply_created` (no `Location`); `409 ticket_is_closed`, `409 ticket_reply_limit_reached` |
| `POST /admin/tickets/{id}/read` | CSRF + `edit*` | `200 ticket_marked_read`, idempotent |
| `POST /admin/tickets/{id}/unread` | CSRF + `edit*` | `200 ticket_marked_unread`, idempotent |
| `POST /admin/tickets/{id}/close` | CSRF + `edit*` | `200 ticket_closed`, idempotent |
| `POST /admin/tickets/{id}/reopen` | CSRF + `edit*` | `200 ticket_reopened`; `409 invalid_status` when not closed |
| `DELETE /admin/tickets/{id}` | CSRF + `edit*` | `200 ticket_deleted`; `404 ticket_not_found` for a ticket already gone |

Every read carries `Cache-Control: no-store`. Every write checks `RejectsCsrfAsync()` **first**, before
the body is read.

### Reading and writing — `auth/tickets`

| Endpoint | Auth | Accepts / answers |
| --- | --- | --- |
| `GET /auth/tickets` | session | the caller's own page, same query grammar |
| `GET /auth/tickets/{id}` | session | own thread; somebody else's identifier → `404 ticket_not_found` |
| `POST /auth/tickets` | CSRF | `{ subject }` → `201 ticket_created` + `Location: /auth/tickets/{id}`; `403 permission_denied` for an operator |
| `POST /auth/tickets/{id}/replies` | CSRF | `{ message }` → `201 ticket_reply_created`; `409 ticket_is_closed`, `409 ticket_reply_limit_reached` |
| `POST /auth/tickets/{id}/read` | CSRF | `200 ticket_marked_read`, idempotent |

The account and the side both come from the token; no body or query names an owner. The create's
`Location` is relative because the server does not know which host serves the frontend, and it is
present because the row exists in full the moment it is written and this very controller reads it
back — unlike the registration create, which has no client-addressable URI yet.

### The fences

- **No `POST /admin/tickets`.** Support is the account's door; an operator answers conversations
  rather than opening one. The collection answers `405 method_not_allowed` to it through the shared
  status-page envelope.
- **No close, reopen, delete or unread on `/auth/tickets`.** Declaring the end of a conversation and
  removing it are the operator's; a `DELETE {id}`, `POST {id}/close`, `POST {id}/reopen` or
  `POST {id}/unread` on the self surface is answered by a fence action that reads neither the
  identifier nor the body, so the path answers `405` rather than the generic `404`. `POST {id}` and
  `PUT {id}` are not declared either and reach the same envelope.
- **Reply on a closed ticket writes nothing**, on either surface: the service reports
  `TicketAction.ReplyToClosed` before staging a row, and the 409 is the transport's answer.
- **The 501st message is refused** with `ticket_reply_limit_reached`, before it is staged.
  `TicketLimits.MaxRepliesPerTicket = 500`.

### Full code list

| Code | Status | Produced by |
| --- | --- | --- |
| `ticket_created` | 201 | create (account side) |
| `ticket_reply_created` | 201 | reply, either side |
| `ticket_marked_read` | 200 | read, either side |
| `ticket_marked_unread` | 200 | unread (operator) |
| `ticket_closed` | 200 | close (operator, idempotent) |
| `ticket_reopened` | 200 | reopen (operator) |
| `ticket_deleted` | 200 | delete (operator) |
| `ticket_not_found` | 404 | unknown identifier, a deleted ticket, or somebody else's on the self side |
| `ticket_is_closed` | 409 | reply to a closed conversation |
| `ticket_reply_limit_reached` | 409 | the message that would cross the thread bound |
| `invalid_status` | 409 | reopen of a ticket that is not closed — the same code the account transitions answer |
| `validation_failed` | 400 | blank/over-long `subject` or `message`, or a broken query parameter |
| `csrf_invalid` | 400 | a write without the antiforgery pair |
| `permission_denied` | 403 | no account grant, an out-of-scope author on detail, or an operator's create |
| `method_not_allowed` | 405 | a fenced verb on either surface |
| `auth_required` | 401 | no accepted token |

### The two deviations, both recorded in the controllers' remarks

1. **`POST {id}/read|unread|close|reopen` are RPC-shaped actions**, not fields of a row. Each is a
   state transition with rules of its own (idempotency, which flag it clears, whether the lifecycle
   admits it); a `PATCH {id}` with a state would carry those rules in the caller's payload, and
   `PUT`/`DELETE` cannot mean "mark read". Each also answers its own code instead of one
   `ticket_updated`.
2. **The writes ride the account write grants and the reads the account read grants**, a pair of
   `PermissionAnyRequirement` policies rather than a dedicated ticket pair — see the permission
   section above for the ceiling that decides it.

### A frontend deviation worth recording: the polls

The four ticket screens refresh themselves on a timer, and that is a deliberate exception to the
no-polling stance the rest of the front-end keeps, so it is recorded here rather than left to be
rediscovered in the components:

- **Both lists** carry the established 30-second auto-refresh toggle the log screens offer
  (`AUTO_REFRESH_MS = 30_000`, on by default, the icon-only pause/resume button in the card head,
  `setInterval` through a ref that holds the query's `refetch`). A queue is a live feed in the same
  sense the refusal log is: an answer written while the visitor is looking at the list should move
  the row.
- **Both detail screens poll every 10 seconds** (`POLL_MS = 10_000`) while the page is mounted and
  the ticket is **open**; the interval is torn down on unmount and is not started at all once the
  status is `closed`, because a closed conversation accepts no message. **A tick is skipped while
  one of the page's own writes is in flight** (`reply`, the mark-read, close, reopen), so a poll can
  never land under a send that is about to answer it; the reply's own success handler still calls
  `refetch()` immediately, so the message appears without waiting for the next tick.
- The **one-shot mark-read stays loop-free**: it is guarded by a ref holding the id already marked,
  so a poll that re-reads the same unread ticket does not fire the write again.
- `refetchInterval` is used by nothing: the timers are plain `setInterval` + a ref, the shape the
  log screens set, so no page gains a per-query cache setting and the shared `QueryClient` defaults
  are untouched.

### Layering

`Ticket` / `TicketReply` in `Fluxy.Core/Tickets/Models/`; `NewTicket` / `NewTicketReply` in
`Commands/`; `TicketOutcome` / `TicketReplyOutcome` / `TicketReadResult` in `Outcomes/`;
`TicketListItem` / `AdminTicketListItem` / `TicketPage` / `AdminTicketPage` / `TicketDetail` /
`TicketReplyItem` in `ReadModels/`; `TicketStatus` / `TicketAction` / `TicketSortField` /
`TicketSortOrder` in `Enums/`; `TicketLimits` / `TicketQuery` / `TicketQueryText` /
`TicketReplySides` / `TicketStatuses` in `Utils/`; `ITicketService` in `Interfaces/`;
`ITicketRepository` in `Repositories/`. `TicketService` in `Fluxy.Application/Tickets/Services/`.
The entities, configurations and repository in `Fluxy.DataAccess`; the contracts in
`Fluxy.API/Contracts/Tickets/`; `TicketsController` in `Fluxy.API/Controllers/Admin/` and in
`Fluxy.API/Controllers/Auth/`; `PermissionAnyRequirement`(+handler) in `Fluxy.API/Security/`;
migration `20261113115748_AddTickets`.

### Verified

**1007** unit tests green (the limit constants, the query grammar and every refusal, the status
matrix, the side helper, and the service's create/reply/read/unread/close/reopen/delete rules,
including the flag movements, the blank and over-long fields, the reply-to-closed and thread-bound
refusals, and the operator's create refusal) and **357** integration tests green, of which
`Admin/TicketsTests` and `Auth/TicketsTests` pin the default order with its identifier tie-break and
the `lastReplyAt` fallback, disjoint page turns with one total, the case-insensitive search over the
subject and the owner's name, the viewer-relative status filter, every sort field, the grant scope on
the list and the detail with `403 permission_denied` for a grantless operator, the transitions'
idempotency and flag movement, the reply-to-closed refusal that writes nothing, the 501st message,
the delete cascade with its second-delete 404, the reads' `no-store`, the collection and row fences
(`405`), the antiforgery refusal on every write of both surfaces, the create's `201` and `Location`,
the own-only list and the foreign identifier's `404`, the reseller's isolation, the operator's create
refusal, the account's read that clears only its own flag and stamps the operator's messages, and the
two cross-cutting cascades (deleting an account removes its tickets and replies; a deleted operator's
messages survive with a null author and keep their side). `dotnet build` is clean (0 warnings) and
`dotnet ef migrations has-pending-model-changes` reports no changes.

The front end was verified in the same pass: `typecheck`, `lint`, `test` (314 unit tests),
`build`, `probe:render`, `probe:i18n` (1291 literal keys in both bundles) and `probe:paths` all
green, and the four screens were driven in a browser against the running API with throwaway
PostgreSQL accounts (a client and an administrator, both deleted afterwards) from a capture under
`FluxyFrontend/.tmp-runtime/`. That pass proved the poll's interval (three detail reads ten seconds
apart), that it stops on a closed ticket (no read in thirteen seconds), that a send re-reads
immediately, that the one-shot mark-read fires once, that the list toggle pauses the timer, and it
found two defects the probes cannot see: the account list's actions heading wrapped onto three
lines at 56px (now `actionsColumnWidth(1) + 45`), and the create flow navigated to the API's
`/auth/tickets/{id}` - a live 404 - instead of the area's own address (`createdTicketPath`, with
unit tests).

