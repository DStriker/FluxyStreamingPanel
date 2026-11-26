# The admin dashboard: `GET /admin/dashboard`

> Part of the FluxyBackend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).

One read that answers the whole dashboard screen: installation KPIs, one row per server with
per-server connection aggregates, and live telemetry probed from each server.

| Endpoint | Auth | Answer |
| --- | --- | --- |
| `GET /admin/dashboard` | Admin + `viewSubscriptions` **and** `viewStreams` **and** `viewServers` | `{ kpis, servers, serversUnavailable, generatedAt }`, `Cache-Control: no-store` |

There is no other method on the route: the body is a composition of several aggregates plus a live
probe, so there is no row to address and no collection to page.

## The decisions this document records

### D1 / A1 — the remote statistics contract is an assumption

The payload the probe reads is **`GET http://{ip}:{port}/api/stats`**, with
`Authorization: Bearer <Servers:StatsToken>` when that setting is non-empty, a **~2 second**
timeout, and a JSON body:

```json
{ "cpuPercent": 0, "ramPercent": 0, "diskPercent": 0,
  "uptimeSeconds": 0, "networkInMbps": 0, "networkOutMbps": 0 }
```

**This is an assumption (A1), not a published agent protocol.** A server's own agent does not
exist yet, so the shape was chosen as the simplest one that carries the six facts the screen
draws. It is isolated in exactly two places in one file — `HttpServerStatsProbe` and its private
nested payload type — so a real agent payload later is a **one-type change**: no service, no read
model, no controller and no contract moves.

Plain `http` only. There is no TLS handling, no certificate validation and no scheme choice: an
agent that needs TLS changes the adapter, not this document.

A malformed address or an out-of-range port answers **unavailable**, never an exception. The
`ServerStatsUri` helper parses the address (`IPAddress.TryParse`), brackets IPv6 literals and
returns false for anything else.

### D2 — attribution is by the real `server_id`, and `NULL` is unattributed

`connection_logs.server_id` is the only source of a per-server connection figure — and it is
**opaque**, exactly like `stream_id`: no foreign key, because the relaying process may name a
server row this installation has no record of. `RESTRICT` would make every server that ever served
a connection undeletable; `CASCADE` would destroy the evidence the log exists for.

Attribution rule:

- A live row with `server_id IS NULL` is **unattributed**.
- It **counts** in `kpis.activeConnections` and in `kpis.usersWithActiveConnections`.
- It **contributes to no server row**.
- `?serverId=<uuid>` on the connection log matches the real column only, so no filter value can
  select the unattributed tail.

**The per-server user figure therefore becomes accurate as the capture path starts writing
`server_id`.** Until then the difference between the installation figure and the sum of the server
rows is exactly the unattributed tail — that is expected, not a bug.

Dormant/write side today: the capture path that will write real rows belongs to the relaying
process; the only writer in this pass is the Development seeder, which now sets
`ServerId = subscription.PreferredServerId ?? MainServer.Id`.

### D3 — the gate is admin + three single-permission requirements stacked

`ViewDashboardPolicy = "Permission.ViewDashboard"` has `RequireAuthenticatedUser()` and **four**
requirements on one policy: the `Admin` role, then one `PermissionRequirement` each for
`ViewSubscriptions`, `ViewStreams` and `ViewServers`.

`PermissionRequirement` is internally **"any of"** — its handler succeeds when the group holds
any one of the permissions it names. So "all of" is expressed as three separate
single-permission requirements: ASP.NET Core requires *every* requirement of a policy to pass, and
that combinator is the opposite of the requirement's own name. A single requirement naming all
three would admit an operator holding just one — exactly the wrong answer here.

No new `UserPermission` member, no catalog entry, no permission migration and no frontend permission
mirror. It is a policy name, not a grant.

### D4 — stream definitions

- **Running** = `VideoStreamStatus.Running`.
- **Stopped** = `VideoStreamStatus.Stopped`.
- No bouquet clause: a stream counts where its row says it is, regardless of what packages it.

One grouped read over `video_streams` produces both figures; a status no stream holds is absent
from the group and reads as zero.

### D8 — the network totals cover available servers only

`kpis.networkInMbps` / `kpis.networkOutMbps` sum the servers that answered the probe. The response
carries `serversUnavailable` so a client can label the total as partial — without it a quiet
installation and an installation with a dead machine would draw the same number.

### D9 — every server row is returned

`servers` holds one entry per `servers` row, ordered by name, `ServerStatus.Disabled` included. A
disabled server is a fact the operator configured; the screen matches the servers table. A server
that did not answer is still a row: `available: false` with zeros in every numeric telemetry field.

### D10 — no new dependency

`Microsoft.Extensions.Http` (`IHttpClientFactory`) is already referenced by `Fluxy.Application`, and
nothing else was added: no telemetry package, no redis package in Application, no new project
reference. The cache is a Core interface implemented in `Fluxy.DataAccess`, which already owns
`StackExchange.Redis`.

