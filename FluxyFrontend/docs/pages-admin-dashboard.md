> Part of the FluxyFrontend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).

## The admin dashboard

> `src/pages/admin/DashboardPage.tsx`, behind the Admin rail's `dashboard` item, reading
> `GET /admin/dashboard`. The client and reseller areas still open
> `src/pages/account/DashboardPage.tsx`, the greeting-plus-placeholders page.

### Why there are two dashboards

The two screens answer different questions and only the admin one has data behind it. The
account page greets the signed-in visitor and draws three "under construction" statistic
slots; this page reads the installation's snapshot behind the endpoint's gate — the Admin
role plus `viewSubscriptions` **and** `viewStreams` **and** `viewServers`.

Only the Admin `dashboard` item's `page:` loader in `src/utils/navigation.ts` points at the
admin page. The Client and Reseller entries still point at
`../pages/account/DashboardPage`, and `ADMIN_HOME_ROUTE` / `CLIENT_HOME_ROUTE` /
`RESELLER_HOME_ROUTE` are untouched, so `/admin/dashboard` is the same landing path the
server names in `redirect`. `probe:render` resolves all three loaders and asserts exactly
that split; a lazy loader swapped by accident compiles and the rail looks identical, which
is why the check exists.

### The frame

There is no outer page card. The page is a heading row, the KPI cards and the server cards:

```
<Flex justify="space-between" align="center" gap={12} wrap style={{ marginBottom: 16 }}>
  <Typography.Title level={4} style={{ margin: 0 }}>{t('nav.items.dashboard')}</Typography.Title>
  <Flex align="center" gap={8}> …toolbar… </Flex>
</Flex>
…the state switch (error alert / stale alert / KPI row / server row)…
```

The heading row sits **above** the state switch, so the title is present in loading, failed and
empty states too — the purpose of the page-title rule in [ui-rules.md](ui-rules.md), kept
structurally without the card. The `Flex` wraps rather than squeezing either half, and the
`Typography.Title` has **no** `whiteSpace` override: a heading longer than the shortened RU
label wraps between words rather than being squeezed, and the toolbar drops to a line of its
own as soon as the two no longer fit (measured at 320 px: EN wraps, the shorter RU heading
still fits beside it, neither clips). No breakpoint logic and no `Divider` are involved.

The toolbar keeps, in order:

1. the icon-only auto-refresh toggle — a `Button` whose accessible name is the action
   **and** the interval ("Pause auto refresh (Refreshes every 30 seconds)"), wrapped in
   `hinted` so its tooltip opens over the button; and
2. a secondary "Updated at HH:mm:ss" text — drawn once a snapshot exists, dimmed to 0.5
   opacity while a re-read is in flight.

The heading reuses `nav.items.dashboard`; no locale key was added for it. The greeting card is
deliberately absent: the signed-in shell's header already names the account.

### The KPI row

`<Row gutter={[16, 16]}>` with four count cards (`xs={12} sm={12} md={12} lg={5}`) and the
network card (`xs={24} sm={24} md={24} lg={4}`):

| Card | Value | Icon | Link target |
| --- | --- | --- | --- |
| Active connections | `kpis.activeConnections` | `ApiOutlined` | `liveConnectionsPath(area)` |
| Users online | `kpis.usersWithActiveConnections` | `TeamOutlined` | `liveConnectionsPath(area)` |
| Active streams | `kpis.streamsRunning` | `VideoCameraOutlined` | `videoStreamsPath(area)` |
| Disabled streams | `kpis.streamsStopped` | `StopOutlined` | `videoStreamsPath(area, 'Stopped')` |
| Network in | `kpis.networkInMbps` | `ArrowDownOutlined` | — |
| Network out | `kpis.networkOutMbps` | `ArrowUpOutlined` | — |

Every icon is the first child of its `Statistic` title node and carries `aria-hidden`, so the
title's accessible name stays exactly the translated label and the drawn icon is never the only
signal. The network pair takes one arrow each because one icon cannot stand for two directions.
The icons take no explicit colour or size — the title's own styling carries them — and sit on a
4 px inline end margin rather than inside a `Space`. `Statistic`'s `prefix` was rejected: it sits
beside the number, not the label.

