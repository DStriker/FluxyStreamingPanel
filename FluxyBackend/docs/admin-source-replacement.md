> Part of the FluxyBackend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).

## The mass source replacement: `admin/source-replacements`

One operator, two strings, every playback address in the installation. The screen exists for
the day a host moves or an authorization fragment changes: four tables hold addresses that point
at the old one, and editing them by hand is four screens and a mistake waiting to happen.

| Endpoint | Auth | Answer |
| --- | --- | --- |
| `POST /admin/source-replacements` | CSRF + all four edit gates | `200 source_replacement_completed` with per-domain counts and refusals |

### Scope: playback addresses only

The run writes exactly four columns and nothing else:

- `video_stream_sources.url`
- `audio_stream_sources.url`
- `vod_items.source`
- `series_episodes.source`

Deliberately **outside** the scope: `video_streams.logo_url`, `audio_streams.logo_url`, the
on-demand `poster_url`, `backdrop_url` and `subtitle_url`, the series poster/backdrop, the
episode `image_url`, `video_stream_files.path` and `video_streams.active_source` /
`audio_streams.active_source`. Swapping a host is a question about *where a medium is played
from*; a logo and a stored file path are different questions and a bulk edit of them is a
different feature nobody asked for.

### The four-grant fence

The endpoint carries four stacked `[Authorize]` policies - `EditStreams`,
`EditAudioStreams`, `EditVideoOnDemand`, `EditSeries` - and ASP.NET Core merges them into one
authorization evaluation where **every** requirement must succeed. That is the fence: a caller
who may edit video streams but not episodes would otherwise be handed a control that writes rows
their grants do not cover. No new `UserPermission` member, no catalog change and no migration:
the four grants already exist, and the base Administrators group holds all of them.

The refusal is `403 permission_denied`, not `403 auth_role_changed`, for a caller whose role is
correct and whose group is missing one grant. `ApiAuthorizationResultHandler` reads *every*
permission requirement the merged policy carries (not the first) and answers `permission_denied`
unless the group holds all of them - which is also the rule stacked attributes have always meant.

### The replacement rule

`value.Replace(find, replace, StringComparison.Ordinal)` — **ordinal, case sensitive, every
occurrence**. Nothing is trimmed on either side. `Host` and `host` are different strings, and an
operator replacing `token=abc` means the characters.

The repository narrows candidates with a plain SQL `Contains(find)`. That is a filter, not the
rule: a collation that widened it would only add work. The service re-applies the ordinal rule
to every candidate and counts a candidate it left unchanged as **skipped**.

### Validation

| Body | Answer |
| --- | --- |
| `find` null, empty or whitespace only | `400 validation_failed`, errors `find` |
| `find == replace` (ordinal) | `400 validation_failed`, errors `replace` |
| both of the above | both keys reported |
| `replace` absent | read as `""` (this is how an operator deletes a fragment) |

The antiforgery pair is checked **before the body is read**, so a request with no pair is
`400 csrf_invalid` rather than a validation answer. A refused request reads no table and writes
nothing.

### Refusals: per entity, all-or-nothing, partial success

A resulting address may be unusable even though the request was valid. A refusal is judged per
**entity**, not per address:

- any resulting address of an entity blank (or whitespace only) ⇒ the entity is refused with
  `source_replacement_blank`, and **none** of its values change;
- otherwise any resulting address longer than its column ⇒ `source_replacement_too_long`,
  same all-or-nothing rule;
- blank is checked first, so it wins when both apply.

The other entities are still written and the request still answers `200`: partial success is the
shape, and each refusal names the domain, the entity id, its display name and the code. Duplicate
resulting addresses are allowed - the replacement is a text edit, not a uniqueness rule, and the
schema has no constraint that would refuse it.

