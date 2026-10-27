using Fluxy.Application.Content.Utils;
using Fluxy.Application.Streams.Utils;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Repositories;
using Fluxy.Core.Persistence.Exceptions;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Utils;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Interfaces;
using Fluxy.Core.Streams.Models;
using Fluxy.Core.Streams.Outcomes;
using Fluxy.Core.Streams.ReadModels;
using Fluxy.Core.Streams.Repositories;
using Fluxy.Core.Streams.Utils;

namespace Fluxy.Application.Streams.Services
{
    /// <summary>
    /// Reads the configured audio streams and changes them on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An audio stream has one payload shape. Both kinds relay from an ordered list of source
    /// addresses, so there is no field the chosen kind switches off and no payload that is
    /// cleared because another kind cannot carry it. What remains is the rule that a stream
    /// with nothing to relay is refused, and the rule that a name is unique within its
    /// category.
    /// </para>
    /// <para>
    /// Every write stages its changes and commits them through the unit of work, which is what
    /// makes a stream and its source rows one operation rather than a row followed by a list
    /// that might not arrive. A unique violation is interpreted here: only the category-scoped
    /// name index becomes "that name is taken", and anything else is rethrown so a bug in the
    /// list writer cannot wear that sentence.
    /// </para>
    /// </remarks>
    public sealed class AudioStreamService : IAudioStreamService
    {
        private readonly IAudioStreamRepository _streams;
        private readonly IBouquetRepository _bouquets;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioStreamService"/> class.
        /// </summary>
        /// <param name="streams">Repository holding the streams and their sources.</param>
        /// <param name="bouquets">
        /// Repository that answers whether the bouquets a stream names exist, and stages the
        /// membership rows that tie the two together.
        /// </param>
        /// <param name="unitOfWork">Commits the changes one operation stages.</param>
        public AudioStreamService(
            IAudioStreamRepository streams,
            IBouquetRepository bouquets,
            IUnitOfWork unitOfWork)
        {
            _streams = streams;
            _bouquets = bouquets;
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public Task<AudioStreamPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? categoryId,
            AudioStreamStatus? status,
            AudioStreamType? type,
            AudioStreamSortField sortBy,
            AudioStreamSortOrder sortOrder,
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
        public Task<AudioStream?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => _streams.GetAsync(id, cancellationToken);

        /// <inheritdoc />
        public async Task<AudioStreamOutcome> CreateAsync(
            NewAudioStream input,
            CancellationToken cancellationToken = default)
        {
            var errors = ValidateShape(
                input.Name,
                input.Type,
                input.Status,
                input.CategoryId,
                input.RestreamUserAgent,
                input.RestreamProxy,
                input.Sources);

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            // The category is checked after the shape rather than before it, because a body
            // that already broke a rule of its own has nothing to say about the database yet.
            if (await _streams.CategoryIsUsableAsync(input.CategoryId, cancellationToken) is false)
            {
                return Invalid(Field(nameof(NewAudioStream.CategoryId), AudioStreamRules.CategoryRule));
            }

            if (await _streams.IsNameTakenAsync(
                input.Name.Trim(),
                input.CategoryId,
                exceptStream: null,
                cancellationToken))
            {
                return AlreadyExists(nameof(NewAudioStream.Name));
            }

            // The bouquets are a picker on the stream's own form, so a body naming one that does
            // not exist is refused with the field named rather than failing at the write.
            if (!await BouquetMembership.AreKnownAsync(_bouquets, input.BouquetIds, cancellationToken))
            {
                return Invalid(Field(nameof(NewAudioStream.BouquetIds), AudioStreamRules.BouquetsRule));
            }

            var write = BuildWrite(
                input.Name.Trim(),
                input.Type,
                input.Status,
                input.CategoryId,
                input.LogoUrl,
                input.Enigma2Sid,
                input.RestreamUserAgent,
                input.RestreamProxy,
                input.RestreamCookies,
                input.RestreamHeaders,
                input.Sources);

            var streamId = await _streams.AddAsync(write, cancellationToken);

            // Staged before the save, so the row and the packages it joined are written by one
            // SaveChanges.
            await BouquetMembership.StageAsync(
                _bouquets,
                ContentType.AudioStream,
                streamId,
                input.BouquetIds,
                cancellationToken);

            if (await SaveAsync(cancellationToken) is { } createFailure)
            {
                // A name another request took between the check above and this write. The
                // answer is the same one the check would have given - but only when the name
                // index is what was refused; any other unique index is a bug in this service,
                // and answering "that name is taken" for one would hide it.
                return createFailure.IsConstraint(AudioStreamRules.NameIndexName)
                    ? AlreadyExists(nameof(NewAudioStream.Name))
                    : throw createFailure;
            }

            return new AudioStreamOutcome
            {
                Action = AudioStreamAction.Created,
                StreamId = streamId
            };
        }

        /// <inheritdoc />
        public async Task<AudioStreamOutcome> UpdateAsync(
            Guid id,
            AudioStreamPatch patch,
            CancellationToken cancellationToken = default)
        {
            // The read brings the sources with the row, and that is load-bearing rather than an
            // optimisation: the replacement decides what to delete by comparing against what
            // the row already holds, and an unloaded collection reads as empty.
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
            var logoUrl = MergeText(patch.LogoUrl, stream.LogoUrl);
            var enigma2Sid = MergeText(patch.Enigma2Sid, stream.Enigma2Sid);
            var restreamUserAgent = MergeText(patch.RestreamUserAgent, stream.RestreamUserAgent);
            var restreamProxy = MergeText(patch.RestreamProxy, stream.RestreamProxy);
            var restreamCookies = MergeText(patch.RestreamCookies, stream.RestreamCookies);
            var restreamHeaders = MergeText(patch.RestreamHeaders, stream.RestreamHeaders);
            var sources = patch.Sources ?? stream.Sources;

            var errors = ValidateShape(
                name,
                type,
                status,
                categoryId,
                restreamUserAgent,
                restreamProxy,
                sources);

            if (errors.Count > 0)
            {
                return Invalid(errors, id);
            }

            if (await _streams.CategoryIsUsableAsync(categoryId, cancellationToken) is false)
            {
                return Invalid(
                    Field(nameof(AudioStreamPatch.CategoryId), AudioStreamRules.CategoryRule),
                    id);
            }

            if (await _streams.IsNameTakenAsync(name, categoryId, id, cancellationToken))
            {
                return AlreadyExists(nameof(AudioStreamPatch.Name), id);
            }

            // Null keeps every link, an empty list leaves every bouquet.
            if (!await BouquetMembership.AreKnownAsync(_bouquets, patch.BouquetIds, cancellationToken))
            {
                return Invalid(Field(nameof(AudioStreamPatch.BouquetIds), AudioStreamRules.BouquetsRule), id);
            }

            var write = BuildWrite(
                name,
                type,
                status,
                categoryId,
                logoUrl,
                enigma2Sid,
                restreamUserAgent,
                restreamProxy,
                restreamCookies,
                restreamHeaders,
                sources);

            await _streams.UpdateAsync(id, write, cancellationToken);

            // Inside the same unit of work as the row itself, so a failure rolls both back.
            await BouquetMembership.StageAsync(
                _bouquets,
                ContentType.AudioStream,
                id,
                patch.BouquetIds,
                cancellationToken);

            if (await SaveAsync(cancellationToken) is { } updateFailure)
            {
                // Only the name index may be answered as a name conflict. The other unique
                // index is on the source position, and a violation of it is this service
                // writing its own list wrongly.
                return updateFailure.IsConstraint(AudioStreamRules.NameIndexName)
                    ? AlreadyExists(nameof(AudioStreamPatch.Name), id)
                    : throw updateFailure;
            }

            return new AudioStreamOutcome
            {
                Action = AudioStreamAction.Updated,
                StreamId = id
            };
        }

        /// <inheritdoc />
        public async Task<AudioStreamOutcome> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            // The sources go with the stream through the foreign key's cascade, so this is one
            // removal rather than two.
            if (!await _streams.RemoveAsync(id, cancellationToken))
            {
                return NotFound(id);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new AudioStreamOutcome
            {
                Action = AudioStreamAction.Removed,
                StreamId = id
            };
        }

        /// <inheritdoc />
        public async Task<AudioStreamOutcome> StartAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => await SetStatusAsync(id, AudioStreamStatus.Running, AudioStreamAction.Started, cancellationToken);

        /// <inheritdoc />
        public async Task<AudioStreamOutcome> StopAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => await SetStatusAsync(id, AudioStreamStatus.Stopped, AudioStreamAction.Stopped, cancellationToken);

        /// <inheritdoc />
        public async Task<AudioStreamOutcome> RestartAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => await SetStatusAsync(id, AudioStreamStatus.Running, AudioStreamAction.Restarted, cancellationToken);

        /// <summary>Writes one status to one stream, which is all starting, stopping and restarting do.</summary>
        /// <remarks>
        /// Starting a stream that is already running is a success rather than a complaint: the
        /// row says what the operator asked it to say, and refusing would make a double click
        /// look like a failure of the thing the visitor was trying to achieve. The active
        /// source is deliberately left alone, because it is what the relaying process reports.
        /// </remarks>
        private async Task<AudioStreamOutcome> SetStatusAsync(
            Guid id,
            AudioStreamStatus status,
            AudioStreamAction action,
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

            return new AudioStreamOutcome { Action = action, StreamId = id };
        }

        /// <summary>Checks everything about a stream that can be judged from the request alone.</summary>
        /// <remarks>
        /// The category is deliberately not checked here - whether that identifier names a row
        /// is a question about the database, and it is asked once the body has proved it is
        /// worth asking about.
        /// </remarks>
        private static Dictionary<string, string[]> ValidateShape(
            string name,
            AudioStreamType type,
            AudioStreamStatus status,
            Guid categoryId,
            string? restreamUserAgent,
            string? restreamProxy,
            IReadOnlyList<string> sources)
        {
            var errors = AudioStreamRules.ValidateCommon(
                type,
                status,
                categoryId,
                restreamUserAgent,
                restreamProxy);

            if (!AudioStreamRules.IsUsableName(name))
            {
                errors[nameof(NewAudioStream.Name)] = AudioStreamRules.NameRule;
            }

            // Both kinds relay from a list, so the list is required whatever the type says.
            if (!AudioStreamRules.HasUsableEntries(sources, AudioStreamLimits.SourceUrlMaxLength))
            {
                errors[nameof(NewAudioStream.Sources)] = AudioStreamRules.SourcesRule;
            }

            return errors;
        }

        /// <summary>Turns the request's values into the values the repository writes.</summary>
        /// <remarks>
        /// Blank entries are dropped and every survivor is trimmed, because the columns store
        /// what the relay will actually use. The order the request named is the order kept.
        /// </remarks>
        private static AudioStreamWrite BuildWrite(
            string name,
            AudioStreamType type,
            AudioStreamStatus status,
            Guid categoryId,
            string? logoUrl,
            string? enigma2Sid,
            string? restreamUserAgent,
            string? restreamProxy,
            string? restreamCookies,
            string? restreamHeaders,
            IReadOnlyList<string> sources)
            => new()
            {
                Name = name,
                Type = type,
                Status = status,
                CategoryId = categoryId,
                LogoUrl = AudioStreamRules.NullIfBlank(logoUrl),
                Enigma2Sid = AudioStreamRules.NullIfBlank(enigma2Sid),
                RestreamUserAgent = AudioStreamRules.NullIfBlank(restreamUserAgent),
                RestreamProxy = AudioStreamRules.NullIfBlank(restreamProxy),
                RestreamCookies = AudioStreamRules.NullIfBlank(restreamCookies),
                RestreamHeaders = AudioStreamRules.NullIfBlank(restreamHeaders),
                Sources = CleanList(sources)
            };

        /// <summary>Drops blank entries and trims every survivor.</summary>
        private static IReadOnlyList<string> CleanList(IReadOnlyList<string> entries)
            => entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Select(entry => entry.Trim())
                .ToList();

        /// <summary>Writes the staged changes, interpreting the one failure that is expected.</summary>
        /// <returns>Null when it was written, or the exception when a unique index refused it.</returns>
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

        /// <summary>One field's worth of rejections, keyed by the C# property name.</summary>
        private static Dictionary<string, string[]> Field(string name, string[] sentences)
            => new(StringComparer.Ordinal) { [name] = sentences };

        private static AudioStreamOutcome NotFound(Guid id)
            => new() { Action = AudioStreamAction.NotFound, StreamId = id };

        private static AudioStreamOutcome AlreadyExists(string field, Guid id = default)
            => new()
            {
                Action = AudioStreamAction.AlreadyExists,
                StreamId = id,
                Errors = Field(field, ["Another stream of this category already has that name."])
            };

        private static AudioStreamOutcome Invalid(
            IReadOnlyDictionary<string, string[]> errors,
            Guid id = default)
            => new() { Action = AudioStreamAction.Invalid, StreamId = id, Errors = errors };
    }
}