> The ratified list handed to a later change named D1–D4, D8, D9 and D10. D5–D7 were not part of it
> and are deliberately not invented here; this document records only what was decided. D11–D14 were
> added by the change that answered the main server row in-process (owner decisions S1–S5).

### D11 — the main server row is measured locally (S1)

**The main row never leaves the process.** The dashboard asks `IServerStatsProbe` for every row as
before, but the registration the dashboard resolves is `SelfServerStatsProbe`, a decorator: it
answers the row whose identifier is `MainServer.Id` from `SelfServerStatsSampler` and delegates
every other row to `HttpServerStatsProbe`.

The rule is the **identifier** (`MainServer.IsMain(server.Id)`), never the stored `Server.IsMain`
column. The column is a fact the seeder wrote; the identifier is the rule the delete refusal and
the seeder already share, so a row edited by hand cannot change which machine is described
locally. A row called anything and marked `Disabled` is still the main row.

Why this exists: no server on this installation exposes the assumed `GET /api/stats` route yet, and
the main row's configured `ip`/`port` is an editable starting point for clients rather than a
socket the statistics live behind. Before this decision every row - the main one included -
answered `available: false` with zeros, including on the machine that was serving the request.

**The stored `ip` and `port` of the main row no longer drive its telemetry.** They still drive
`GET /admin/servers` and still name the address clients are handed; they are simply not what the
dashboard dials for the main row.

`IServerStatsProbe`, `DashboardService`, the controller and the wire contract are unchanged: six
numbers, same types, `available` still derived from "a sample came back".

### D12 — the six local figures, stated exactly (S3, S4)

The main row keeps the same field names as a remote row, but the facts behind them are narrower.
They are written out here because printing a number the system cannot actually claim is the
failure this pass exists to prevent.

| Field | What the main row measures | What it is **not** |
| --- | --- | --- |
| `cpuPercent` | `Process.TotalProcessorTime` delta over wall-clock delta, divided by `Environment.ProcessorCount`, ×100, clamped to 0..100. **This process only.** | Not the machine's load: another process busy on the same host is invisible. |
| `ramPercent` | `Process.WorkingSet64` over `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes`. **This process's working set against the runtime's budget.** | Not the host's physical RAM. Inside a container the budget is the container's, so the figure reflects the container. |
| `diskPercent` | Used space over total size of the `DriveInfo` for `Path.GetPathRoot(Environment.CurrentDirectory)`. | Not every mounted disk: it is the one volume the process can reach from where it runs. |
| `uptimeSeconds` | Seconds since `Process.StartTime`. **Process uptime.** | Not host uptime: **a restart resets it to zero.** It also differs in kind from what a remote agent would report, because a remote agent measures its machine while this measures one process - which is why the shared field is documented as "how long the measured instance has been up". |
| `networkInMbps` | Delta of `BytesReceived` summed over `NetworkInterface.GetAllNetworkInterfaces()`, converted to decimal megabits per second. | Not the relayed media only. **It is a whole-interface counter: database, Redis, HTTP and management traffic all count.** This is a deliberate owner acceptance, not an oversight. |
| `networkOutMbps` | The same for `BytesSent`. | Same limitation. |

Because the main row is now `available: true`, its network rates contribute to
`kpis.networkInMbps` / `kpis.networkOutMbps` like any other available row (D8 still sums available
rows only). The installation total therefore includes the main process's non-relay traffic. That
is the same accepted limitation seen from the KPI row.

### D13 — the window is bounded, and a cold start answers (S2)

The processor and the network figures are **rates**, and a rate needs a window. The sampler keeps
the earlier `(TotalProcessorTime, byte counters, instant)`. The window it computes over is now
**bounded on both sides**:

- **`MinimumWindow = 250 ms`.** Two readings taken closer together turn scheduler jitter into a
  violent rate spike - a few milliseconds of extra processor time over a five-millisecond window
  reads as a busy machine, and a handful of bytes reads as a burst of traffic. A quarter of a
  second is short enough to still describe "now" and long enough for the counters to mean
  something.
- **`MaximumWindow = 60 s`.** Beyond a minute the average stops describing "now": a dashboard
  opened after an idle hour would otherwise show that hour's traffic, which on a quiet
  installation is near zero, while the machine may be busy at the moment the screen is opened.

The cases in `ReadAsync`:

| Previous reading | Window used |
| --- | --- |
| none (first request after a restart) | take a reading, **wait 250 ms**, take a second, compute over that short window |
| gap below `MinimumWindow` | same short window; a fresh sample cannot be computed from a gap that small |
| gap inside `MinimumWindow`..`MaximumWindow` | the natural gap, with no wait and no extra reading |
| gap above `MaximumWindow` | replaced by the short window measured now, not averaged over the gap |