Limits are read from the tables that already own them, never redefined:
`VideoStreamLimits.SourceUrlMaxLength` (2000), `AudioStreamLimits.SourceUrlMaxLength` (2000),
`VodLimits.SourceMaxLength` (2048), `SeriesLimits.SourceMaxLength` (2048).

### Counting

- `updated` — entities whose values changed.
- `skipped` — candidate addresses on which the ordinal replacement produced the same text.
- no match at all — neither; a valid run that found nothing is `200` with `updated: 0`.

`domains` always carries **all four** entries, in the order `videoStreams`, `audioStreams`,
`videoOnDemand`, `seriesEpisodes`, whether or not a table had a candidate. The top-level
`updated`/`skipped` are the sums.

### In place, one save

The set intents in `SourceReplacementRepository` mutate existing tracked rows in place: a stream
source is keyed by `(stream_id, position)`, an item or an episode by its own identifier, and only
the address column changes. No row is deleted and re-inserted, no identifier or position moves,
`created_at` is untouched, and `ExecuteUpdate`/`ExecuteDelete`/raw SQL are not used. `ApplyAudit`
moves `updated_at` only on rows the tracker saw as `Modified`, so an untouched address in the
same chain keeps its original stamp.

The whole run commits with **exactly one** `IUnitOfWork.SaveChangesAsync` (through
`TrySaveAsync`), after every entity has been judged. A validation refusal returns before the
save; a `UniqueViolationException` here is a bug and is rethrown rather than dressed up.

### `active_source` stays stale, deliberately

`active_source` is the relaying process's report of which address is answering *right now*. It is
not a playback address the operator edits, so the replacement leaves it alone; until the relay
reports again it names the old host. That is accepted and documented rather than patched: writing
it from an admin form would make the application invent a fact only the relay knows.

### No index, sequential scan

There is no index on the source columns, and none is added: the operation is a rare
administrator action over a table the relay reads by `stream_id`, and an index on a `text` column
for one-off substring search would cost every insert to make one button faster. The SQL filter is a
sequential scan by design, and the note here is so the next reader does not "fix" it with a
migration.

### Shape of the code

`SourceDomain` / `SourceRefusalReason` in `Fluxy.Core/Content/Enums/`,
`ReplaceSourcesOperation` in `Commands/`, `SourceReplacementOutcome` /
`SourceReplacementDomainCount` / `SourceReplacementRefusal` in `Outcomes/`, the four
`Stored*SourceRow` projections in `Rows/`, `ISourceReplacementRepository` in `Repositories/` and
`ISourceReplacementService` in `Interfaces/`; the implementation
`Fluxy.Application/Content/Services/SourceReplacementService.cs`; the repository
`Fluxy.DataAccess/Repositories/SourceReplacementRepository.cs` (`internal sealed`, registered in
`AddDataAccess`); the contracts in `Fluxy.API/Contracts/Content/` and the controller
`Fluxy.API/Controllers/Content/SourceReplacementsController.cs`.

Core gains no project reference and no EF type, Application still cannot see DataAccess, and the
repository registers the same scoped lifetime as every other one because it stages through the
request's own `FluxyDbContext`.

### What the tests pin

`tests/Fluxy.IntegrationTests/Content/SourceReplacementTests.cs` covers the four-domain run and
the stable order, the ordinal case-sensitive all-occurrence rule, both input refusals and the
both-at-once body, the missing antiforgery pair - including that it is answered *before* the
body rules are judged - each of the four single-grant-missing refusals plus the all-four success,
the blank and over-long per-entity refusals with the rest of the run continuing, the unmatched
success, the identity/position/`created_at` preservation with `active_source` untouched and
`updated_at` moving only where the value changed, and the `405` on every other verb.

`tests/Fluxy.UnitTests/Content/SourceReplacementServiceTests.cs` pins the rule a database cannot
state: one entity that produces both a blank and an over-long address gets exactly one refusal,
`source_replacement_blank`, and stages nothing - which is what fails if the two checks are ever
reordered.
