> Part of the FluxyBackend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).

## The MAG event journal: what was sent to a device, and whether it confirmed

The record of the operations an administrator sent to the MAG device of one subscription. Unlike
the connection log and the refused-request log, this journal has a **real write**: a send is the
operation, not a capture somebody else performs. The other half of the capability is the read the
support call arrives with and the collection-level erase that resets the table.

### Stored shape — `mag_events`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `uuid` | Client-generated, from `AuditableEntity` (`ValueGenerated.Never`). |
| `subscription_id` | `uuid NOT NULL` | **A real foreign key to `subscriptions`, `ON DELETE CASCADE`.** Not an opaque identifier. |
| `event_type` | `smallint NOT NULL` | `MagEventType`, `HasConversion<short>()` (`PlayChannel` = 1 … `ResetStbLock` = 6). |
| `message` | `varchar(1000) NULL` | The operation's payload. Null for the four operations that carry none. |
| `status` | `smallint NOT NULL` | `MagEventStatus`, `HasConversion<short>()` (`NotConfirmed` = 0, `Confirmed` = 1). |
| `need_confirm` | `boolean NOT NULL` | Forced true for `sendMessage`, otherwise the caller's value or false. |
| `reboot_after_ok` | `boolean NOT NULL` | Defaults to false. |
| `auto_hide_timeout` | `boolean NOT NULL` | Defaults to false. |
| `additional_services_on` | `boolean NOT NULL` | **The one flag whose default is true.** |
| `anec` | `boolean NOT NULL` | Defaults to false. |
| `vclub` | `boolean NOT NULL` | Defaults to false. |
| `created_at`, `updated_at` | `timestamptz NOT NULL` | Stamped by the context, as everywhere. |

Two indexes, one per question the screen asks: `(created_at DESC, id DESC)` for the default order
and the tie-break every page turn depends on, and `(subscription_id, created_at)` for "this
subscription's events, oldest first" - the drill-down and the shape the `subscriptionId` filter runs
on. **No column carries `HasDefaultValue`**, the rule every table of this solution follows: the
flags are resolved by `MagEventRules` in the service and written explicitly, so a column the
application forgot is an error rather than a value the database invented.

**The subscription cascades and no device address is snapshotted.** An event is evidence about one
subscription, so deleting the subscription deletes its events - a journal line whose subject is gone
is what the operator asked to remove. There is no `mag_mac` column: the list resolves the address
from the subscription when the page is built, so a corrected MAC draws as it is now rather than as
it was when the event was sent. A subscription whose MAG address has since been cleared reads as
`null` on its old rows, and the screen draws a dash.

**Every width comes from `MagEventListLimits`** (`MessageMaxLength = 1000`, `DefaultPageSize = 20`,
`MaxPageSize = 1000`, `MaxSearchLength = 200`), and the send rule refuses a longer message rather
than cutting it, so a column width and the rule that judges a value cannot drift apart.

### The permission — the subscription surface's own pair, reused

`GET /admin/mag-events` requires `viewSubscriptions` (`UserPermission.ViewSubscriptions =
16777216`); `POST` and `DELETE /admin/mag-events` require `editSubscriptions`
(`UserPermission.EditSubscriptions = 33554432`) plus a fresh antiforgery pair. Both already exist
and neither the catalog nor the frontend mirror changes: reading the journal is reading the
subscription surface, and sending an event or wiping the record of what was sent is a change to what
that surface holds. **No third permission was invented and no permission backfill migration
exists** - `DefaultUserSeeder` reconciles the base groups against the catalog on every boot anyway.

### The endpoints — `admin/mag-events`

| Endpoint | Auth | Accepts / answers |
| --- | --- | --- |
| `GET /admin/mag-events` | `viewSubscriptions` | `page`, `pageSize`, `search`, `event`, `subscriptionId`, `dateFrom`, `dateTo`, `sortBy`, `sortOrder` → `{ items, total, page, pageSize }` |
| `POST /admin/mag-events` | CSRF + `editSubscriptions` | `{ subscriptionId, event, message?, needConfirm?, rebootAfterOk?, autoHideTimeout?, additionalServicesOn?, anec?, vclub? }` → `201 mag_event_sent` with a `Location` naming the new row |
| `DELETE /admin/mag-events` | CSRF + `editSubscriptions` | `200 mag_events_cleared`; wipes the whole table |

`MagEventQuery` / `MagEventQueryText` resolve, in Core so they are testable without a host, every
camelCase query parameter. The read carries `Cache-Control: no-store` through
`ApiControllerBase.NoStore()`.

