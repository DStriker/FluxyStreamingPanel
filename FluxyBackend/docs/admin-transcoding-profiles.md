> Part of the FluxyBackend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).

## Transcoding profiles: `admin/transcoding-profiles`

A transcoding profile is a **name plus a set of optional ffmpeg options**. The encoder that
would spend them does not exist yet in this pass: what exists is the operator's side - the
table, the form, the picker the four content forms draw, and the reference every one of those
four rows may carry.

| Endpoint | Auth | Answer |
| --- | --- | --- |
| `GET /admin/transcoding-profiles` | `viewTranscodingProfiles` | `page`, `pageSize`, `search`, `sortBy`, `sortOrder` → `{ items, total, page, pageSize }` |
| `GET /admin/transcoding-profiles/options` | **any** of `viewStreams`, `viewAudioStreams`, `viewVideoOnDemand`, `viewSeries` | `[{ id, name }]`, ordered by name |
| `GET /admin/transcoding-profiles/{id}` | `viewTranscodingProfiles` | one profile; `404 transcoding_profile_not_found` |
| `POST /admin/transcoding-profiles` | CSRF + `editTranscodingProfiles` | `201 transcoding_profile_created` + relative `Location`; `409 transcoding_profile_already_exists` |
| `PATCH /admin/transcoding-profiles/{id}` | CSRF + `editTranscodingProfiles` | `200 transcoding_profile_updated`; `404`, `409 already_exists`, `400 validation_failed` |
| `DELETE /admin/transcoding-profiles/{id}` | CSRF + `editTranscodingProfiles` | `200 transcoding_profile_deleted`; `404 transcoding_profile_not_found`, `409 transcoding_profile_in_use` |

The class carries `[Authorize(Policy = AdminPolicy)]`; each action adds its own permission on top,
the shape every admin controller here follows.

### The field table

Every option is nullable, and **null means "do not pass this flag to ffmpeg"** - a real state
rather than a missing value.

| Field | Type | Rule |
| --- | --- | --- |
| `name` | string | 1..100, trimmed, unique for the installation, compared **case sensitively**. Index `IX_transcoding_profiles_name`. |
| `videoCodec` | enum (`VideoCodec`, 34 values) | default `copy` when a create names none |
| `audioCodec` | enum (`AudioCodec`, 27 values) | default `copy` |
| `preset` | enum (`TranscodingPreset`, 10 values) | default `default` |
| `videoProfile` | enum (`VideoProfile`, 8 values) | null when not set |
| `videoBitrateAvg`, `audioBitrateAvg`, `minBitrateTolerance`, `maxBitrateTolerance`, `bufferSize` | int kbps | null or ≥ 0 |
| `crf` | int | null or 0..51 |
| `width`, `height` | int | null or ≥ −1; `-1` keeps the aspect ratio |
| `aspectRatio` | string | null or ≤ 32 characters |
| `framerate` | decimal | null or > 0; stored `numeric(9,3)` |
| `audioSampleRate` | int Hz | null or > 0 |
| `audioChannels` | int | null or 1..8 |
| `threads` | int | null or ≥ 0; `0` means automatic |
| `logoUrl` | string | null or ≤ 2048 characters |
| `redactionX/Y/Width/Height` | int | **all four or none**; edges ≥ 0, width/height ≥ 1 |

**The wire names are ffmpeg's own lowercase names**, not the C# member names: `libx264`,
`libx265`, `libvpx-vp9`, `msmpeg4v2`, `pcm_alaw`, `pcm_mulaw`, plus `veryslow` and `high4.0`.
`Enum.TryParse` cannot spell them, which is why
`Controllers/Support/TranscodingProfileTextParser.cs` owns one table per enum and the response
is written through the same table. The frontend's `src/features/transcoding-profiles/ffmpegOptions.ts`
mirrors them, and `tests/features/transcoding-profiles/ffmpegOptions.test.ts` pins the sets.

### The create defaults

A create applies the three values that mean "pass no flag" when the body names none:
`videoCodec = copy`, `audioCodec = copy`, `preset = default`, `videoProfile = null`. Everything
else stays null. The defaults are applied by the service - there is **no database default**, per
the project rule.

### The query contract

