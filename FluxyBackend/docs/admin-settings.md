> Part of the FluxyBackend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).

## Installation settings: `admin/settings`, `admin/mag-settings` and `auth/public-settings`

Two one-row resources that hold what the installation is, rather than what an operator manages:
the panel's own settings and the MAG portal's. A third, anonymous read exists for the one value a
guest form needs before anybody has signed in.

| Endpoint | Auth | Answer |
| --- | --- | --- |
| `GET /admin/settings` | `viewSettings` | every stored application value; `recaptchaConfigured` in place of the key |
| `PATCH /admin/settings` | CSRF + `editSettings` | full write; `200 settings_updated`, `400 validation_failed` |
| `GET /admin/mag-settings` | `viewSettings` | every stored MAG value, both enums in the owner's words |
| `PATCH /admin/mag-settings` | CSRF + `editSettings` | full write; `200 mag_settings_updated`, `400 validation_failed` |
| `GET /auth/public-settings` | **anonymous** | `{ "recaptchaSiteKey": "" }` and nothing else |

The admin classes carry `[Authorize(Policy = AdminPolicy)]` and each action adds its own
permission, the shape every admin controller here follows. Every read carries
`Cache-Control: no-store`; every write checks the antiforgery pair **before** it reads the body,
which is why the request contracts carry no data annotations. The guest read carries `no-store`
too, because an operator can change the key and a shared cache holding the old answer would hand a
stale key to every visitor behind it.

**There is no `POST`, `PUT`, `DELETE` or `{id}` path on either collection.** Each table holds the
installation's one row, created by the migration and kept by the seeder, with no identifier a
client chooses and no lifecycle. The collection answers `405 method_not_allowed` to every method
it does not own through the shared status-page envelope, and a `{id}` path - a placeholder an
operator might paste by habit - is answered by a fence action that reads neither the identifier nor
the body, so it answers `405` rather than the generic `404`.

### `application_settings`

| Column | Type | Default |
| --- | --- | --- |
| `id` | `uuid` PK | `00000000-0000-0000-0000-000000000501` |
| `service_name` | `varchar(100)` | `Fluxy Streaming panel` |
| `default_timezone` | `varchar(64)` | `Europe/Berlin` |
| `enigma2_bouquet_name` | `varchar(100)` | `Fluxy IPTV` |
| `recaptcha_site_key` | `varchar(255)` | `''` |
| `recaptcha_secret_key` | `varchar(255)` | `''` |
| `enable_trials` | `boolean` | `true` |
| `public_api_flood_limit` | `integer` | `20` (`0` = unlimited) |
| `public_api_flood_whitelist` | `text[]` | `{}` |
| `require_user_agent` | `boolean` | `false` |
| `retention_user_logs` … `retention_mag_events` | `integer` ×6 | `30` |
| `backup_enabled` | `boolean` | `false` |
| `backup_period` | `smallint` | `1` (`Daily`) |
| `backup_rotation_count` | `integer` | `10` (`0` = infinite) |
| `fallback_stream_down_url`, `fallback_user_banned_url`, `fallback_user_expired_url`, `fallback_max_connections_url` | `varchar(2048)` ×4 | `''` |
| `created_at`, `updated_at` | `timestamptz` | context-stamped |

All columns are non-nullable and declare **no database default** (`HasDefaultValue` is forbidden
project-wide). The values above live in `ApplicationSettingsDefaults` and are written explicitly by
the migration and the seeder.

### `mag_settings`

| Column | Type | Default |
| --- | --- | --- |
| `id` | `uuid` PK | `00000000-0000-0000-0000-000000000502` |
| `show_all_categories`, `strict_security`, `show_channel_logos`, `show_preview_logos` | `boolean` ×4 | `true` |
| `default_container` | `smallint` | `1` (`MpegTs`) |
| `default_theme` | `smallint` | `0` (`Default`) |
| `recording_max_length_minutes` | `integer` | `180` |
| `max_local_recordings` | `integer` | `10` |
| `playback_limit` | `integer` | `3` |
| `allowed_stb_types` | `text[]` | all 31 catalogue entries, in catalogue order |
| `allowed_recording_stb_types` | `text[]` | `{MAG255,MAG256,MAG257}` |

Both lists are `text[]` columns for the reason the server country codes are: they are read with the
row and never queried by position, so a child table would buy a join for an order the array keeps.

### The wire contract

`GET /admin/settings` answers camelCase JSON:

```json
{
  "serviceName": "Fluxy Streaming panel",
  "defaultTimeZone": "Europe/Berlin",
  "enigma2BouquetName": "Fluxy IPTV",
  "recaptchaSiteKey": "",
  "recaptchaConfigured": false,
  "enableTrials": true,
  "publicApiFloodLimit": 20,
  "publicApiFloodWhitelist": [],
  "requireUserAgent": false,
  "retentionUserLogs": 30,
  "retentionCreditLogs": 30,
  "retentionConnectionLogs": 30,
  "retentionApiRequestFailures": 30,
  "retentionStreamProblems": 30,
  "retentionMagEvents": 30,
  "backupEnabled": false,
  "backupPeriod": "daily",
  "backupRotationCount": 10,
  "fallbackStreamDownUrl": "",
  "fallbackUserBannedUrl": "",
  "fallbackUserExpiredUrl": "",
  "fallbackMaxConnectionsUrl": ""
}
```

**The private reCAPTCHA key is never answered.** `recaptchaConfigured` is `stored secret is
non-empty`; a response that carried the key would put the installation's reCAPTCHA protection into
every proxy log and browser cache that held the settings page. The key is carried inside the
server-side snapshot, which no client can reach.

`PATCH` is a full write. Every field above is required except `recaptchaSecretKey`:
**absent or empty-after-trim keeps the stored secret; a non-empty value of at most 255 characters
replaces it.** That is the only asymmetry, and it exists because the form cannot send back a value
the response never gave it. A success answers
`{ "code": "settings_updated", "message": "The application settings have been updated." }`; the
snapshot is refreshed after the single save and before the response is built, so the process that
served the write reads its own change at once.

`GET /admin/mag-settings` answers `defaultContainer` as the canonical `mpeg-ts` or `hls` and
`defaultTheme` as the owner's own spelling. `PATCH` accepts the same vocabulary:
`mpeg-ts`, `mpegts`, `hls`; and `default`, `digital`, `emerald`, `cappucino`, `ocean blue`
(case-insensitive, trimmed). **`cappucino` is deliberately not corrected to `cappuccino`** - it is
the owner's spelling. Success is `{ "code": "mag_settings_updated", "message": "The MAG settings
have been updated." }`.

The STB catalogue is exactly 31 entries, in this order:
`Aura HD, Aura HD2, Aura HD3, Aura HD4, Aura HD5, Aura HD6, Aura HD7, Aura HD8, Aura HD9, MAG200,
MAG245, MAG245D, MAG250, MAG254, MAG255, MAG256, MAG257, MAG260, MAG270, MAG275, MAG322, MAG323,
MAG324, MAG325, MAG349, MAG350, MAG351, MAG352, MAG420, MAG520, WR320`. Both lists must name at
least one entry, are matched case-insensitively, are stored in the canonical spelling and are
de-duplicated in first-occurrence order. A name outside the catalogue refuses the whole list rather
than being dropped: a client that sent a name believes it asked for it.

### The validation keys

Errors are collected in the service keyed by the C# property name and normalised by the existing
`FieldErrorKeys`, so the wire always reads camelCase. The application settings keys are exactly:
`serviceName`, `defaultTimeZone`, `enigma2BouquetName`, `recaptchaSiteKey`, `recaptchaSecretKey`,
`enableTrials` (never refused), `publicApiFloodLimit`, `publicApiFloodWhitelist`,
`requireUserAgent` (never refused), `retentionUserLogs`, `retentionCreditLogs`,
`retentionConnectionLogs`, `retentionApiRequestFailures`, `retentionStreamProblems`,
`retentionMagEvents`, `backupEnabled` (never refused), `backupPeriod`, `backupRotationCount`,
`fallbackStreamDownUrl`, `fallbackUserBannedUrl`, `fallbackUserExpiredUrl`,
`fallbackMaxConnectionsUrl`. The MAG keys are:
`showAllCategories`, `strictSecurity`, `showChannelLogos`, `showPreviewLogos`,
`defaultContainer`, `defaultTheme`, `recordingMaxLengthMinutes`, `maxLocalRecordings`,
`playbackLimit`, `allowedStbTypes`, `allowedRecordingStbTypes`.