- `page` (default 1, at least 1), `pageSize` (default 20, 1..1000), `search` (at most 200 characters,
  case-insensitive substring of `message` **or** the subscription's `mag_mac`). The MAC half is a
  correlated `EXISTS`, not a join in the predicate, so `total` is still counted on `mag_events`
  alone; the value the query stores is trimmed;
- `event` is one of the six wire names (`playChannel`, `closePortal`, `reloadPortal`,
  `rebootDevice`, `sendMessage`, `resetStbLock`), matched case-insensitively and
  whitespace-tolerantly. **An unknown value is refused under `event`, never treated as "no filter"**:
  a mistyped operation that silently widened the list would answer a request nobody made;
- `subscriptionId` is a uuid as text; a value that is not one is refused under `subscriptionId`
  rather than read as "no filter". A blank value is "no filter";
- `dateFrom` / `dateTo`, one range over `created_at`, **both inclusive and each optional**, an ISO
  8601 date or date-time parsed with the invariant culture and an absent offset read as UTC
  (`2026-05-01`, `2026-05-01T10:00`, `2026-05-01t10:00:00z`, `2026-05-01T10:00:00+03:00`). An
  unparseable bound is refused under its own name; a `dateFrom` later than `dateTo` is a valid query
  describing an empty result (200, not 400), because swapping the bounds would answer a different
  question from the one asked. Inclusive because the screen's picker names whole days - the page
  sends the first and last millisecond of the chosen day in the account's zone;
- `sortBy` is `createdAt` and nothing else (default `createdAt`), parsed from plain text
  case-insensitively; `sortOrder` is `asc` | `desc` (default `desc`). Enum binding was deliberately
  not used: it takes a member spelled in full, so `event=playChannel` would be refused while
  `Event=PlayChannel` was accepted.

**Nothing is clamped.** A broken parameter is answered `400 validation_failed` keyed by its
camelCase query name, merged with `ListQueryErrors.Common` for `page`/`pageSize`/`search`, never
silently narrowed. Every parameter is judged before the answer is given, so one request that broke
two of them is told about both at once. `total` is counted under the same predicate the page is cut
from, before `Skip`/`Take`; the order is tie-broken by `id` in the same direction, so consecutive
page turns are disjoint with one constant `total`.

**The response row** (`AdminMagEventItemResponse`): `id` (text), `subscriptionId` (text), `magMac`
(resolved from the subscription, or null), `eventType`, `message`, `status` (`confirmed` /
`notConfirmed`), `createdAt`. The identifiers travel as text because a JavaScript client parses a
uuid as a number otherwise.

### Send rules — one operation, one meaning

`POST` records one row for one subscription's device and answers `201` with a `Location` naming it.
The antiforgery pair is checked **first, before the body is read** - the same order every write of
this API follows. The request contract carries **no data annotations**, deliberately: framework
validation runs before an action, so a field rule written as an attribute would answer before the
CSRF check and turn a forged request into a field report. Every field is judged by `MagEventRules`
after the pair has been accepted (an unparseable body still fails model binding first, as on every
endpoint).

`MagEventRules.TryResolve` applies the rules in this order, and the service then asks two questions
about the subscription:

| Operation | Payload | Flags |
| --- | --- | --- |
| `sendMessage` | A non-blank message, trimmed, at most 1000 characters. | `need_confirm` is **forced true**, whatever the body said. |
| `playChannel` | The video stream's identifier, required, trimmed. **Shape only** - `Guid.TryParse` accepts the N, B, P and X forms as well as the canonical `8-4-4-4-12`; whether a video stream carries that identifier is a fact about another table and deliberately not a condition of sending. The admin form's own mirror is narrower (canonical spelling only), which is safe because the stream picker is the only way a value reaches that field. | Unset flags resolve to their defaults. |
| `closePortal`, `reloadPortal`, `rebootDevice`, `resetStbLock` | **None. A message sent anyway is dropped, not stored and not refused.** | Unset flags resolve to their defaults. |

Every flag arrives as a nullable on the request because "not sent" and "sent false" are different
requests. The service resolves them: `need_confirm` as above, `reboot_after_ok` / `auto_hide_timeout`
/ `anec` / `vclub` false when unset, and **`additional_services_on` true when unset** - the one
non-false default, which is exactly why defaults live in the service and not in the database. A new
row is always stored `status = notConfirmed`; the confirmation path that will flip it belongs to the
future streaming subsystem.

**The refusal codes, in the order the action reaches them:**

1. `csrf_invalid` (400) - no valid antiforgery pair; the body was never read and nothing was written.
2. `validation_failed` (400) - a field the request got wrong, under that field's camelCase key:
   - `subscriptionId` - missing, blank, or not a uuid. **An unknown-but-well-formed subscription
     identifier is also answered here, as `validation_failed` under `subscriptionId`** (the service
     asks whether the row exists before it asks about the device): a body naming an identifier no
     row carries is a broken field, not a subscription without a device;
   - `event` - missing or naming none of the six operations;
   - `message` - missing or blank for `sendMessage` / `playChannel`, longer than 1000 characters
     after trimming, or not a uuid for `playChannel`.
3. `mag_device_missing` (400) - the subscription **exists** and has a null or blank `MagMac`, so
   there is nothing to send the event to. That is a fact about the row rather than a malformed body
   field, so it travels as its own code and not through the validation map. The two answers are
   deliberately distinguishable: an operator who mistyped an identifier and one whose subscription
   has no device need different corrections.

The wording of the ratified pair is fixed by the integration suite: an unknown `subscriptionId` is
`validation_failed` under that key, and a missing device is `mag_device_missing` - ratified by the
lead after the phase-B review's finding 2, so neither answer may drift to the other.

**`status` is not the request's to name.** The contract has no `status` field: a new row is always
`notConfirmed`, and a client cannot claim otherwise. The six flags are nullable precisely so that
"not sent" and "sent false" stay different requests, and the server resolves whatever the caller
left out.

### The clear — a collection-level `DELETE`

`DELETE /admin/mag-events` deletes every row of the journal, whatever the screen was filtered to.
It sends no filter and no identifier, and **any query string is ignored on purpose**: the button
says "clear the journal", so a delete that obeyed a filter the caller happened not to send would
wipe a different set from the one it named. It is the collection-level `DELETE` REST describes for
"remove everything", not a filtered bulk delete wearing the same verb. An empty table answers `200`
with a count of zero - the second press of a button that already did its job is not an error.
`ExecuteDelete` is immediate and outside the unit of work, which the repository interface's remarks
say; `IMagEventRepository.ClearAllAsync` is what performs it. The antiforgery pair is checked
**first**, because this action destroys an installation's record of what was sent.

**This departure is already agreed, not new.** It is the same collection-level clear the user log,
the refused-request log and the stream problem journal each record in AGENTS.md's "Already agreed"
list (the `/admin/user-logs`, `/admin/api-request-failures` and `/admin/stream-problems` bullets).
The permission half is the other recorded departure restated: the read and the clear sit on the
parent resource's existing pair rather than one grant or a fourth permission, because this journal is
the subscription surface's record and that surface already has a read and a change.

**`PATCH`, `PUT` and `DELETE {id}` answer `405 method_not_allowed`.** An event is a fact that was
sent; editing or deleting one would rewrite the record of what happened, so no such route performs
anything. The `{id}` path matches no real route, and rather than letting an unknown path answer the
generic `404`, the controller declares a fence action for those three verbs that reads neither the
identifier nor the body and answers `405`; the collection answers the same for methods it does not own
through the shared status-page envelope. The `405` describes the resource, not the caller's grant: a
group holding only `viewSubscriptions` gets the same answer rather than a `403`, because the fence is
the absence of a handler.

### The `event` / `eventType` naming asymmetry

The same fact has two wire spellings, and both are the contract:

- **inbound** (the `POST` body and the `GET` query parameter) says `event`;
- **outbound** (a row of the list) says `eventType`.

The asymmetry is explicit on both sides rather than inherited from camelCase. The body contract name
is `Event`, so it serializes as `event`; the query parameter is but for its binding called
`eventType`, and `[FromQuery(Name = "event")]` is what makes the wire name the short one. The
response row's C# property is `EventType`, which camelCases to `eventType`. The frontend mirrors it:
`MagEventSendBody.event` on the way in, `MagEventRow.eventType` on the way out. A client that reuses
one spelling for the other is refused `validation_failed` under `event` on a send and reads
`undefined` from a row; the asymmetry is recorded here rather than smoothed over, because renaming
either side is a wire change.

### Layering

`MagEvent` in `Fluxy.Core/Subscriptions/Models/`, `NewMagEvent` / `MagEventWrite` in `Commands/`,
`MagEventListItem` / `MagEventPage` in `ReadModels/`, `MagEventType` / `MagEventStatus` /
`MagEventSortField` / `MagEventSortOrder` in `Enums/`, `MagEventListLimits` / `MagEventQuery` /
`MagEventQueryText` in `Utils/`, `IMagEventService` in `Interfaces/`, `IMagEventRepository` in
`Repositories/`; `MagEventService` and `MagEventRules` in `Fluxy.Application/Subscriptions/`
(`Services/` and `Utils/`); the entity, configuration and repository in `Fluxy.DataAccess`; the
contracts in `Fluxy.API/Contracts/Subscriptions/`; `MagEventsController` in
`Fluxy.API/Controllers/Subscriptions/`; migration `20261111102215_AddMagEvents`.

### Verified

**905** unit tests green (the query defaults and every refusal, the limit constants, and the send
rules case for case: the forced `need_confirm`, the four operations that drop a payload, the
`playChannel` shape, the length bound and the one non-false flag default) and **311** integration
tests green (`MagEventTests` pins the default newest-first order with its identifier tie-break,
every broken query parameter refused under its own name, the subscription/event/search/date filters
and their composition, the inclusive range with each side optional, the bounds refused rather than
clamped, the send rules per operation, the ratified `validation_failed`/`mag_device_missing` pair,
the reused grant pair with `403 permission_denied` and the role gates, the clear with its CSRF
refusal and its filter-ignoring behaviour, every per-row method answering `405` (including for a
read-only admin), the cascade on subscription delete, and a stored `status = 1` reading as
`confirmed`). Frontend `typecheck`, `lint`, **293** unit tests, `build` and the three static probes
are green; `dotnet build -warnaserror` clean and `dotnet ef migrations has-pending-model-changes`
reports no changes.
