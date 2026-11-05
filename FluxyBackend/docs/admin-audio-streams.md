> Part of the FluxyBackend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).

## The audio streams API: `admin/audio-streams`

The second medium's streams, built as a **separate resource** rather than a third kind of the video one. The route is `admin/audio-streams`, the tables are `audio_streams` and `audio_stream_sources`, and the permissions are `ViewAudioStreams` / `EditAudioStreams` — deliberately **not** the video pair. A group that may read the video streams may not read the audio ones, and the table's `editAudioStreams` gate is the only thing standing between "may watch" and "may change".

| Endpoint | Auth | Answer |
| --- | --- | --- |
| `GET /admin/audio-streams` | `Permission.ViewAudioStreams` | page: `page`, `pageSize`, `search`, `categoryId`, `status`, `type`, `sortBy`, `sortOrder` |
| `GET /admin/audio-streams/{id}` | `Permission.ViewAudioStreams` | one stream with its `sources`; `404 audio_stream_not_found` |
| `POST /admin/audio-streams` | CSRF + `Permission.EditAudioStreams` | `201 audio_stream_created` + relative `Location` |
| `PATCH /admin/audio-streams/{id}` | CSRF + `Permission.EditAudioStreams` | `200 audio_stream_updated` |
| `POST /admin/audio-streams/{id}/start` / `…/stop` / `…/restart` | CSRF + `Permission.EditAudioStreams` | `audio_stream_started` / `audio_stream_stopped` / `audio_stream_restarted` |
| `POST /admin/audio-streams/bulk` | CSRF + `Permission.EditAudioStreams` | `200 audio_streams_bulk_completed`, one entry per identifier |
| `POST /admin/audio-streams/import` | CSRF + `Permission.EditAudioStreams` | `200 audio_streams_import_completed`, counts and per-line problems |
| `DELETE /admin/audio-streams/{id}` | CSRF + `Permission.EditAudioStreams` | `200 audio_stream_deleted`, `404` on a second |

### Two kinds, and the payload they share

`AudioStreamType` is **exactly two members**: `OnDemand = 1` and `Redirect = 2`. Both are **URL kinds** — both relay from the same ordered `sources` list, so the type decides *how* the stream runs and never *what* it carries. That single fact makes the audio resource simpler than the video one, and every difference below follows from it:

- **There is no kind whose payload another kind can switch off.** So there is no delay, no archive, no VOD id, no series id and no EPG pair — and therefore **no bulk warning vocabulary** either. A video bulk run may answer `video_stream_delay_cleared` or `video_stream_archive_cleared` beside its refusals; an audio bulk answer carries **no warnings at all**, and the frontend's `useBulkAction` is given only a `partialTitleKey`.
- **A bulk type change may choose either kind** and clears nothing. The video menu had to leave three kinds out because their payload could not be invented; the audio menu offers both.
- **The source list is replaced wholesale, in the order sent**, exactly as the video one is — absent keeps, empty clears, a list replaces. An empty list is **refused** for a URL kind ("add at least one source address"), because a stream with no source could never play.
- **`LogoUrl`, `Enigma2Sid` and all four `Restream*` fields are kept**, and both kinds carry them, so nothing on the form is ever greyed out for a kind that cannot hold it.

`AudioStreamStatus` is `Stopped = 0` / `Running = 1`, and like `UserStatus` it is **not ordered** — a check is for the exact member, never a comparison.

### What else is the same, and what is not

- **A name is unique within its category**, compared exactly and case sensitively like username and email: `UNIQUE(category_id, name)`, index `IX_audio_streams_category_id_name`. The lost race is caught and mapped to `409 audio_stream_already_exists` naming `name` — and, as with the video streams, **only that exact constraint name** counts. A violation of `IX_audio_stream_sources_stream_id_position` is a bug in the list writer and is rethrown, never dressed up as a taken name.
- **The update loads the sources collection** before replacing it (`.Include(entry => entry.StreamSources)`), for the reason the video `UpdateAsync` had to be fixed for: an unloaded `ICollection` navigation reads as **empty**, so the comparison always differed, nothing was removed and the fresh rows collided with the ones still in the table.
- **The category must exist and must be `ContentType.AudioStream`.** A category of another kind is refused — the audio streams are filed under audio categories, and the same table holds video categories.
- **`activeSource` is read-only.** No service writes it; the relaying process reports it.
- **The list is paged** (`total` counted before paging, the same "the pager must not lie" rule), `search` is a case-insensitive substring of the name, `pageSize` defaults to 20 and is refused above **1000**, and the search string is refused above **200**. The sort fields are `name`, `category`, `status` and `type`, parsed from plain text case-insensitively.
- **There is no `sort_order` column.** A stream has no position of its own — the order a stream is shown in belongs to the list that shows it, and a playlist's order is the order the relay plays in, not a display order.
- **No captcha and no attempt window** — the recorded admin-area departure: every write is behind an admin session, the `EditAudioStreams` permission and a fresh antiforgery pair, and `RejectsCsrfAsync()` runs before any body is read.

### The bulk menu: seven operations, not nine

