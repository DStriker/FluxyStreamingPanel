using Fluxy.Core.Epg.Repositories;
using Fluxy.Core.Persistence.Exceptions;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Utils;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Interfaces;
using Fluxy.Core.Streams.Models;
using Fluxy.Core.Streams.ReadModels;
using Fluxy.Core.Streams.Repositories;
using Fluxy.Core.Streams.Utils;
using Fluxy.Application.Streams.Utils;

namespace Fluxy.Application.Streams.Services
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
    /// channels on two different screens.
    /// </para>
    /// <para>
    /// Nothing here knows about HTTP. Which <see cref="VideoStreamAction"/> becomes a 201 and
    /// which becomes a 409 is the transport layer's business.
    /// </para>
    /// </remarks>
    public sealed class VideoStreamService : IVideoStreamService
    {
        /// <summary>The sentence a file list that holds nothing usable gets.</summary>
        private static readonly string[] FilesRule =
        [
            "Add at least one file, and no more than "
                + VideoStreamLimits.FilePathMaxLength
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

        /// <summary>The sentence a guide source that names no existing source gets.</summary>
        private static readonly string[] EpgSourceRule =
        [
            "There is no programme guide source with that identifier."
        ];

        private readonly IVideoStreamRepository _streams;
        private readonly IEpgSourceRepository _epgSources;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoStreamService"/> class.
        /// </summary>
        /// <param name="streams">Repository holding the streams and their two lists.</param>
        /// <param name="epgSources">
        /// Repository that answers whether the guide source a stream names exists.
        /// </param>
        /// <param name="unitOfWork">Commits the changes one operation stages.</param>
        public VideoStreamService(
            IVideoStreamRepository streams,
            IEpgSourceRepository epgSources,
            IUnitOfWork unitOfWork)
        {
            _streams = streams;
            _epgSources = epgSources;
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public Task<VideoStreamPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? categoryId,
            VideoStreamStatus? status,
            VideoStreamType? type,
            VideoStreamSortField sortBy,
            VideoStreamSortOrder sortOrder,
            CancellationToken cancellationToken = default)
            => _streams.GetPageAsync(
                page,
                pageSize,
                search,
                categoryId,
                status,
                type,
                sortBy,
                sortOrder,
                cancellationToken);

        /// <inheritdoc />
        public Task<VideoStream?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => _streams.GetAsync(id, cancellationToken);

        /// <inheritdoc />
        public async Task<VideoStreamOutcome> CreateAsync(
            NewVideoStream input,
            CancellationToken cancellationToken = default)
        {
            var errors = ValidateShape(
                input.Name,
                input.Type,
                input.Status,
                input.CategoryId,
                input.DelayMinutes,
                input.ArchiveDays,
                input.SeriesId,
                input.VodId,
                input.EpgSourceId,
                input.EpgChannelKey,
                input.RestreamUserAgent,
                input.RestreamProxy,
                input.Sources,
                input.Files);

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            // A guide source is a row of this database, so a stream may only name one that
            // exists - the foreign key would refuse it anyway, and a refusal here names the
            // field instead of failing at the write.
            if (VideoStreamEpg.Configured(input.EpgSourceId, input.EpgChannelKey)
                && await _epgSources.ExistsAsync(input.EpgSourceId!.Value, cancellationToken) is false)
            {
                return Invalid(Field(nameof(NewVideoStream.EpgSourceId), EpgSourceRule));
            }

            // The category is checked after the shape rather than before it, because a body
            // that already broke a rule of its own has nothing to say about the database yet -
            // and a category that is not a category of video streams is refused rather than
            // accepted with a stream filed where no stream is ever shown.
            if (await _streams.CategoryIsUsableAsync(input.CategoryId, cancellationToken) is false)
            {
                return Invalid(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [nameof(NewVideoStream.CategoryId)] = VideoStreamRules.CategoryRule
                });
            }

            if (await _streams.IsNameTakenAsync(
                input.Name.Trim(),
                input.CategoryId,
                exceptStream: null,
                cancellationToken))
            {
                return AlreadyExists(nameof(NewVideoStream.Name));
            }

            var stream = BuildWrite(
                input.Name.Trim(),
                input.Type,
                input.Status,
                input.CategoryId,
                input.LogoUrl,
                input.Enigma2Sid,
                input.DelayMinutes,
                input.SeriesId,
                input.VodId,
                input.EpgSourceId,
                input.EpgChannelKey,
                input.ArchiveDays,
                input.RestreamUserAgent,
                input.RestreamProxy,
                input.RestreamCookies,
                input.RestreamHeaders,
                input.Sources,
                input.Files);

            var streamId = await _streams.AddAsync(stream, cancellationToken);

            if (await SaveAsync(cancellationToken) is { } createFailure)
            {
                // A name another request took between the check above and this write. The
                // answer is the same one the check would have given - but only when the name
                // index is what was refused; any other unique index is a bug in this service,
                // and answering "that name is taken" for one would hide it behind a sentence
                // about something else entirely.
                return createFailure.IsConstraint(VideoStreamRules.NameIndexName)
                    ? AlreadyExists(nameof(NewVideoStream.Name))
                    : throw createFailure;
            }

            return new VideoStreamOutcome
            {
                Action = VideoStreamAction.Created,
                StreamId = streamId
            };
        }

        /// <inheritdoc />
        public async Task<VideoStreamOutcome> UpdateAsync(
            Guid id,
            VideoStreamPatch patch,
            CancellationToken cancellationToken = default)
        {
            // The read brings both lists with the row, and that is load-bearing rather than an
            // optimisation: the replacement below decides what to delete by comparing against
            // what the row already holds, and an unloaded collection reads as *empty* - so the
            // comparison always differed and the fresh rows were inserted over the ones still
            // in the table.
            var stream = await _streams.FindForUpdateAsync(id, cancellationToken);

            if (stream is null)
            {
                return NotFound(id);
            }

            // Merged first and judged afterwards, so the rules below see the row as it is
            // about to become rather than as it was. Every "absent means leave it alone" of
            // the patch is resolved here, in one place.
            var name = patch.Name is null ? stream.Name : patch.Name.Trim();
            var type = patch.Type ?? stream.Type;
            var status = patch.Status ?? stream.Status;
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
            var sources = patch.Sources ?? stream.Sources;
            var files = patch.Files ?? stream.Files;
            var archiveDays = patch.ArchiveDays ?? stream.ArchiveDays;

            var errors = ValidateShape(
                name,
                type,
                status,
                categoryId,
                delayMinutes,
                archiveDays,
                seriesId,
                vodId,
                epgSourceId,
                epgChannelKey,
                restreamUserAgent,
                restreamProxy,
                sources,
                files);

            if (errors.Count > 0)
            {
                return Invalid(errors, id);
            }

            // The merged pair is checked against the source table for the reason the create is:
            // a guide whose source does not exist is a key nothing will ever look up.
            if (VideoStreamEpg.Configured(epgSourceId, epgChannelKey)
                && await _epgSources.ExistsAsync(epgSourceId!.Value, cancellationToken) is false)
            {
                return Invalid(
                    Field(nameof(VideoStreamPatch.EpgSourceId), EpgSourceRule),
                    id);
            }

            if (await _streams.CategoryIsUsableAsync(categoryId, cancellationToken) is false)
            {
                return Invalid(
                    Field(nameof(VideoStreamPatch.CategoryId), VideoStreamRules.CategoryRule),
                    id);
            }

            if (await _streams.IsNameTakenAsync(name, categoryId, id, cancellationToken))
            {
                return AlreadyExists(nameof(VideoStreamPatch.Name), id);
            }

            var write = BuildWrite(
                name,
                type,
                status,
                categoryId,
                logoUrl,
                enigma2Sid,
                delayMinutes,
                seriesId,
                vodId,
                epgSourceId,
                epgChannelKey,
                archiveDays,
                restreamUserAgent,
                restreamProxy,
                restreamCookies,
                restreamHeaders,
                sources,
                files);

            await _streams.UpdateAsync(id, write, cancellationToken);

            if (await SaveAsync(cancellationToken) is { } updateFailure)
            {
                // Only the name index may be answered as a name conflict. The other two
                // unique indexes are on the two position columns, and a violation of either
                // is this service writing its own lists wrongly - reporting one as "that name
                // is taken" is how the bug below stayed hidden for as long as it did, and it
                // sent the search to the uniqueness rule instead of to the write.
                return updateFailure.IsConstraint(VideoStreamRules.NameIndexName)
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
            // The two lists go with it through the foreign keys' cascade, so this is one
            // removal rather than three - and deleting the rows rather than leaving them
            // behind is what keeps "the rows a stream has are its list" true after the stream
            // is gone.
            if (!await _streams.RemoveAsync(id, cancellationToken))
            {
                return NotFound(id);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

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

        /// <inheritdoc />
        public async Task<VideoStreamOutcome> RestartAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => await SetStatusAsync(id, VideoStreamStatus.Running, VideoStreamAction.Restarted, cancellationToken);

        /// <summary>Writes one status to one stream, which is all starting, stopping and restarting do.</summary>
        /// <remarks>
        /// Starting a stream that is already running is a success rather than a complaint: the
        /// row says what the operator asked it to say, and refusing would make a double click
        /// look like a failure of the thing the visitor was trying to achieve.
        ///
        /// The active source is deliberately left alone. It is what the relaying process reports
        /// back, and clearing it here would be this side claiming the process answered when
        /// nothing has asked it anything.
        /// </remarks>
        private async Task<VideoStreamOutcome> SetStatusAsync(
            Guid id,
            VideoStreamStatus status,
            VideoStreamAction action,
            CancellationToken cancellationToken)
        {
            var previous = await _streams.SetStatusAsync(id, status, cancellationToken);

            if (previous is null)
            {
                return NotFound(id);
            }

            if (previous != status)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return new VideoStreamOutcome { Action = action, StreamId = id };
        }

        /// <summary>Checks everything about a stream that can be judged from the request alone.</summary>
        /// <remarks>
        /// The category is deliberately not checked here - whether that identifier names a row
        /// is a question about the database, and it is asked once the body has proved it is
        /// worth asking about.
        /// </remarks>
        private static Dictionary<string, string[]> ValidateShape(
            string name,
            VideoStreamType type,
            VideoStreamStatus status,
            Guid categoryId,
            int delayMinutes,
            int archiveDays,
            Guid? seriesId,
            Guid? vodId,
            Guid? epgSourceId,
            string? epgChannelKey,
            string? restreamUserAgent,
            string? restreamProxy,
            IReadOnlyList<string> sources,
            IReadOnlyList<string> files)
        {
            var errors = VideoStreamRules.ValidateCommon(
                type,
                status,
                categoryId,
                delayMinutes,
                archiveDays,
                restreamUserAgent,
                restreamProxy);

            if (!VideoStreamRules.IsUsableName(name))
            {
                errors[nameof(NewVideoStream.Name)] = VideoStreamRules.NameRule;
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
                    if (!VideoStreamRules.HasUsableEntries(sources, VideoStreamLimits.SourceUrlMaxLength))
                    {
                        errors[nameof(NewVideoStream.Sources)] = VideoStreamRules.SourcesRule;
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
                    if (!VideoStreamRules.HasUsableEntries(files, VideoStreamLimits.FilePathMaxLength))
                    {
                        errors[nameof(NewVideoStream.Files)] = FilesRule;
                    }

                    break;
            }

            return errors;
        }

        /// <summary>
        /// Turns the request's lists and type into the values the repository writes.
        /// </summary>
        /// <remarks>
        /// Clearing the fields the type does not own is this method's whole point: it is the
        /// one place that decides, from the merged type, which list survives and which becomes
        /// empty. Blank entries are dropped and every survivor is trimmed, because the columns
        /// store what the relay will actually use.
        /// </remarks>
        private static VideoStreamWrite BuildWrite(
            string name,
            VideoStreamType type,
            VideoStreamStatus status,
            Guid categoryId,
            string? logoUrl,
            string? enigma2Sid,
            int delayMinutes,
            Guid? seriesId,
            Guid? vodId,
            Guid? epgSourceId,
            string? epgChannelKey,
            int archiveDays,
            string? restreamUserAgent,
            string? restreamProxy,
            string? restreamCookies,
            string? restreamHeaders,
            IReadOnlyList<string> sources,
            IReadOnlyList<string> files)
        {
            var hasEpg = epgSourceId is not null
                && !string.IsNullOrWhiteSpace(epgChannelKey);

            return new VideoStreamWrite
            {
                Name = name,
                Type = type,
                Status = status,
                CategoryId = categoryId,
                LogoUrl = VideoStreamRules.NullIfBlank(logoUrl),
                Enigma2Sid = VideoStreamRules.NullIfBlank(enigma2Sid),
                DelayMinutes = delayMinutes,
                SeriesId = type is VideoStreamType.OnlineSeries ? seriesId : null,
                VodId = type is VideoStreamType.VodPlaylist ? vodId : null,
                EpgSourceId = hasEpg ? epgSourceId : null,
                EpgChannelKey = hasEpg ? epgChannelKey!.Trim() : null,
                ArchiveDays = type is VideoStreamType.Permanent ? archiveDays : 0,
                RestreamUserAgent = VideoStreamRules.NullIfBlank(restreamUserAgent),
                RestreamProxy = VideoStreamRules.NullIfBlank(restreamProxy),
                RestreamCookies = VideoStreamRules.NullIfBlank(restreamCookies),
                RestreamHeaders = VideoStreamRules.NullIfBlank(restreamHeaders),
                Sources = VideoStreamRules.IsUrlKind(type)
                    ? CleanList(sources)
                    : Array.Empty<string>(),
                Files = type is VideoStreamType.FilePlaylist
                    ? CleanList(files)
                    : Array.Empty<string>()
            };
        }

        /// <summary>Drops blank entries and trims every survivor.</summary>
        private static IReadOnlyList<string> CleanList(IReadOnlyList<string> entries)
            => entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Select(entry => entry.Trim())
                .ToList();

        /// <summary>Writes the staged changes, interpreting the one failure that is expected.</summary>
        /// <returns>Null when it was written, or the exception when a unique index refused it.</returns>
        /// <remarks>
        /// Any unique violation is returned rather than thrown here, and the callers sort them:
        /// Only <see cref="VideoStreamRules.NameIndexName"/> becomes a name conflict, everything
        /// else is rethrown so a bug in the list writer cannot wear the sentence about a taken
        /// name.
        /// </remarks>
        private async Task<UniqueViolationException?> SaveAsync(CancellationToken cancellationToken)
            => await _unitOfWork.TrySaveAsync(cancellationToken);

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
    }
}
