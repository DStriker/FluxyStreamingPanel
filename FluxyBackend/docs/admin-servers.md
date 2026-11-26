> Part of the FluxyBackend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).
>
> The reference direction below was added by the change
> [`openspec/changes/add-server-references`](../../openspec/changes/add-server-references/design.md).

## Servers: `admin/servers`

A server is **an address the installation may relay through**, plus what the machine behind it
is and how it is doing. One row is special: the **main server**, the installation's own address,
created by the startup seeder under a fixed identifier and never deletable.

| Endpoint | Auth | Answer |
| --- | --- | --- |
| `GET /admin/servers` | `viewServers` | `page`, `pageSize`, `search`, `sortBy`, `sortOrder` → `{ items, total, page, pageSize }` |
| `GET /admin/servers/options` | any of `viewSubscriptions` / `viewStreams` | `[{ id, name, timeshiftOnly }]` ordered by name - the picker the subscription and stream forms draw; **no `viewServers` required** |
| `GET /admin/servers/{id}` | `viewServers` | one server; `404 server_not_found` |
| `POST /admin/servers` | CSRF + `editServers` | `201 server_created` + relative `Location`; `409 server_already_exists`; `400 validation_failed` |
| `PATCH /admin/servers/{id}` | CSRF + `editServers` | `200 server_updated`; `404`, `409 already_exists`, `400 validation_failed` |
| `DELETE /admin/servers/{id}` | CSRF + `editServers` | `200 server_deleted`; `404 server_not_found`, `409 server_immutable` on the main row, `409 server_in_use` while a subscription or a stream still names it |

The class carries `[Authorize(Policy = AdminPolicy)]`; each action adds its own permission on top,
the shape every admin controller here follows. Every read carries `Cache-Control: no-store`; every
write checks the antiforgery pair **before** it reads the body, which is why the request contracts
carry no data annotations.

### The field table

| Field | Type | Rule |
| --- | --- | --- |
| `name` | string | 1..200, trimmed, **unique for the installation**, compared case sensitively. Index `IX_servers_name`. |
| `ip` | string | 1..45 characters and an `IPAddress.TryParse` literal (IPv4 or IPv6), trimmed. |
| `port` | int | 1..65535. |
| `clientCount` | int | 0..10000. **`0` means unlimited.** |
| `osName` | string | 1..100, trimmed. |
| `networkInterface` | string | 1..50, trimmed (`eth0` for the seeder). |
| `latency` | double | ≥ 0. **`0` means not measured.** Not a create field: a new row always starts at `0`. |
| `status` | string | written as `active` or `disabled` (case insensitive); an absent status on a create means `active`, and a full write must name one. The answer spells the stored member in full — `Active`, `Disabled` — the convention the stream responses use. |
| `guaranteedSpeedKbps` | int | 0..1 000 000. **`0` means unknown.** The unit is in the name; the UI converts Mbps ↔ kbps and the API never does. |
| `timeshiftOnly` | bool | whether the server only serves timeshifted content. |
| `hostname` | string? | ≤ 253, trimmed; blank is stored as `null`. |
| `vpnIp` | string? | the address rule when present; blank is stored as `null`. |
| `countryCodes` | string[] | every element an ISO 3166-1 alpha-2 code, canonicalised to upper case by `Fluxy.Core/GeoIp/Utils/CountryCodes.Normalize`; de-duplicated preserving first occurrence; at most 249. A null list is stored as `[]`. |
| `isMain` | bool | **response only** — the seeder's flag, never writable. |

The three numeric sentinels are real states rather than missing values, so the columns are
non-nullable and declare no database default:
**`clientCount 0 = unlimited`, `guaranteedSpeedKbps 0 = unknown`, `latency 0 = not measured`.**

Validation keys are the C# property names the service reports (`ClientCount`, `CountryCodes`,
`VpnIp`, …); `FieldErrorKeys` rewrites them to the camelCase names the wire uses, so the client
always reads `clientCount`, `countryCodes`, `vpnIp`.

### The reference direction: three columns point *at* a server

`subscriptions.preferred_server_id`, `video_streams.transcoding_server_id` and
`video_streams.archive_server_id` are nullable `uuid` columns, each a foreign key to `servers(id)`
with **`ON DELETE RESTRICT`** and an index of its own. `ServerService.DeleteAsync` counts the three
before it removes anything:

```
existence (404 server_not_found)  →  main row (409 server_immutable)  →  references (409 server_in_use)
```

**The count is mandatory, not belt and braces.** FK `RESTRICT` alone would turn a referenced delete
into a provider error (a 500); asking first answers a code and leaves the row exactly where it was.
The race is accepted: a reference committed between the count and the delete still reaches the
foreign key and turns that delete into a provider error (a 500) rather than a `409`; the count
narrows the window and `RESTRICT` is the backstop. The pinned order is what the unit and integration
suites assert: a missing identifier can be neither the main row nor "in use", and the main row's
flag dominates the count.