The page defaults to `page=1`, `pageSize=20`, `sortBy=name`, `sortOrder=asc`. The bounds are
**refused rather than clamped** (`pageSize` ≤ 1000, `search` ≤ 200 characters), and a sort field
that names nothing is `400 validation_failed` under `sortBy`. The name is the **only** sortable
column: every other field is an optional ffmpeg option whose order a reader does not compare
down a table.

Search is a case-insensitive substring of the name, applied before the count, so `total` is the
size of the filtered list.

### The seed

Migration `AddTranscodingProfiles` creates the table **and** inserts one profile:

| Name | videoCodec | audioCodec | preset | everything else |
| --- | --- | --- | --- | --- |
| `Standard H264 AAC` | `libx264` | `aac` | `default` | null |

The row has a constant identifier (`00000000-0000-0000-0000-000000000301`) and the insert is
guarded by `WHERE NOT EXISTS`, so re-running the statement leaves one row. It is **not a startup
seeder**: the row is written once by the migration and from then on it is an ordinary profile -
renameable, changeable and deletable like any other.

### The four references and the in-use refusal

`video_streams`, `audio_streams`, `vod_items` and `series` each carry a nullable
`transcoding_profile_id` with a real foreign key (`ON DELETE RESTRICT`). The list rows do **not**
carry it; the detail responses of the four resources do.

- A create names it as `transcodingProfileId`, or omits it for none. An identifier that names no
  row is `400 validation_failed` under that field rather than a database failure.
- A **PATCH is three-state**, the convention `SeriesId`/`EpgSourceId` already follow: JSON `null`
  keeps the stored value, `""` clears it, a uuid string replaces it.
- `DELETE` of a referenced profile answers `409 transcoding_profile_in_use` and **the row
  survives**. The service counts the four tables first
  (`ITranscodingProfileRepository.CountReferencesAsync`, the shape
  `ContentCategoryRepository.CountContentAsync` uses); the foreign key is the guarantee behind it.

### The permission pair, and the options exception

`ViewTranscodingProfiles = 1L << 30` and `EditTranscodingProfiles = 1L << 31`, both owned by
`Admin`, registered in `UserPermissionCatalog.All` and `Owner`. **This pair is why
`UserPermission` widened from `int` to `long` and `user_group_permissions.permission` from
`integer` to `bigint`** (`WidenUserGroupPermissionToBigint`, lossless for every stored grant).
See [admin-user-groups.md](admin-user-groups.md) and the reversal note in
[tickets.md](tickets.md).

**The options endpoint is gated by an "any of" policy over the four content-view grants**, not by
the pair. Choosing a profile while editing a stream is part of editing the stream, so an operator
who may run the streams gets the names to choose from and no table to browse. The full list,
detail and every write stay behind the pair. The policy is
`AuthenticationExtensions.ViewTranscodingProfileOptionsPolicy`, built on
`PermissionAnyRequirement`.

### Layering

`TranscodingProfile` in `Fluxy.Core/Transcoding/Models/`; `NewTranscodingProfile`,
`TranscodingProfileWrite` and `TranscodingProfilePatch` in `Commands/`; the enums
(`VideoCodec`, `AudioCodec`, `TranscodingPreset`, `VideoProfile`, `TranscodingProfileAction`,
the sort pair), read models (`TranscodingProfileView`, `TranscodingProfilePage`,
`TranscodingProfileOption`), `TranscodingProfileOutcome`, the two limit classes,
`TranscodingProfileReference` (the three-state merge the four content services share),
`ITranscodingProfileService` and `ITranscodingProfileRepository` in their own folders.
`TranscodingProfileService` and `TranscodingProfileRules` in `Fluxy.Application/Transcoding/`.
The entity, configuration, repository and two migrations in `Fluxy.DataAccess`. The contracts in
`Fluxy.API/Contracts/Transcoding/`, the parser in `Controllers/Support/`, and
`TranscodingProfilesController` in `Controllers/Admin/` - `admin/transcoding-profiles` is an
admin-area route that belongs to no content section.

### Verified by running

`dotnet build` clean, 1032 unit tests, 372 integration tests green (11 of them this resource:
create→detail→patch keep/clear→delete→404, the guarded seed, name uniqueness, per-field 400s,
search/sort/page, CSRF and the two grants, the options endpoint's four-grant gate, in-use refusal
per referencing table, and the set/replace/clear round trip on all four resources), and
`dotnet ef migrations has-pending-model-changes` reports no changes.