| Field | Rule |
| --- | --- |
| `serviceName`, `enigma2BouquetName` | trimmed, 1..100, non-empty |
| `defaultTimeZone` | trimmed, 1..64, must resolve through `TimeZonePolicy` (the profile's own rule, reused) |
| `recaptchaSiteKey`, `recaptchaSecretKey` | trimmed, ≤ 255, empty allowed |
| `publicApiFloodLimit` | 0..10000; `0` means unlimited. Not the rate-limit extension's `> 0` rule. |
| `publicApiFloodWhitelist` | each entry through `LoginGuardPolicy.NormalizeIpEntry`, deduped, at most 100 entries |
| six retentions | 1..3650 |
| `backupRotationCount` | 0..1000; `0` keeps every backup |
| four fallback URLs | trimmed, empty allowed, otherwise an absolute `http`/`https` URI ≤ 2048 |
| `defaultContainer` | `mpeg-ts`, `mpegts`, `hls` → canonical `mpeg-ts` / `hls` |
| `defaultTheme` | `default`, `digital`, `emerald`, `cappucino`, `ocean blue` → owner's spellings |
| `recordingMaxLengthMinutes` | 1..1440 |
| `maxLocalRecordings` | 0..1000; `0` allows none |
| `playbackLimit` | 1..`SubscriptionLimits.ConnectionsMax` (10000), the subscription's own ceiling |

### What is enforced in this pass, and what is not

**Exactly three settings have a consumer today:**

1. **reCAPTCHA.** `GoogleRecaptchaValidator` reads the private key from the snapshot. An empty
   secret answers `RecaptchaValidationOutcome.Skipped`; a configured one runs the check exactly as
   before. `CaptchaGuard` and the four guest controllers are untouched. `RecaptchaOptions` keeps
   `VerifyUrl` and `MinimumScore`; its `SecretKey` is read once, at startup, to seed the row.
2. **Trials.** `SubscriptionService.CreateAsync` refuses a create whose *effective* `IsTrial` is
   true while `enableTrials` is false, under `errors["IsTrial"] → isTrial`. Because the check reads
   the effective row, a trial tariff plan is covered without a second rule. `UpdateAsync` is
   deliberately not gated - the owner scoped enforcement to creation.
3. **Service name.** The four email subjects interpolate
   `Your {serviceName} registration code` / `... confirmation code` / `... password reset code`
   (`RegistrationService`, the two `ProfileService` sends, `PasswordResetService`). Bodies stay
   hardcoded.

**Stored, validated and exposed only - not enforced in this pass:**

- `enigma2BouquetName`, the four fallback URLs - no playlist/MAG portal producer exists;
- `publicApiFloodLimit`, `publicApiFloodWhitelist`, `requireUserAgent` - there is no public
  streaming API to throttle or inspect;
- all six retention values, `backupEnabled`/`backupPeriod`/`backupRotationCount` - there is no
  `IHostedService`/`BackgroundService` anywhere in the solution and no `pg_dump` at runtime;
  backup execution is out of scope entirely;
- `defaultTimeZone` - no screen formats an installation-wide date with it yet;
- **all eleven MAG settings** - the MAG flags overlap `TariffPlan.StrictStbDeviceId` and
  `Subscription.IsStbLock`, and there is no MAG portal to read them.

### Seeding: the migration, the seeder and the bootstrap copy

- The migration `AddApplicationSettings` creates both tables and inserts the one row in each,
  guarded by `WHERE NOT EXISTS`, exactly as `AddTranscodingProfiles` seeds its profile. A re-run, or
  a database where the startup seeder wrote the row first, cannot duplicate it.
- `SettingsSeeder` runs at startup after `ServerSeeder`. It creates either row that is absent, in
  one save, and **never overwrites a row that exists**. It also primes the snapshot, so the first
  guest request after a boot already sees the stored settings.
- **The configured reCAPTCHA secret is copied into the row on its first creation only.** Before
  this screen existed, `RECAPTCHA_SECRET_KEY` was the only place the key could live; the one-time
  copy keeps such an installation working. Afterwards **the database is the only source** - editing
  `.env` changes nothing, because an operator who cleared the key in the panel must not have it
  silently restored by a restart.
- `ApplicationSettingsDefaults` / `MagSettingsDefaults` in Core are **mandatory**, not a
  convenience: the integration fixture truncates the database and re-seeds only base groups + the
  main server, so the snapshot provider answers these statics whenever a repository returns null.

### The snapshot

`ISettingsSnapshotProvider` (Core) hands out two immutable records, `ApplicationSettingsSnapshot`
and `MagSettingsSnapshot`, which mirror the contracts **and include `RecaptchaSecretKey`
server-side**. `SettingsSnapshotProvider` (Application) is a singleton:

- it never holds a `DbContext` - each read opens its own scope through `IServiceScopeFactory`;
- it is guarded by `SemaphoreSlim(1,1)`, so concurrent readers share one refresh;
- a reading older than `SettingsLimits.SnapshotTtl` (30 s, measured with `TimeProvider`) is renewed
  on the next access;
- a repository failure serves the previous reading and logs a warning - settings are needed by
  flows that must keep working, and a database hiccup must not disable reCAPTCHA or start refusing
  trials;
- a null row answers the defaults statics;
- both arrays are copied for each reader, so a caller cannot edit the cached instance.

`SomeService.UpdateAsync` calls `ReloadAsync` after its single `SaveChangesAsync`, so the process
that served a write reads it back immediately.

### The permission pair

`ViewSettings = 1L << 34` (`17179869184`) and `EditSettings = 1L << 35` (`34359738368`), both owned
by `Admin`, appended to `UserPermissionCatalog.All` and `Owner` immediately after `EditServers`.
Bits 0 and 1 remain retired and unused. Policies `ViewSettingsPolicy` / `EditSettingsPolicy` live in
`AuthenticationExtensions` and carry `RequireAuthenticatedUser()` + `RoleRequirement(Admin)` +
`PermissionRequirement(...)`, a copy of the servers pair.

`GrantSettingsPermissions` is data only, mirroring `GrantTariffPlanPermissions`, and backfills the
pair for an installation that migrated without restarting. Its insert is guarded by
`WHERE NOT EXISTS`.

> **Deviation from the written specification:** the specification named the backfill row
> identifiers `...221` and `...222`, but `AddBlacklistEntries` already inserts its permission rows
> with exactly those primary keys, and a primary key is installation-wide. Reusing them failed every
> fresh migration with `23505 duplicate key value violates unique constraint
> "PK_user_group_permissions"`. The pair therefore uses `...231` and `...232`; the permission values
> (`17179869184`, `34359738368`) and the base group (`...003`) are exactly the specified ones.

### Codes → statuses

| Code | Status | Meaning |
| --- | --- | --- |
| `settings_updated` | 200 | the application row was written |
| `mag_settings_updated` | 200 | the MAG row was written |
| `validation_failed` | 400 | one or more fields, keyed by their camelCase names |
| `csrf_invalid` | 400 | the antiforgery pair did not validate |
| `auth_required` | 401 | no session |
| `permission_denied` | 403 | the group holds neither the read grant nor the write one |
| `method_not_allowed` | 405 | a method the resource does not own, or any `{id}` path |

### Layering

`ApplicationSettings`, `MagSettings` (`Fluxy.Core/Settings/Models/`); the three enums
(`BackupPeriod`, `MagContainer`, `MagTheme`); `ApplicationSettingsDefaults`, `MagSettingsDefaults`,
`SettingsLimits`, `StbTypeCatalog`, `MagThemeCatalog`, `MagContainerCatalog`; the two snapshots
(`ReadModels/`); `ApplicationSettingsPatch`, `MagSettingsPatch` (`Commands/`); `SettingsAction`,
`SettingsOutcome`, `MagSettingsAction`, `MagSettingsOutcome` (`Outcomes/`); `ISettingsService`,
`IMagSettingsService`, `ISettingsSnapshotProvider`, `ISettingsSeeder` (`Interfaces/`);
`IApplicationSettingsRepository`, `IMagSettingsRepository` (`Repositories/`).
`SettingsRules`, `MagSettingsRules` and the four services in `Fluxy.Application/Settings/`. The
entities, configurations, repositories and the two migrations in `Fluxy.DataAccess`. The contracts
in `Fluxy.API/Contracts/Settings/`, the controllers in `Controllers/Admin/` and
`Controllers/Auth/`.

### Verified by running

`dotnet build FluxyBackend.slnx` clean; the unit suite green (including `StbTypeCatalogTests`,
`MagThemeCatalogTests`, `MagContainerCatalogTests`, `SettingsRulesTests`, `MagSettingsRulesTests`,
`SettingsSnapshotProviderTests`, and the extended permission-catalog assertions);
`dotnet test --project tests\Fluxy.IntegrationTests` green (`Settings/SettingsTests.cs`: the
anonymous `401`s, the guest read, every default, the write round trip and the immediate
`/auth/public-settings` answer, the write-only secret, the exact validation keys, the MAG defaults
and canonicalisation, the two grants, CSRF, both `405` fences, and the trials switch on both
creation paths); and `dotnet ef migrations has-pending-model-changes` reports no changes.