**The timeshift-toggle seam is accepted (owner decision O1).** `PATCH /admin/servers/{id}` is a
full write with no stream knowledge, so an operator may flip a server already bound as a permanent
stream's transcoder to `timeshiftOnly = true`; no reference check is added there. The stream write
over that binding refuses itself on its next save (`400 validation_failed` keyed
`transcodingServerId`), which is how the seam self-reports. Nothing else in the servers surface
knows about the three columns.

`GET /admin/servers/options` exists for the two forms that need to *choose* a server: it is a bare
array ordered by name, it includes the seeded main row like any other, and it is gated by
`ViewServerOptionsPolicy` — Admin plus an "any of" `ViewSubscriptions` / `ViewStreams`. The
`viewServers` pair deliberately does **not** admit it: reading a name to fill a picker is part of
editing the subscription or the stream, and browsing the table is a different job.

### The write shapes

- **`POST` is a create.** Only `name`, `ip`, `port`, `osName` and `networkInterface` are strictly
  required; the rest are resolved by the service (`clientCount = 0`, `status = active`,
  `guaranteedSpeedKbps = 0`, `timeshiftOnly = false`, `countryCodes = []`, `latency = 0`).
- **`PATCH` is a full write, not a partial update.** The form holds one server, so the body is
  the whole row: an omitted text is refused as the blank it becomes, an omitted `clientCount` is
  the `0` that means unlimited, an omitted `timeshiftOnly` is `false`, and an absent or unknown
  `status` is refused under `status`. This is the convention the tariff plan, the streams and the
  transcoding profile already follow.
- **`isMain` never appears in a request**, so no operator can promote a row or demote the main one.

### The query contract

The page defaults to `page=1`, `pageSize=20`, `sortBy=name`, `sortOrder=asc`. The bounds are
**refused rather than clamped** (`pageSize ≤ 1000`, `search ≤ 200 characters`), and a value that
names nothing is **refused under its own parameter** (`400 validation_failed`) rather than treated
as "no filter". The sortable columns are `name`, `status`, `latency`, `hostname`, `ip`,
`clientcount`, `createdat`.

Search is a case-insensitive substring of the **name**, the **host name** or the **address**,
applied before the count, so `total` is the size of the filtered list. Rows without a host name
sort **last** in either direction, and every order ends on the identifier so two pages cannot show
or skip a row written in the same instant.

### The main row: seeded, editable, not deletable

| Question | Answer |
| --- | --- |
| Identifier | `00000000-0000-0000-0000-000000000401` (`MainServer.Id`) |
| Created by | `ServerSeeder` at startup, **after** the user seeder and the migrations |
| Editable? | **Yes** — `PATCH` writes it like any other row |
| Deletable? | **No** — `DELETE` answers `409 server_immutable` and the row survives |
| `isMain` written by | the repository, from `MainServer.IsMain(id)`; never by a request |

The seeder is **idempotent**: `ExistsMainAsync` is asked first and a row that exists is left
exactly as the operator left it, name included. It **never throws** — a failed address probe or a
refused write is logged and the boot continues, because an installation without its main row is
still an installation and the operator can add the row by hand.

**The main row is measured in-process on the dashboard, so it is available from the first request
even though its address does not answer.** Its `ip` and `port` are an address clients are handed,
not a socket the statistics live behind, so the dashboard does not dial them for this row:
`SelfServerStatsProbe` answers it from a local sampler (processor time, working set, the volume
under the working directory, whole-interface network counters and process uptime) and delegates
every other row to the HTTP probe. The figures therefore describe **this process**, not the machine
and not the relayed media; the field-by-field statement of what each one means, and the window
bounds that keep the rates describing the last minute rather than an idle hour, are in
[admin-dashboard.md](admin-dashboard.md) (D11–D13). A `Disabled` main row is still measured and may
show `available: true` — the status says whether the installation should relay through it, the
availability says whether the probe answered, and those are two different facts. `admin/servers`
itself is unchanged: the row remains editable, and the delete refusal is still the only special
case.

**Ordering matters in `DeleteAsync`:** the existence check runs first, then `MainServer.IsMain(id)`.
A missing identifier therefore answers `404 server_not_found`, never `409 server_immutable` — only
a row that exists can be refused for being the main one. The integration suite pins that order.

### The identity chain

`ServerIdentityProvider` answers the four values the seed writes, configuration first and the
machine second:

| Fact | Order |
| --- | --- |
| `Ip` | `Servers:MainIp` if it parses → the host's non-loopback IPv4 (`Dns.GetHostAddresses`, then `NetworkInterface`) → `MainServer.FallbackIp` = `127.0.0.1` |
| `Port` | `Servers:MainPort` if 1..65535 → the first port of the `Urls` configuration value → `MainServer.FallbackPort` = `5159` |
| `OperatingSystem` | `RuntimeInformation.OSDescription`, trimmed and cut to 100 → `MainServer.FallbackOsName` = `unknown` |
| `NetworkInterface` | the literal `MainServer.DefaultNetworkInterface` = `eth0` (no NIC enumeration) |