**So the main row is available from the very first request after a restart**, and its CPU and
network figures always describe the last at most a minute. A cold start (or a build after a long
idle period) pays one 250 ms wait; that is the deliberate cost of not printing a number the
system cannot claim. Sleeping for a window was rejected in the first pass because it would make a
screen refresh block - that objection is answered by the memo below rather than by leaving the row
unavailable.

**One wait per burst, not one per caller.** The work runs behind a `SemaphoreSlim(1,1)`, and the
last computed sample is memoised for `MinimumWindow`. A caller that arrives while the memo is
fresh is answered immediately; a caller that waited on the gate re-checks the memo before doing
any work. So several tabs or a cache miss under Redis fail-open pay one wait and the rest answer
at once. No `lock` is held across an `await`, and the gate is released in a `finally` on every
path, including a throw from the machine reader.

**Nothing is fabricated.** If the local read genuinely fails - a runtime that reports no memory
budget, a drive with no total size, a platform where `Process.StartTime` throws, a machine with no
readable interface, a counter that went backwards - the sampler answers null and the decorator
falls through to `HttpServerStatsProbe`, whose answer for a main row in today's installation is
null. The row is then `available: false` with zeros, exactly like any other unreadable server. A
caller that cancels answers null for **that request only**; the cancellation is not restated as a
machine failure and never reaches `DashboardService` as an exception.

### D14 — the main row can be Disabled and Available at once (S5)

`ServerStatus.Disabled` is the operator's statement that the installation should not relay through
the row; `available` is whether the machine answered the probe. They are different facts about the
same row and the main row may carry both. No refusal was added to `ServerService.UpdateAsync`, and
`admin/servers` is unchanged: disabling the main row hides nothing and breaks nothing on the
dashboard, and re-enabling it needs no special case.

## The cache

- Adapter: `RedisServerStatsCache` (`Fluxy.DataAccess/Infrastructure/Redis/`), singleton.
- Key: **`fluxy:dashboard:v1`**, TTL **10 seconds**.
- Serialization: `System.Text.Json` (`JsonSerializerDefaults.Web`).
- **Single-flight**: a miss takes one `SemaphoreSlim(1,1)`, re-reads the key inside the gate, then
  builds. Two simultaneous misses build once; the second serves what the first stored.
- **Fail-open**: an unconfigured, unreachable or erroring redis degrades to building directly. A
  cache failure never fails the request.
- An **undeserializable** stored value (an older shape) is treated as a miss: logged, rebuilt,
  overwritten, never thrown.
- Exceptions from the build delegate are **database failures** and propagate; the gate is released
  in a `finally` either way.

The cache is per process, which is the right scope for a ten-second window: a redis lock would
have worse failure modes than two builds.

## Layering

`DashboardSnapshot`, `DashboardKpis`, `DashboardServer` and `ServerStatsSample` in
`Fluxy.Core/Servers/ReadModels/`; `IDashboardService`, `IServerStatsProbe` and `IServerStatsCache`
in `Fluxy.Core/Servers/Interfaces/`; the grouped read rows in `Fluxy.Core/Subscriptions/Rows/` and
`Fluxy.Core/Streams/Rows/`. `DashboardService`, `SelfServerStatsProbe`, `HttpServerStatsProbe`,
`SelfServerStatsSampler`, `SelfServerStatsMachineReader`, `SelfServerStatsMath`, `SelfStatsReading`,
`ServerStatsOptions` and `ServerStatsUri` in `Fluxy.Application/Servers/`. The cache beside the
other redis stores and the four repository methods in `Fluxy.DataAccess`. The contracts
(`AdminDashboardResponse`, `AdminDashboardKpisResponse`, `AdminDashboardServerResponse`) in
`Fluxy.API/Contracts/Dashboard/` and `DashboardController` in `Controllers/Admin/`.

`Fluxy.Application` gains no EF Core, redis or MaxMind reference: the probe uses
`IHttpClientFactory` (`Microsoft.Extensions.Http`, already referenced), the local sampler uses BCL
only (`System.Diagnostics`, `System.Net.NetworkInformation`, `System.IO`), and the cache is a Core
interface implemented in DataAccess.

**The DI shape.** `HttpServerStatsProbe` stays registered under its own name - but as a **keyed**
singleton (`SelfServerStatsProbe.RemoteKey`), not as the unkeyed `IServerStatsProbe` - and
`AddHttpClient(nameof(HttpServerStatsProbe), …)` is untouched. The unkeyed `IServerStatsProbe` is
the decorator, which asks for the keyed remote through `[FromKeyedServices]`. Two unkeyed
registrations would leave one silently winning; injecting the unkeyed contract into the decorator
would ask the container to build it from itself. `SelfServerStatsSampler` is a singleton because
it holds the previous reading between requests.