`BulkAudioStreamAction` is `Start`, `Stop`, `Restart`, `Delete`, `MoveCategory`, `ChangeType` and `ChangeRestream`. The video endpoint's five extra operations (`change-delay`, `change-archive`, and the file-list and series/vod edits) have no audio counterpart, because an audio stream has no such field to write. The restream operation replaces all four settings as a group — a blank value clears its setting — the same whole-list rule the single-row form follows, in the same request shape.

The body is validated **before any row is read**: the action is matched case-insensitively and with hyphens, the identifiers must be 1..1000 uuids and duplicates collapse, and the value an action needs must be present and usable. A body that breaks any of those answers `400 validation_failed` naming every broken field at once. `RejectsCsrfAsync()` runs **before** the body is parsed, so a stale pair is `csrf_invalid` rather than a validation answer.

A well-formed run answers `200 audio_streams_bulk_completed` with `succeeded`, `failed` and one `items` entry per identifier; each entry carries the very code the single-row endpoint would answer, `ok` read off the status that code maps to, and `errors` keyed by camelCase field name. A category move that collides with a name already used there refuses **that row alone** with `audio_stream_already_exists`; the others still move. Partial success is the shape.

### The playlist import

`POST /admin/audio-streams/import` is a multipart form with the file in `playlist` and the settings beside it: `type`, `status`, `categoryId`, the four `restream*` fields, `appendSourceToExisting` and `overwriteExistingSettings`. **The parser is shared with the video import** (`M3uPlaylistParser`) and the entry limit is passed to it explicitly (`AudioStreamImportLimits.MaxEntries` = 5000) rather than relying on its default; the file limit is 10 MiB.

- **The kind offers only `OnDemand` and `Redirect`** — both relay from a list of addresses, which is exactly what a playlist supplies, so there is no kind the service would refuse.
- **Status starts at `Stopped`**, the endpoint's own default and the safe state for a batch of thousands.
- **There is no delay and no archive** on the form, for the same reason the kind menu is short.
- **The batch is partial success by design**: `imported + skipped + failed == total`, and every entry that did not land is in `problems` with its line, name, code and field errors. A name already used in the category or repeated within the file is **skipped** (`audio_stream_already_exists`); an unusable title, source or parser line is **failed** (`validation_failed`). Existing streams are never updated unless the operator opts in.
- **One save writes the batch.** The service stages every row through `AddRangeAsync` and calls `TrySaveAsync` once. A name another request took between the check and the save is `409 audio_stream_import_conflict` and **nothing** from the batch is written; the check matches `IX_audio_streams_category_id_name` by name, so any other unique violation is rethrown.

### The two permissions, and what they do not migrate

`UserPermission` grew `ViewAudioStreams = 4096` and `EditAudioStreams = 8192` — the two free values above the stream pair at 1024 and 2048. **Adding a permission needs no migration**: the catalog is code, and the base groups are reconciled with it at startup. The Administrators row gains the two grants on the next boot; **an existing custom administrator group has to be granted them by hand**, because the seeder only reconciles the three base groups. The values `1` and `2` remain retired and unused.

`ContentCategoryRepository.CountStreamsAsync` now counts **both** media: the question the caller is asking is "may this category go?", and a category holding an audio stream is as much in use as one holding a video stream.

### Layering

`AudioStream` in `Fluxy.Core/Streams/Models/`, `AudioStreamListItem` / `AudioStreamPage` in `ReadModels/`, `NewAudioStream` / `AudioStreamPatch` / `AudioStreamWrite` / `BulkAudioStreamOperation` / `NewAudioStreamImport` / `AudioStreamBatchUpdate` in `Commands/`, `AudioStreamType` / `AudioStreamStatus` / `AudioStreamAction` / `AudioStreamSortField` / `AudioStreamSortOrder` / `BulkAudioStreamAction` / `AudioStreamImportStatus` in `Enums/`, `AudioStreamOutcome` / `BulkAudioStreamItemResult` / `BulkAudioStreamOutcome` / `AudioStreamImportOutcome` / `AudioStreamImportProblem` in `Outcomes/`, the four limits classes in `Utils/`, and `IAudioStreamService` / `IAudioStreamBulkService` / `IAudioStreamImportService` / `IAudioStreamRepository` in `Interfaces/` and `Repositories/`. `AudioStreamService`, `AudioStreamBulkService` and `AudioStreamImportService` live in `Fluxy.Application/Streams/Services/` beside the video ones, the two entities and their configurations in `Fluxy.DataAccess/` (migration `20261023172343_AddAudioStreams`), the contracts in `Fluxy.API/Contracts/Streams/`, and the three controllers in `Fluxy.API/Controllers/Content/`.

**`AudioStreamOutcome` lives in `Outcomes/`, not in `Enums/`** — the one place the two media disagree about where a thing sits. The video outcome is an enum in `Enums/`; the audio one is a record carrying the action, the row identifier and the field errors, because the audio service answers a refusal with the field names the form draws beside the inputs. Follow the folder the type actually belongs to rather than the video one's precedent.

**`AudioStreamTextParser`** lives in `Fluxy.API/Controllers/Support/` beside `VideoStreamTextParser`: the transport parses the form fields it can parse and refuses a value that names no member, while whether the category is an audio category and whether each line can become a stream are rules of the service.

Verified: `dotnet build` clean, **210** unit tests and **116** integration tests green, and `dotnet ef migrations has-pending-model-changes` reports no changes.