The `Urls` parser is deliberately textual rather than `Uri`-based: `http://*:5159`, `http://+:5159`,
`http://0.0.0.0:5159`, `https://[::]:7221` and `http://localhost:5159;https://localhost:7221` all
name their first port, and an entry with no port (`*`, `+`, `http://localhost`) is skipped.

Both overrides exist because a container cannot always discover the address it is reached at from
inside. Neither is required: without them the address comes from the host probe and the port from
`Urls`, and only a host with no usable interface reaches `127.0.0.1:5159`.

**What this machine produced, observed:** with nothing configured the seeder logged
`172.31.0.1:5159` (the host's own IPv4 from `Dns.GetHostAddresses`, the port from the `http`
profile's `Urls`) and wrote exactly that row; with `Servers__MainIp=10.99.0.7` and
`Servers__MainPort=7000` in the environment it wrote `10.99.0.7:7000` instead. `OSDescription`
resolved to `Microsoft Windows 10.0.19045`. A second boot with the row already present seeded
nothing and left `created_at`, `ip` and `port` untouched — the idempotence the seeder promises.

### Codes → statuses

| Code | Status | Meaning |
| --- | --- | --- |
| `server_created` | 201 | the row exists; `Location` names it |
| `server_updated` | 200 | every editable field was written |
| `server_deleted` | 200 | the row is gone |
| `server_not_found` | 404 | no row carries that identifier |
| `server_already_exists` | 409 | another row holds the name (pre-check or the unique index) |
| `server_immutable` | 409 | the request addressed the main row with `DELETE` |
| `server_in_use` | 409 | a subscription or a stream still names the row, so `DELETE` refused it |
| `validation_failed` | 400 | one or more fields, keyed by their camelCase names |
| `csrf_invalid` | 400 | the antiforgery pair did not validate |
| `permission_denied` | 403 | the group holds neither the pair's read grant nor its write grant |

### The permission pair

`ViewServers = 1L << 32` and `EditServers = 1L << 33`, both owned by `Admin`, registered in
`UserPermissionCatalog.All` and `Owner`. Reads carry `ViewServers`, every write `EditServers`; the
enum is already `long` and the column `bigint`, so the pair needed no widening and no migration.

The picker is the one action outside that pair:
`ViewServerOptionsPolicy = "Permission.ViewServerOptions"` is an "any of" policy over
`ViewSubscriptions` and `ViewStreams` (with the same `Admin` role requirement every policy here
carries). No `UserPermission` member, catalog entry, migration or frontend permission mirror was
added for it — it is a policy name, not a grant.

### Layering

`Server`, `ServerIdentity`, `NewServer`, `ServerPatch`, `ServerWrite`, the enums, `ServerView`,
`ServerOption`, `ServerReference`, `ServerPage`, `ServerOutcome`, `MainServer`, `ServerLimits`,
`ServerListLimits`, `IServerService`, `IServerIdentityProvider`, `IServerSeeder` and
`IServerRepository` in `Fluxy.Core/Servers/`. `ServerRules` and the three services in
`Fluxy.Application/Servers/`. The entity, configuration, repository and the `AddServers` and
`AddServerReferences` migrations in `Fluxy.DataAccess`. The contracts (`AdminServerResponse`,
`ServerOptionResponse`, `ServerResponses`) in `Fluxy.API/Contracts/Servers/`, the parser in
`Controllers/Support/`, and `ServersController` in `Controllers/Admin/` — `admin/servers` is an
admin-area route that belongs to no content section.

`ServerRules` sits in Application rather than reusing the API layer's blacklist address helper: a
domain rule must not live above the layer that judges it. The *rule* is the same
(`IPAddress.TryParse` plus the column length), written once where both layers can reach it.

The reference columns are read by two other domains — `ISubscriptionRepository` and
`IVideoStreamRepository` resolve the names with correlated subqueries, and the subscription and
stream services call `IServerRepository.GetReferenceAsync` for the existence probe and the
timeshift flag. Application to Core interfaces only, so the layering rule holds.

### Verified by running

`dotnet build` clean, `dotnet build -c Release` clean, the unit suite green (the `Servers` folder:
every bound at its edge and one past it, every normalization, the uniqueness pre-check and the
index race, the three delete refusals in their required order, the options delegation and the
identity chain), the integration suite green (`Admin/ServersTests.cs`: the wire shape, the name
rule, search over three columns, sort by latency and created-at, the query refusals, the seeded
main row and its identity values, the main row's editable-but-not-deletable rule, both write
`404`s, the `409 server_in_use` guard, the options endpoint's three grant cases, CSRF and the two
grants), and `dotnet ef migrations has-pending-model-changes` reports no changes.