## The five reads, and no N+1

`DashboardService.BuildAsync` makes five queries, sequentially (one scoped context is not safe for
concurrent commands):

1. `IServerRepository.GetAllAsync` — every row, ordered by name.
2. `IConnectionLogRepository.ReadLiveServerCountsAsync` — live rows grouped by `server_id`,
   with `COUNT(DISTINCT subscriptions.user_id)`; the null group is kept.
3. `IConnectionLogRepository.CountLiveUsersAsync` — `COUNT(DISTINCT subscriptions.user_id)` over
   every live row.
4. `ISubscriptionRepository.ReadPreferredServerUserCountsAsync` — non-null
   `preferred_server_id` grouped, distinct owners.
5. `IVideoStreamRepository.ReadStatusCountsAsync` — grouped by stored status.

The telemetry is then probed for all servers with `Task.WhenAll` bounded by `ProbeConcurrency = 4`
(a `SemaphoreSlim`). A probe that returns null **or throws** leaves the row with zeros and
`available: false`; one unreachable machine never fails the snapshot.

## Codes and refusals

| Case | Answer |
| --- | --- |
| No session | `401` through the shared `{ code, message }` envelope |
| A client session (the class-level role gate) | `403 auth_role_changed` |
| Admin missing any of the three view grants | `403 permission_denied` |
| Everything else | `200` with the snapshot |

## Verified by running

`dotnet build FluxyBackend.slnx` clean (and `-c Release -warnaserror` clean); unit suite green at
**1413** tests (the new `Servers` folder: every address and port shape of `ServerStatsUri`, the
probe's full exception matrix - 500, non-JSON, `{}`, a missing field, a non-finite number, an
unreachable server, a client timeout, a malformed address and the caller's own cancellation - and
`DashboardService`'s composition: zeros for an unavailable server and its exclusion from the
network total, the unattributed group in the installation totals, a cache hit that neither reads
nor probes, six servers with `MaxActive <= 4`, and a throwing probe that still yields a normal
snapshot). The local sample has its own two files: `SelfServerStatsMathTests` proves the CPU clamp,
the zero-denominator refusals, the floored uptime and the decimal-Mbps conversion, and
`SelfServerStatsSamplerTests` proves the cold start answering over its own short window, the
minimum and maximum bounds (a stale reading replaced rather than averaged over the gap), the memo
that makes a burst of eight callers read the machine twice, cancellation answering null, and the
failed read that still releases the gate; `SelfServerStatsProbeTests` proves the decorator's two
branches, the identifier-not-flag rule, the disabled-main case and the cancelled request that is
answered unavailable without being restated as a machine failure. Integration suite green at
**459** tests (the dashboard endpoint's wire shape, its no-store header, the all-three-grants gate,
401 for an anonymous caller, the role refusal for a client session, two failing probes counted and
excluded from the network total, the second request performing no extra probes, a factory with no
redis string still answering, the main row measured locally **from the first request with no
priming** while an unreachable non-main row stays unavailable with zeros, plus the four grouped
reads and the connection log's new server filter); `dotnet ef migrations
has-pending-model-changes` reports no changes.

Verified by hand against a running `dotnet run --project Fluxy.API --launch-profile http` with a
throwaway admin: `GET /admin/dashboard` answered 200 with `Cache-Control: no-store` and one row per
server; two immediate calls carried the same `generatedAt` (the cache hit); `?serverId=banana`
answered 400 with `errors.serverId`; an unknown uuid answered `{ items: [], total: 0 }`.

After the local main-row change, the same boot was repeated with a throwaway admin. The first call
answered `available: false` with zeros for the main row — the first-call policy of that pass — and
`serversUnavailable` was 2. That policy has since been replaced by the bounded window below, so the
record of it is kept only as the state that was observed.

**Bounded window, observed.** A fresh boot with a throwaway admin, two calls in a row immediately
after startup, both `200`:

- first build: `available: true`, `uptimeSeconds: 5`, `cpuPercent: 5.7816…`, `ramPercent: 1.2201…`,
  `diskPercent: 96.6872…`, `networkInMbps: 1.1692…`, `networkOutMbps: 0.1416…`,
  `serversUnavailable: 1`
- second build (same `generatedAt` — the cache hit, so the same sample)

The planted unreachable row (`123.123.123.123:8000`) stayed `available: false` with zeros. After a
deliberate ~70 second idle pause a third call answered `available: true`, `uptimeSeconds: 95`,
`cpuPercent: 0.0736…`, `ramPercent: 1.2469…`, `diskPercent: 96.6859…`, `networkInMbps: 1.9737…`,
`networkOutMbps: 4.0284…` — a figure measured over the fresh 250 ms window, not the
70-second average. The throwaway account and its `user_logs` rows were deleted afterwards.