Every count card is `height: '100%'` and every `Statistic` sets
`styles={{ content: { fontVariantNumeric: 'tabular-nums' } }}`, so the four cards line up
and the digits do not shuffle as numbers change. The card's `title` is a real
`Typography.Link` — an `<a href>` — rather than a click handler on the number, so a
keyboard and a screen reader meet the destination before the value. The link is set at
12 px with `wordBreak: 'normal'`, because antd's typography base sets
`word-break: break-word` and would otherwise split a long Russian word mid-syllable in a
147 px card on a 320 px phone.

The number grouping is driven by the resolved i18n language: the page reads the locale's
own group and decimal separators off `Intl.NumberFormat.formatToParts` and hands them to
`Statistic`, because antd's defaults are English (`,` and `.`) and a Russian page must
group `1234` as `1 234` and print `0,0`.

The **network card** is one card holding two statistics side by side — incoming and
outgoing — each `precision={1}` with an `Mbit/s` suffix at 12 px, `fontSize: 20` on the
figures and 12 px card padding, because a `lg={4}` column is about 168 px and the default
24 px figure would wrap the pair onto two rows. There is no combined total: the two
directions are separate measurements and their sum is a number no server reported. When
`serversUnavailable > 0` a secondary 12 px line states the totals cover reachable servers
only and names the count (D8).

### The server cards

`<Row gutter={[16, 16]}>` with each card in `<Col xs={24} md={12} xl={8}>` and
`style={{ height: '100%' }}` on the `Card`. One card per server, in the server's order.

- **Title** — a `Typography.Link` carrying the server name, linking to
  `liveConnectionsPath(area, server.id)`, plus a `Tag` for `Disabled`. The tag is the only
  place the status is drawn; an `Active` server gets no tag.
- **Body** — a `<Descriptions column={1} size="small">` with active connections,
  preferred-server users, users online, network in, network out and
  uptime. Counts are integers; the two network lines are one-decimal (`formatMbps`);
  uptime is the compact `formatUptime` form (`12d 4h 31m`, `<1m`).
- **Rings** — a `<Flex justify="center" align="flex-start" gap={8} wrap={false}
  style={{ marginTop: 12 }}>` holding three `<Progress type="circle" size={72}
  strokeWidth={8} … />` for CPU, RAM and disk, each under a 12 px label coloured with
  `token.colorTextSecondary`. `justify="center"` centres the group in the card body at every
  column width; there is no `maxWidth` because there is no left-anchored group left to stop
  stretching. Three rings at 72 px plus two 8 px gaps is 232 px, which with 32 px of card
  padding is 264 px — inside a 320 px phone, so the three stay three-across at every width
  instead of wrapping one onto a line of its own.

