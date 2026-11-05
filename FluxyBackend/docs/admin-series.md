> Part of the FluxyBackend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).

## The series API: `admin/series`

A series is its own aggregate: a catalogue row with a body of metadata, plus the episodes filed under it. It is not a third kind of stream and does not reuse the on-demand table: a series has many episodes, each with a season and a number, while an on-demand item has one source. `ContentType.Series` already existed for the category side, so only the rows, services and endpoints are new.

| Endpoint | Auth | Answer |
| --- | --- | --- |
| `GET /admin/series` | `Permission.ViewSeries` | page: `page`, `pageSize`, `search`, `categoryId`, `status`, `sortBy`, `sortOrder` |
| `GET /admin/series/{id}` | View | one series with `seasonsCount` and `episodesCount` |
| `POST /admin/series` | CSRF + `Permission.EditSeries` | `201 series_created` + relative `Location` |
| `PATCH /admin/series/{id}` | CSRF + Edit | `200 series_updated` |
| `DELETE /admin/series/{id}` | CSRF + Edit | `200 series_deleted`, episodes cascaded |
| `POST /admin/series/bulk` | CSRF + Edit | `200 series_bulk_completed`, one entry per id |
| `GET /admin/series/{id}/episodes` | View | page: `page`, `pageSize`, `search`, `seasonNumber`, `status`, `type`, `sortBy`, `sortOrder` |
| `GET /admin/series/{id}/episodes/{episodeId}` | View | one episode in full |
| `POST /admin/series/{id}/episodes` | CSRF + Edit | `201 series_episode_created` |
| `PATCH /admin/series/{id}/episodes/{episodeId}` | CSRF + Edit | `200 series_episode_updated` |
| `DELETE /admin/series/{id}/episodes/{episodeId}` | CSRF + Edit | `200 series_episode_deleted` |
| `POST /admin/series/{id}/episodes/bulk` | CSRF + Edit | `200 series_episodes_bulk_completed` |
| `POST /admin/series/{id}/seasons` | CSRF + Edit | `201 series_season_created` with `addedCount` |
| `GET /admin/series/transcode-formats` | View | `{ available: false, formats }` |
| `GET`/`POST /admin/series/{id}/episodes/{episodeId}/transcodes` | View / Edit | availability / `503 transcode_not_configured` |

- **`SeasonsCount` is `MAX(season_number)`, not a stored column and not the number of distinct seasons.** `EpisodesCount` is `COUNT(*)`. Both are projected in the page and the detail query, so deleting the last episode of the highest season lowers the count without a second write.
- **A season is not a table.** It is the set of episodes carrying one `season_number`. The add-season form reads the top level of a folder, keeps the video extensions in `SeriesLimits.EpisodeFileExtensions`, orders the files naturally (`Episode 2` before `Episode 10`) and creates numbers `1..N` in one `AddRange` and one `SaveChangesAsync`. An existing season is refused with `409 series_season_already_exists`; adding to it is the single-episode form's job. A relative folder is resolved against the content root, like a stored on-demand file.
- **The generated name is the series name plus the selected tags**: `<Series> S01E02`, `<Series> S01`, `<Series> E02`, or `<Series>` when neither checkbox is on. It is cut to 200 characters by shortening the base, never the tag.
- **A name is unique within its category** (`IX_series_category_id_name`); a season-and-number pair is unique inside a series (`IX_series_episodes_series_id_season_number_episode_number`). A violation from either exact index is the feature's `already_exists`; any other unique violation is rethrown.
- **Bulk operations are the single-row writes in a loop**: series `change-status`, `move-category`, `delete`; episodes `change-status`, `change-type`, `delete`. Partial success is the shape, the same as every other bulk endpoint here.
- **The transcode surface reuses `VodTranscodeFormat` and `VodTranscodeAction`.** The five containers are one list for both resources, and `SeriesTextParser.ParseFormatOrNull` delegates to `VodTextParser`. No encoder is configured, so availability is `false` and a request is a `503` that writes nothing.
- **The permissions are `ViewSeries = 65536` and `EditSeries = 131072`.** The startup seeder grants them to the base Administrators group; an existing custom administrator group has to be granted them by hand. The series count was added to `ContentCategoryRepository.CountContentAsync`, so deleting a category with a series is `409 content_category_in_use`.
- **`VideoStream.SeriesId` stays an opaque `uuid`.** Existing stream rows may name a series that was never recorded, and the delivery process resolves the reference rather than a foreign key.

Layering follows the usual split: `SeriesItem` / `Episode` in `Fluxy.Core/Series/Models/`, the read models, commands, outcomes, enums and limits in their folders, `ISeriesService` / `ISeriesBulkService` / `IEpisodeService` / `IEpisodeBulkService` / `ISeasonService` / `IEpisodeTranscodeService` and the two repositories in `Interfaces/` and `Repositories/`; the services, `SeriesRules`, `EpisodeRules` and `EpisodeFolderScanner` in `Fluxy.Application/Series/`; the entities, configurations and repositories in `Fluxy.DataAccess` (migration `AddSeries`); the contracts in `Fluxy.API/Contracts/Series/`; and the six controllers in `Fluxy.API/Controllers/Content/Series*.cs`.
