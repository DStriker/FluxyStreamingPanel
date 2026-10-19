using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Content;
using Fluxy.Core.Models.Streams;
using Fluxy.DataAccess.Configurations;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Fluxy.Application.Services.Streams
{
    /// <summary>
    /// Reads the configured streams and changes them on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One rule decides most of what happens here, and it lives in this class rather than at
    /// the call sites so that no second endpoint reaching the same action can forget it:
    /// <b>the type owns the fields only it can use</b>. A permanent, on-demand or redirect
    /// stream carries a list of URLs; a series stream carries the identifier of its series; a
    /// video-playlist stream carries the identifier of one on-demand item; a file-playlist
    /// stream carries a list of paths. Whatever the request also sent in a field the chosen
    /// type does not use is not stored - it is cleared, not refused, because a form whose
    /// operator has just changed the type is still carrying the previous type's values and
    /// would otherwise answer with a refusal for a request nobody meant to make. The other
    /// half of the same rule: the field the type <i>does</i> use is required, and a stream
    /// whose own payload is missing is refused.
    /// </para>
    /// <para>
    /// Two further rules sit beside it. A name is unique <i>within its category</i>, the same
    /// way a category name is unique within its kind of content - two categories are two
    /// sections of a channel list, and "News" under general and "News" under sports are two
    /// channels on two different screens. And a position is never a field anybody types: it is
    /// the highest position of the category plus one on creation, and the end of the category
    /// again when the stream is moved into another one.
    /// </para>
    /// <para>
    /// Nothing here knows about HTTP. Which <see cref="VideoStreamAction"/> becomes a 201 and
    /// which becomes a 409 is the transport layer's business.
    /// </para>
    /// </remarks>
    public sealed class VideoStreamService : IVideoStreamService
    {
        /// <summary>The sentence a name that is blank or too long gets.</summary>
        private static readonly string[] NameRule =
        [
            "The name must be between 1 and "
                + VideoStreamConfiguration.NameMaxLength
                + " characters."
        ];

        /// <summary>The sentence a type that names no member of the enum gets.</summary>
        private static readonly string[] TypeRule =
        [
            "The stream type must be one of: permanent, ondemand, redirect, " +
                "onlineseries, vodplaylist, fileplaylist."
        ];

        /// <summary>The sentence a category that names no row of the right kind gets.</summary>
        private static readonly string[] CategoryRule =
        [
            "The category must be an existing category of video streams."
        ];

        /// <summary>The sentence a delay that is not a whole number of minutes gets.</summary>
        private static readonly string[] DelayRule = ["The delay must be zero or more minutes."];

        /// <summary>The sentence an archive length that is not a whole number of days gets.</summary>
        private static readonly string[] ArchiveDaysRule = ["The archive length must be zero or more days."];

        /// <summary>The sentence an archive that only a permanent stream may keep gets.</summary>
        private static readonly string[] PermanentArchiveRule =
        [
            "Only a permanent stream may keep an archive. Set the archive length to zero, " +
                "or make the stream permanent."
        ];

        /// <summary>The sentence a URL list that holds nothing usable gets.</summary>
        private static readonly string[] SourcesRule =
        [
            "Add at least one source address, and no more than "
                + VideoStreamConfiguration.SourceUrlMaxLength
                + " characters each."
        ];

        /// <summary>The sentence a file list that holds nothing usable gets.</summary>
        private static readonly string[] FilesRule =
        [
            "Add at least one file, and no more than "
                + VideoStreamConfiguration.FilePathMaxLength
                + " characters each."
        ];

        /// <summary>The sentence a series stream with no series gets.</summary>
        private static readonly string[] SeriesRule =
        [
            "A series stream needs the identifier of the series it plays."
        ];

        /// <summary>The sentence a video-playlist stream with no item gets.</summary>
        private static readonly string[] VodRule =
        [
            "A video playlist stream needs the identifier of the item it plays."
        ];

        /// <summary>The sentence an identifier that is not a uuid gets.</summary>
        private static readonly string[] IdentifierRule =
        ["That value must be the uuid of the item it names."];

        /// <summary>The sentence a half-configured programme guide gets.</summary>
        private static readonly string[] EpgRule =
        [
            "A programme guide needs both a source and a channel key. Clear the other one " +
                "if the stream carries no guide."
        ];

        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoStreamService"/> class.
        /// </summary>
        /// <param name="context">Context holding the streams and their two lists.</param>
        public VideoStreamService(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<VideoStreamPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? categoryId,
            VideoStreamStatus? status,
            VideoStreamType? type,
            VideoStreamSortField sortBy,
            VideoStreamSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            var query = _context.VideoStreams.AsNoTracking();

            if (categoryId is { } wantedCategory)
            {
                query = query.Where(entry => entry.CategoryId == wantedCategory);
            }

            if (status is { } wantedStatus)
            {
                query = query.Where(entry => entry.Status == wantedStatus);
            }

            if (type is { } wantedType)
            {
                query = query.Where(entry => entry.Type == wantedType);
            }

            // Folded before the comparison rather than with a collation, the same way the
            // accounts list searches its usernames and the visit history its user agents:
            // `Contains` on the lowered value is a substring match any name answers to
            // regardless of how the column was typed.
            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                query = query.Where(entry => entry.Name.ToLower().Contains(needle));
            }

            // Counted *before* the page is taken and with the same filter, because the pager
            // shows this number: a total counted over every row would advertise pages that the
            // filter has already removed.
            var total = await query.CountAsync(cancellationToken);

            query = Order(query, sortBy, sortOrder);

            var rows = await query
                .Select(entry => new StoredStream
                {
                    Id = entry.Id,
                    Name = entry.Name,
                    Status = entry.Status,
                    Type = entry.Type,
                    CategoryId = entry.CategoryId,

                    // The join on the category is inner - its key is NOT NULL and RESTRICT -
                    // so no row of this page can arrive without one. The navigation is
                    // declared nullable because a query that did not include it genuinely has
                    // nothing there; this projection includes it, and says so.
                    CategoryName = entry.Category!.Name,
                    LogoUrl = entry.LogoUrl,
                    ActiveSource = entry.ActiveSource,
                    EpgSourceId = entry.EpgSourceId,
                    EpgChannelKey = entry.EpgChannelKey,
                    SortOrder = entry.SortOrder
                })
                // Widening to long before the cast: a page number comes off a query string,
                // and `(page - 1) * pageSize` on a huge one overflows int into a negative
                // OFFSET, which the database answers as an error rather than as an empty page.
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new VideoStreamPage
            {
                Items = rows.Select(ToListItem).ToList(),
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public async Task<VideoStream?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var stream = await _context.VideoStreams
                .AsNoTracking()
                .Include(entry => entry.Category)
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            if (stream is null)
            {
                return null;
            }

            // The two lists are rows of their own, so they are two more reads - one per page
            // worth of streams would be two more per row, which is why the table endpoint does
            // not ask for them at all. Ordered by position rather than by identifier, because
            // the position is the order the relay plays in and the identifier is a random uuid.
            var sources = await _context.VideoStreamSources
                .AsNoTracking()
                .Where(entry => entry.StreamId == id)
                .OrderBy(entry => entry.Position)
                .Select(entry => entry.Url)
                .ToListAsync(cancellationToken);

            var files = await _context.VideoStreamFiles
                .AsNoTracking()
                .Where(entry => entry.StreamId == id)
                .OrderBy(entry => entry.Position)
                .Select(entry => entry.Path)
                .ToListAsync(cancellationToken);

            return stream.ToModel(sources, files);
        }

        /// <inheritdoc />
        public async Task<VideoStreamOutcome> CreateAsync(
            NewVideoStream input,
            CancellationToken cancellationToken = default)
        {
            var errors = ValidateShape(
                input.Name,
                input.Type,
                input.CategoryId,
                input.DelayMinutes,
                input.ArchiveDays,
                input.SeriesId,
                input.VodId,
                input.EpgSourceId,
                input.EpgChannelKey,
                input.Sources,
                input.Files);

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            // The category is checked after the shape rather than before it, because a body
            // that already broke a rule of its own has nothing to say about the database yet -
            // and a category that is not a category of video streams is refused rather than
            // accepted with a stream filed where no stream is ever shown.
            if (await CategoryIsUsableAsync(input.CategoryId, cancellationToken) is false)
            {
                return Invalid(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [nameof(NewVideoStream.CategoryId)] = CategoryRule
                });
            }

            if (await NameTakenAsync(input.Name.Trim(), input.CategoryId, null, cancellationToken))
            {
                return AlreadyExists(nameof(NewVideoStream.Name));
            }

            // The end of the category's list is the highest position that category holds, plus
            // one - read here rather than claimed by the caller, because the caller's screen
            // can be one move behind the database and the end is a fact about the rows, not
            // about what anybody saw. An empty category reads null and starts at 1.
            var highest = await _context.VideoStreams
                .Where(entry => entry.CategoryId == input.CategoryId)
                .MaxAsync(entry => (int?)entry.SortOrder, cancellationToken);

            var entity = new VideoStreamEntity
            {
                Name = input.Name.Trim(),
                Status = VideoStreamStatus.Stopped,
                Type = input.Type,
                CategoryId = input.CategoryId,
                LogoUrl = NullIfBlank(input.LogoUrl),
                // Nothing in the interface writes it, so an insert never has one. The relaying
                // process reports it, and only it.
                ActiveSource = null,
                Enigma2Sid = NullIfBlank(input.Enigma2Sid),
                DelayMinutes = input.DelayMinutes,
                ArchiveDays = input.Type is VideoStreamType.Permanent ? input.ArchiveDays : 0,
                SortOrder = (highest ?? 0) + 1,
                RestreamUserAgent = NullIfBlank(input.RestreamUserAgent),
                RestreamProxy = NullIfBlank(input.RestreamProxy),
                RestreamCookies = NullIfBlank(input.RestreamCookies),
                RestreamHeaders = NullIfBlank(input.RestreamHeaders)
            };

            // The pair is stored only when it agrees with itself, which the validation above
            // already required - so this is the same decision it took, written down once.
            entity.SeriesId = input.Type is VideoStreamType.OnlineSeries ? input.SeriesId : null;
            entity.VodId = input.Type is VideoStreamType.VodPlaylist ? input.VodId : null;

            if (input.EpgSourceId is { } epgSource && input.EpgChannelKey is { } epgKey)
            {
                entity.EpgSourceId = epgSource;
                entity.EpgChannelKey = epgKey.Trim();
            }

            AddSources(entity, input.Type, input.Sources);
            AddFiles(entity, input.Type, input.Files);

            _context.VideoStreams.Add(entity);

            if (await SaveAsync(cancellationToken) is { } createFailure)
            {
                // A name another request took between the check above and this write. The
                // answer is the same one the check would have given - but only when the name
                // index is what was refused; any other unique index is a bug in this service,
                // and answering "that name is taken" for one would hide it behind a sentence
                // about something else entirely.
                return createFailure.IsConstraint(NameIndexName)
                    ? AlreadyExists(nameof(NewVideoStream.Name))
                    : throw createFailure;
            }

            return new VideoStreamOutcome
            {
                Action = VideoStreamAction.Created,
                StreamId = entity.Id
            };
        }

        /// <inheritdoc />
        public async Task<VideoStreamOutcome> UpdateAsync(
            Guid id,
            VideoStreamPatch patch,
            CancellationToken cancellationToken = default)
        {
            // The two lists are loaded with the row, and that is load-bearing rather than an
            // optimisation: `ReplaceSources`/`ReplaceFiles` decide what to delete by comparing
            // against what the row already holds, and an unloaded collection reads as *empty* -
            // so the comparison always differed, `RemoveRange` removed nothing, and the fresh
            // rows were inserted over the ones still in the table, colliding on
            // `IX_video_stream_sources_stream_id_position`. Every change to a stream that had
            // a source or file list failed that way, including one that touched nothing else.
            var stream = await _context.VideoStreams
                .Include(entry => entry.StreamSources)
                .Include(entry => entry.StreamFiles)
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            if (stream is null)
            {
                return NotFound(id);
            }

            // Merged first and judged afterwards, so the rules below see the row as it is
            // about to become rather than as it was. Every "absent means leave it alone" of
            // the patch is resolved here, in one place.
            var name = patch.Name is null ? stream.Name : patch.Name.Trim();
            var type = patch.Type ?? stream.Type;
            var categoryId = patch.CategoryId ?? stream.CategoryId;
            var delayMinutes = patch.DelayMinutes ?? stream.DelayMinutes;
            var logoUrl = MergeText(patch.LogoUrl, stream.LogoUrl);
            var enigma2Sid = MergeText(patch.Enigma2Sid, stream.Enigma2Sid);
            var epgChannelKey = MergeText(patch.EpgChannelKey, stream.EpgChannelKey);
            var restreamUserAgent = MergeText(patch.RestreamUserAgent, stream.RestreamUserAgent);
            var restreamProxy = MergeText(patch.RestreamProxy, stream.RestreamProxy);
            var restreamCookies = MergeText(patch.RestreamCookies, stream.RestreamCookies);
            var restreamHeaders = MergeText(patch.RestreamHeaders, stream.RestreamHeaders);

            // The three references travel as text so that "clear" and "keep" are two different
            // answers; this is where the text stops being text.
            if (!TryMergeIdentifier(patch.SeriesId, stream.SeriesId, out var seriesId, out var seriesError))
            {
                return Invalid(Field(nameof(VideoStreamPatch.SeriesId), seriesError ?? IdentifierRule), id);
            }

            if (!TryMergeIdentifier(patch.VodId, stream.VodId, out var vodId, out var vodError))
            {
                return Invalid(Field(nameof(VideoStreamPatch.VodId), vodError ?? IdentifierRule), id);
            }

            if (!TryMergeIdentifier(patch.EpgSourceId, stream.EpgSourceId, out var epgSourceId, out var epgError))
            {
                return Invalid(Field(nameof(VideoStreamPatch.EpgSourceId), epgError ?? IdentifierRule), id);
            }

            // The lists take the shape the caller sent: absent keeps, empty clears, anything
            // else replaces wholesale. Replacing is what makes the order on the operator's
            // screen the order the relay plays in.
            var sources = patch.Sources is null
                ? await ReadSourcesAsync(id, cancellationToken)
                : patch.Sources;

            var files = patch.Files is null
                ? await ReadFilesAsync(id, cancellationToken)
                : patch.Files;

            var errors = ValidateShape(
                name,
                type,
                categoryId,
                delayMinutes,
                patch.ArchiveDays ?? stream.ArchiveDays,
                seriesId,
                vodId,
                epgSourceId,
                epgChannelKey,
                sources,
                files);

            if (errors.Count > 0)
            {
                return Invalid(errors, id);
            }

            if (await CategoryIsUsableAsync(categoryId, cancellationToken) is false)
            {
                return Invalid(
                    Field(nameof(VideoStreamPatch.CategoryId), CategoryRule),
                    id);
            }

            if (await NameTakenAsync(name, categoryId, id, cancellationToken))
            {
                return AlreadyExists(nameof(VideoStreamPatch.Name), id);
            }

            // Moving to another category moves the stream to the end of that category's list.
            // The old category keeps its own gaps, the same way a deletion does: renumbering
            // the survivors would be a second source for a fact a reorder already owns.
            if (categoryId != stream.CategoryId)
            {
                var highest = await _context.VideoStreams
                    .Where(entry => entry.CategoryId == categoryId && entry.Id != id)
                    .MaxAsync(entry => (int?)entry.SortOrder, cancellationToken);

                stream.SortOrder = (highest ?? 0) + 1;
            }

            stream.Name = name;
            stream.Type = type;
            stream.CategoryId = categoryId;
            stream.LogoUrl = logoUrl;
            stream.Enigma2Sid = enigma2Sid;
            stream.DelayMinutes = delayMinutes;
            stream.SeriesId = type is VideoStreamType.OnlineSeries ? seriesId : null;
            stream.VodId = type is VideoStreamType.VodPlaylist ? vodId : null;
            stream.ArchiveDays = type is VideoStreamType.Permanent
                ? (patch.ArchiveDays ?? stream.ArchiveDays)
                : 0;
            stream.RestreamUserAgent = restreamUserAgent;
            stream.RestreamProxy = restreamProxy;
            stream.RestreamCookies = restreamCookies;
            stream.RestreamHeaders = restreamHeaders;

            if (epgSourceId is { } source && epgChannelKey is { } key)
            {
                stream.EpgSourceId = source;
                stream.EpgChannelKey = key;
            }
            else
            {
                // Only one half of the pair was present, and the validation above refused
                // that - so this branch is unreachable while the rules hold. Cleared rather
                // than left half-written so that a future rule change cannot leave a row
                // carrying a key nothing will look up.
                stream.EpgSourceId = null;
                stream.EpgChannelKey = null;
            }

            ReplaceSources(stream, type, sources);
            ReplaceFiles(stream, type, files);

            if (await SaveAsync(cancellationToken) is { } updateFailure)
            {
                // Only the name index may be answered as a name conflict. The other two
                // unique indexes are on the two position columns, and a violation of either
                // is this service writing its own lists wrongly - reporting one as "that name
                // is taken" is how the bug below stayed hidden for as long as it did, and it
                // sent the search to the uniqueness rule instead of to the write.
                return updateFailure.IsConstraint(NameIndexName)
                    ? AlreadyExists(nameof(VideoStreamPatch.Name), id)
                    : throw updateFailure;
            }

            return new VideoStreamOutcome
            {
                Action = VideoStreamAction.Updated,
                StreamId = id
            };
        }

        /// <inheritdoc />
        public async Task<VideoStreamOutcome> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var stream = await _context.VideoStreams
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            if (stream is null)
            {
                return NotFound(id);
            }

            // The two lists go with it through the foreign keys' cascade, so this is one
            // removal rather than three - and deleting the rows rather than leaving them
            // behind is what keeps "the rows a stream has are its list" true after the stream
            // is gone.
            _context.VideoStreams.Remove(stream);

            await _context.SaveChangesCheckedAsync(cancellationToken);

            return new VideoStreamOutcome
            {
                Action = VideoStreamAction.Removed,
                StreamId = id
            };
        }

        /// <inheritdoc />
        public async Task<VideoStreamOutcome> StartAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => await SetStatusAsync(id, VideoStreamStatus.Running, VideoStreamAction.Started, cancellationToken);

        /// <inheritdoc />
        public async Task<VideoStreamOutcome> StopAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => await SetStatusAsync(id, VideoStreamStatus.Stopped, VideoStreamAction.Stopped, cancellationToken);

        /// <summary>Writes one status to one stream, which is all starting and stopping do.</summary>
        /// <remarks>
        /// Starting a stream that is already running is a success rather than a complaint: the
        /// row says what the operator asked it to say, and refusing would make a double click
        /// look like a failure of the thing the visitor was trying to achieve.
        ///
        /// <see cref="VideoStreamEntity.ActiveSource"/> is deliberately left alone. It is what
        /// the relaying process reports back, and clearing it here would be this side claiming
        /// the process answered when nothing has asked it anything.
        /// </remarks>
        private async Task<VideoStreamOutcome> SetStatusAsync(
            Guid id,
            VideoStreamStatus status,
            VideoStreamAction action,
            CancellationToken cancellationToken)
        {
            var stream = await _context.VideoStreams
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            if (stream is null)
            {
                return NotFound(id);
            }

            if (stream.Status != status)
            {
                stream.Status = status;
                await _context.SaveChangesCheckedAsync(cancellationToken);
            }

            return new VideoStreamOutcome { Action = action, StreamId = id };
        }

        /// <summary>
        /// Checks everything about a stream that can be judged from the request alone: the
        /// name, the type, the numbers, the half of a programme guide, and the payload the
        /// type owns.
        /// </summary>
        /// <remarks>
        /// The category is deliberately not checked here - whether that identifier names a row
        /// is a question about the database, and it is asked once the body has proved it is
        /// worth asking about.
        /// </remarks>
        private static Dictionary<string, string[]> ValidateShape(
            string name,
            VideoStreamType type,
            Guid categoryId,
            int delayMinutes,
            int archiveDays,
            Guid? seriesId,
            Guid? vodId,
            Guid? epgSourceId,
            string? epgChannelKey,
            IReadOnlyList<string> sources,
            IReadOnlyList<string> files)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var trimmedName = name.Trim();
            if (trimmedName.Length is < 1 or > VideoStreamConfiguration.NameMaxLength)
            {
                errors[nameof(NewVideoStream.Name)] = NameRule;
            }

            // A number in the column outside the members would be a kind every reader has to
            // guess about - refused rather than coerced.
            if (!Enum.IsDefined(type))
            {
                errors[nameof(NewVideoStream.Type)] = TypeRule;
            }

            if (categoryId == Guid.Empty)
            {
                errors[nameof(NewVideoStream.CategoryId)] = CategoryRule;
            }

            if (delayMinutes < 0)
            {
                errors[nameof(NewVideoStream.DelayMinutes)] = DelayRule;
            }

            if (archiveDays < 0)
            {
                errors[nameof(NewVideoStream.ArchiveDays)] = ArchiveDaysRule;
            }
            else if (archiveDays > 0 && type is not VideoStreamType.Permanent)
            {
                // Not cleared silently, unlike the fields the type does not own: an archive
                // is a number somebody chose, and storing zero instead of it would be the
                // server disagreeing with an answer it was given rather than ignoring one it
                // could not use.
                errors[nameof(NewVideoStream.ArchiveDays)] = PermanentArchiveRule;
            }

            // A guide is two facts, and a stream that names only one of them names neither -
            // so a half-configured pair is refused rather than stored as "no guide", which
            // would tell the operator the form had not been saved.
            if (epgSourceId is null ^ string.IsNullOrWhiteSpace(epgChannelKey))
            {
                errors[nameof(NewVideoStream.EpgSourceId)] = EpgRule;
                errors[nameof(NewVideoStream.EpgChannelKey)] = EpgRule;
            }

            switch (type)
            {
                case VideoStreamType.Permanent:
                case VideoStreamType.OnDemand:
                case VideoStreamType.Redirect:
                    if (!HasUsableEntries(sources, VideoStreamConfiguration.SourceUrlMaxLength))
                    {
                        errors[nameof(NewVideoStream.Sources)] = SourcesRule;
                    }

                    break;

                case VideoStreamType.OnlineSeries:
                    if (seriesId is null)
                    {
                        errors[nameof(NewVideoStream.SeriesId)] = SeriesRule;
                    }

                    break;

                case VideoStreamType.VodPlaylist:
                    if (vodId is null)
                    {
                        errors[nameof(NewVideoStream.VodId)] = VodRule;
                    }

                    break;

                case VideoStreamType.FilePlaylist:
                    if (!HasUsableEntries(files, VideoStreamConfiguration.FilePathMaxLength))
                    {
                        errors[nameof(NewVideoStream.Files)] = FilesRule;
                    }

                    break;
            }

            return errors;
        }

        /// <summary>
        /// Whether a list holds at least one entry that is not blank and none that is too long.
        /// </summary>
        /// <remarks>
        /// Both halves matter. A list of blanks is a list nobody filled in, and a single entry
        /// over the column's length is a row the database would refuse - so both are the
        /// caller's to hear about rather than the provider's exception to surface later.
        /// </remarks>
        private static bool HasUsableEntries(IReadOnlyList<string> entries, int maxLength)
            => entries.Count > 0
                && entries.All(entry => !string.IsNullOrWhiteSpace(entry) && entry.Trim().Length <= maxLength);

        /// <summary>Appends the rows of the list a create is writing, in position order.</summary>
        private static void AddSources(
            VideoStreamEntity entity,
            VideoStreamType type,
            IReadOnlyList<string> sources)
        {
            if (!IsUrlKind(type))
            {
                return;
            }

            var position = 0;
            foreach (var entry in sources)
            {
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                entity.StreamSources.Add(new VideoStreamSourceEntity
                {
                    StreamId = entity.Id,
                    Position = position++,
                    Url = entry.Trim()
                });
            }
        }

        /// <summary>Appends the rows of the file list a create is writing, in position order.</summary>
        private static void AddFiles(
            VideoStreamEntity entity,
            VideoStreamType type,
            IReadOnlyList<string> files)
        {
            if (type is not VideoStreamType.FilePlaylist)
            {
                return;
            }

            var position = 0;
            foreach (var entry in files)
            {
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                entity.StreamFiles.Add(new VideoStreamFileEntity
                {
                    StreamId = entity.Id,
                    Position = position++,
                    Path = entry.Trim()
                });
            }
        }

        /// <summary>Replaces the stored source list with the one the change is sending.</summary>
        private void ReplaceSources(
            VideoStreamEntity stream,
            VideoStreamType type,
            IReadOnlyList<string> sources)
        {
            var wanted = (IsUrlKind(type) ? sources : Array.Empty<string>())
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Select(entry => entry.Trim())
                .ToList();

            var stored = stream.StreamSources
                .OrderBy(entry => entry.Position)
                .ToList();

            if (stored.Select(entry => entry.Url).SequenceEqual(wanted, StringComparer.Ordinal))
            {
                // Nothing moved. Leaving the rows alone keeps their identifiers, which are the
                // only thing about them that outlives a save.
                return;
            }

            _context.VideoStreamSources.RemoveRange(stored);

            for (var index = 0; index < wanted.Count; index++)
            {
                stream.StreamSources.Add(new VideoStreamSourceEntity
                {
                    StreamId = stream.Id,
                    Position = index,
                    Url = wanted[index]
                });
            }
        }

        /// <summary>Replaces the stored file list with the one the change is sending.</summary>
        private void ReplaceFiles(
            VideoStreamEntity stream,
            VideoStreamType type,
            IReadOnlyList<string> files)
        {
            var wanted = (type is VideoStreamType.FilePlaylist ? files : Array.Empty<string>())
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Select(entry => entry.Trim())
                .ToList();

            var stored = stream.StreamFiles
                .OrderBy(entry => entry.Position)
                .ToList();

            if (stored.Select(entry => entry.Path).SequenceEqual(wanted, StringComparer.Ordinal))
            {
                return;
            }

            _context.VideoStreamFiles.RemoveRange(stored);

            for (var index = 0; index < wanted.Count; index++)
            {
                stream.StreamFiles.Add(new VideoStreamFileEntity
                {
                    StreamId = stream.Id,
                    Position = index,
                    Path = wanted[index]
                });
            }
        }

        /// <summary>Reads the stored source list, in the order the relay uses it.</summary>
        private async Task<IReadOnlyList<string>> ReadSourcesAsync(
            Guid id,
            CancellationToken cancellationToken)
            => await _context.VideoStreamSources
                .AsNoTracking()
                .Where(entry => entry.StreamId == id)
                .OrderBy(entry => entry.Position)
                .Select(entry => entry.Url)
                .ToListAsync(cancellationToken);

        /// <summary>Reads the stored file list, in the order it is played.</summary>
        private async Task<IReadOnlyList<string>> ReadFilesAsync(
            Guid id,
            CancellationToken cancellationToken)
            => await _context.VideoStreamFiles
                .AsNoTracking()
                .Where(entry => entry.StreamId == id)
                .OrderBy(entry => entry.Position)
                .Select(entry => entry.Path)
                .ToListAsync(cancellationToken);

        /// <summary>
        /// Whether that identifier names a category this domain shows - an existing category
        /// of video streams. A category of another kind would file the stream where no
        /// channel list ever looks.
        /// </summary>
        private async Task<bool> CategoryIsUsableAsync(Guid categoryId, CancellationToken cancellationToken)
            => await _context.ContentCategories
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Id == categoryId && entry.Type == ContentType.VideoStream,
                    cancellationToken);

        /// <summary>
        /// Whether some other stream of the same category already answers to that name.
        /// </summary>
        /// <remarks>
        /// Compared exactly, case sensitively, for the same reason username and email are - and
        /// scoped to the category, because two categories are two screens. Excluding
        /// <paramref name="exceptStream"/> is what lets a rename keep its own name.
        /// </remarks>
        private async Task<bool> NameTakenAsync(
            string name,
            Guid categoryId,
            Guid? exceptStream,
            CancellationToken cancellationToken)
        {
            return await _context.VideoStreams
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Name == name
                        && entry.CategoryId == categoryId
                        && (exceptStream == null || entry.Id != exceptStream),
                    cancellationToken);
        }

        /// <summary>Puts the page in the order the caller asked for.</summary>
        /// <remarks>
        /// Every branch carries a tie-break, and it is the stream's name: two rows share the
        /// sorted value more often than not (a whole category may be one status), and without a
        /// tie-break the database is free to return them in any order it likes between one page
        /// and the next - which is how the same stream appears on two pages or on none.
        ///
        /// The identifier sorts last rather than first: it is a random uuid, so it breaks ties
        /// exactly, but an order on it is an order nobody asked for.
        /// </remarks>
        private static IQueryable<VideoStreamEntity> Order(
            IQueryable<VideoStreamEntity> query,
            VideoStreamSortField field,
            VideoStreamSortOrder order)
        {
            var descending = order is VideoStreamSortOrder.Descending;

            return field switch
            {
                VideoStreamSortField.Category => Chain(
                    query, entry => entry.Category!.Name, entry => entry.Name, descending),

                VideoStreamSortField.Status => Chain(
                    query, entry => entry.Status, entry => entry.Name, descending),

                VideoStreamSortField.Type => Chain(
                    query, entry => entry.Type, entry => entry.Name, descending),

                VideoStreamSortField.SortOrder => Chain(
                    query, entry => entry.SortOrder, entry => entry.Name, descending),

                _ => Chain(query, entry => entry.Name, entry => entry.Id, descending)
            };
        }

        /// <summary>Orders by the first key and breaks the tie with the second.</summary>
        private static IOrderedQueryable<VideoStreamEntity> Chain<TKey1, TKey2>(
            IQueryable<VideoStreamEntity> source,
            Expression<Func<VideoStreamEntity, TKey1>> primary,
            Expression<Func<VideoStreamEntity, TKey2>> tieBreak,
            bool descending)
        {
            var ordered = descending
                ? source.OrderByDescending(primary)
                : source.OrderBy(primary);

            return descending
                ? ordered.ThenByDescending(tieBreak)
                : ordered.ThenBy(tieBreak);
        }

        /// <summary>Writes the staged changes, interpreting the one failure that is expected.</summary>
        /// <returns>Null when it was written, or the exception when a unique index refused it.</returns>
        /// <remarks>
        /// Any unique violation is returned rather than thrown here, and the callers sort them:
        /// only <see cref="NameIndexName"/> becomes a name conflict, everything else is
        /// rethrown so a bug in the list writer cannot wear the sentence about a taken name.
        /// </remarks>
        private async Task<UniqueViolationException?> SaveAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _context.SaveChangesCheckedAsync(cancellationToken);
                return null;
            }
            catch (UniqueViolationException exception)
            {
                return exception;
            }
        }

        /// <summary>The unique index making a stream's name unique within its category.</summary>
        /// <remarks>
        /// The callers compare against this name to tell the one expected refusal from the two
        /// unique indexes on the source and file position columns: a genuine race for a name is
        /// answered <c>409</c>, while a violation of a position index is a bug in the list
        /// writer and is left to surface as one.
        /// </remarks>
        private const string NameIndexName = "IX_video_streams_category_id_name";

        /// <summary>The three kinds that relay from a list of URLs rather than from anything else.</summary>
        private static bool IsUrlKind(VideoStreamType type)
            => type is VideoStreamType.Permanent
                or VideoStreamType.OnDemand
                or VideoStreamType.Redirect;

        /// <summary>Turns an empty string into null, so a blank field is stored as "none".</summary>
        private static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        /// <summary>
        /// Resolves one nullable text field of a change: absent keeps, empty clears, anything
        /// else is trimmed and stored.
        /// </summary>
        private static string? MergeText(string? sent, string? stored)
        {
            if (sent is null)
            {
                return stored;
            }

            return string.IsNullOrWhiteSpace(sent) ? null : sent.Trim();
        }

        /// <summary>
        /// Resolves one nullable identifier of a change, where the text form answers "keep"
        /// and "clear" without a second field to tell them apart.
        /// </summary>
        /// <param name="sent">What the caller sent: null keeps, empty clears, a uuid sets.</param>
        /// <param name="stored">What the row holds.</param>
        /// <param name="value">The resolved value.</param>
        /// <param name="error">The sentence to refuse with, when the text is not a uuid.</param>
        /// <returns>False when the text names no identifier at all.</returns>
        private static bool TryMergeIdentifier(
            string? sent,
            Guid? stored,
            out Guid? value,
            out string[]? error)
        {
            error = null;

            if (sent is null)
            {
                value = stored;
                return true;
            }

            if (string.IsNullOrWhiteSpace(sent))
            {
                value = null;
                return true;
            }

            if (!Guid.TryParse(sent.Trim(), out var parsed))
            {
                value = null;
                error = IdentifierRule;
                return false;
            }

            value = parsed;
            return true;
        }

        /// <summary>One field's worth of rejections, keyed by the C# property name.</summary>
        private static Dictionary<string, string[]> Field(string name, string[] sentences)
            => new(StringComparer.Ordinal) { [name] = sentences };

        private static VideoStreamListItem ToListItem(StoredStream row) => new()
        {
            Id = row.Id,
            Name = row.Name,
            Status = row.Status,
            Type = row.Type,
            CategoryId = row.CategoryId,
            CategoryName = row.CategoryName,
            LogoUrl = row.LogoUrl,
            ActiveSource = row.ActiveSource,
            HasEpg = VideoStreamEpg.Configured(row.EpgSourceId, row.EpgChannelKey),
            SortOrder = row.SortOrder
        };

        private static VideoStreamOutcome NotFound(Guid id)
            => new() { Action = VideoStreamAction.NotFound, StreamId = id };

        private static VideoStreamOutcome AlreadyExists(string field, Guid id = default)
            => new()
            {
                Action = VideoStreamAction.AlreadyExists,
                StreamId = id,
                Errors = Field(field, ["Another stream of this category already has that name."])
            };

        private static VideoStreamOutcome Invalid(
            IReadOnlyDictionary<string, string[]> errors,
            Guid id = default)
            => new() { Action = VideoStreamAction.Invalid, StreamId = id, Errors = errors };

        /// <summary>
        /// The rows of one page, read before they are turned into the items the table draws.
        /// </summary>
        /// <remarks>
        /// A projection target of its own rather than <see cref="VideoStreamListItem"/> because
        /// the "has a programme guide" column is answered from two fields, and a query that
        /// selected the pair could not assign the answer to a property that has no setter.
        /// </remarks>
        private sealed class StoredStream
        {
            public Guid Id { get; init; }

            public string Name { get; init; } = string.Empty;

            public VideoStreamStatus Status { get; init; }

            public VideoStreamType Type { get; init; }

            public Guid CategoryId { get; init; }

            public string CategoryName { get; init; } = string.Empty;

            public string? LogoUrl { get; init; }

            public string? ActiveSource { get; init; }

            public Guid? EpgSourceId { get; init; }

            public string? EpgChannelKey { get; init; }

            public int SortOrder { get; init; }
        }
    }
}