The wrapper around each ring carries
`aria-label="CPU: 42%"` (the metric's translated name and its rounded value), so the
reading is reachable without reading the SVG label.

#### Ring tiers

`clampPercent` first, then `ringTier`:

| Tier | Range | Stroke | Status |
| --- | --- | --- | --- |
| normal | `< 70` | `token.colorPrimary` | — |
| warn | `70 – 89.9` | `token.colorWarning` | — |
| critical | `≥ 90` | `token.colorError` | `exception` |

Both thresholds are inclusive on the tier they name — exactly 70 is a warning and exactly
90 is critical. The rail under every ring is `token.colorFillSecondary`.

### D5 — an unavailable server draws zeros, not dashes

`server.available === false` means the probe could not reach the machine. The card then
draws:

- every count as `0`, every throughput as `0.0` — **not** an em dash;
- uptime as `<1m` (the `formatUptime` answer for zero);
- all three rings at 0 % in the neutral rail, grey rather than red;
- a `Badge status="default"` with the "Server unreachable" text at the top of the body.

The **badge** is what distinguishes "unreachable" from "legitimately zero"; the numbers
are always numbers. Grey, not red: an unreachable machine is a gap in the measurement, not
a critical load. No `Alert` is involved. At the page level the partial-total note under the
network statistics is the companion — the totals cover reachable servers only.

### D6 — refresh

Auto refresh is **on by default at 30 seconds**, and it is the `ConnectionLogsPage` pattern
verbatim:

- a `useRef<() => void>` holds `() => void query.refetch()`, assigned on every render;
- a `useEffect` keyed on the toggle installs `setInterval(refreshRef.current, 30_000)` and
  clears it on unmount or on pause.

No `refetchInterval`, no per-page `QueryClient` options. The shared client's
`staleTime: 0`, `retry: false`, and focus/reconnect refusals stay exactly as
`src/utils/queryClient.ts` states. The timestamp is `query.dataUpdatedAt` — when this
browser received the answer — not the server's `snapshot.generatedAt`, which can sit behind
the endpoint's ten-second cache.

### The six states

| State | What is drawn |
| --- | --- |
| 1. First load | Four KPI `Statistic`s in `loading` state; three skeleton server cards (`<Card loading />`); no zeros. |
| 2. Refreshing | Values stay, the toggle's button spins, the "Updated at" text dims. |
| 3. Read failed, no data | Error `Alert showIcon` with the translated heading and the server's reason, plus Retry. **The KPI row is not drawn.** |
| 4. Refresh failed over data | The values stay under a slim warning `Alert` saying they are from `HH:mm:ss`, plus the same Retry. |
| 5. Server unavailable | D5: zeros + badge, grey rings. |
| 6. Empty server list | One full-width `<Col xs={24}>` with `<Empty />` and its description. |

States 3 and 4 are distinguished by `data`'s presence, not by `isError` alone; states 1
and 2 by `isPending`, which is true only before the first answer. A failed first read never
renders zeros, and a failed refresh never throws away the last good snapshot.

### Deep links

Built in `src/features/dashboard/links.ts` and unit-tested under
`tests/features/dashboard/links.test.ts`:

- `liveConnectionsPath(area)` → `/admin/live-connections`
- `liveConnectionsPath(area, serverId)` → `/admin/live-connections?serverId=<uuid>`
- `videoStreamsPath(area)` → `/admin/video-streams`
- `videoStreamsPath(area, 'Stopped')` → `/admin/video-streams?status=Stopped`

The **capital S** is load-bearing: the video streams page resolves `status` with
`enumParam` against `['Stopped', 'Running']`, so a lower-case value would be dropped and
the link would arrive unfiltered. The server card's link is the dashboard's own way into
the connection log's `serverId` filter — the log page draws that filter as a removable tag
labelled with the server's name (short id when the server list no longer has the row), and
has no server picker of its own.

### The `features/dashboard` file map

| Path | Holds |
| --- | --- |
| `types/ServerStatus.ts` | the feature's own `'Active' \| 'Disabled'` copy; features do not import features, so this is not imported from `features/servers` |
| `types/DashboardKpis.ts` | the six installation totals |
| `types/DashboardServer.ts` | one server row and its telemetry |
| `types/DashboardSnapshot.ts` | `{ kpis, servers, serversUnavailable, generatedAt }` |
| `types/index.ts` | type-only re-exports — the feature's only barrel |
| `api.ts` | `getDashboard({ signal })` → `apiGet('/admin/dashboard')` |
| `hooks/queries.ts` | `useDashboard()` with key `['dashboard']` and the signal passed through |
| `format.ts` | `formatUptime`, `formatMbps`, `clampPercent`, `RING_WARN_PERCENT`, `RING_CRITICAL_PERCENT`, `ringTier` |
| `links.ts` | `liveConnectionsPath`, `videoStreamsPath` |
| `components/DashboardServerCard.tsx` | one server card; default export; props `{ server, area }`; its own `useTranslation` |

`format.ts` and `links.ts` are pure modules at the feature root beside `api.ts`, exactly as
the other features' rule modules are. Their unit tests live under
`tests/features/dashboard/`, because a static render lands before any value arrives and
those branches — a boundary, a dropped unit, a decimal, a hostile number — are reachable
only as functions.
