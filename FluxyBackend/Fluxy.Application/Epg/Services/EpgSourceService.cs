using Fluxy.Application.Epg.Utils;
using Fluxy.Core.Epg.Commands;
using Fluxy.Core.Epg.Enums;
using Fluxy.Core.Epg.Interfaces;
using Fluxy.Core.Epg.Models;
using Fluxy.Core.Epg.Outcomes;
using Fluxy.Core.Epg.ReadModels;
using Fluxy.Core.Epg.Repositories;
using Fluxy.Core.Epg.Utils;
using Fluxy.Core.Persistence.Exceptions;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Utils;

namespace Fluxy.Application.Epg.Services
{
    /// <summary>
    /// Reads the programme guide sources and changes them on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A source is three facts an operator owns - its name, the address its guide is fetched
    /// from, and how many days of it are kept. Everything else on the row belongs to the
    /// updater that does not exist yet, so no request here touches it.
    /// </para>
    /// <para>
    /// Nothing here knows about HTTP. Which <see cref="EpgSourceAction"/> becomes a 201 and
    /// which a 409 is the transport layer's business.
    /// </para>
    /// </remarks>
    public sealed class EpgSourceService : IEpgSourceService
    {
        private readonly IEpgSourceRepository _sources;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="EpgSourceService"/> class.
        /// </summary>
        /// <param name="sources">Repository holding the sources.</param>
        /// <param name="unitOfWork">Commits the changes one operation stages.</param>
        public EpgSourceService(IEpgSourceRepository sources, IUnitOfWork unitOfWork)
        {
            _sources = sources;
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public Task<EpgSourcePage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            EpgSourceSortField sortBy,
            EpgSourceSortOrder sortOrder,
            CancellationToken cancellationToken = default)
            => _sources.GetPageAsync(
                page,
                pageSize,
                search,
                sortBy,
                sortOrder,
                cancellationToken);

        /// <inheritdoc />
        public async Task<EpgSourceView?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var source = await _sources.GetAsync(id, cancellationToken);

            return source is null ? null : ToView(source);
        }

        /// <inheritdoc />
        public async Task<EpgSourceChannelPage?> GetChannelsAsync(
            Guid sourceId,
            int page,
            int pageSize,
            string? search,
            EpgSourceChannelSortField sortBy,
            EpgSourceChannelSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            // A source that does not exist and one with no channels are two different
            // answers, and the second is a legitimate page of nothing.
            if (await _sources.ExistsAsync(sourceId, cancellationToken) is false)
            {
                return null;
            }

            return await _sources.GetChannelsPageAsync(
                sourceId,
                page,
                pageSize,
                search,
                sortBy,
                sortOrder,
                cancellationToken);
        }

        /// <inheritdoc />
        public async Task<EpgSourceOutcome> CreateAsync(
            NewEpgSource input,
            CancellationToken cancellationToken = default)
        {
            var name = input.Name.Trim();
            var url = input.Url.Trim();
            var daysKeep = input.DaysKeep ?? EpgSourceLimits.DaysKeepDefault;

            var errors = EpgSourceRules.Validate(name, url, daysKeep);

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            if (await _sources.IsNameTakenAsync(name, exceptSource: null, cancellationToken))
            {
                return AlreadyExists(nameof(NewEpgSource.Name));
            }

            var sourceId = await _sources.AddAsync(
                new EpgSourceWrite
                {
                    Name = name,
                    Url = url,
                    DaysKeep = daysKeep
                },
                cancellationToken);

            if (await _unitOfWork.TrySaveAsync(cancellationToken) is { } createFailure)
            {
                // Only the name index may be answered as a name conflict. Any other unique
                // violation is a bug in this service, and dressing it up as a taken name would
                // hide it behind a sentence about something else entirely.
                return createFailure.IsConstraint(EpgSourceRules.NameIndexName)
                    ? AlreadyExists(nameof(NewEpgSource.Name))
                    : throw createFailure;
            }

            return new EpgSourceOutcome
            {
                Action = EpgSourceAction.Created,
                SourceId = sourceId
            };
        }

        /// <inheritdoc />
        public async Task<EpgSourceOutcome> UpdateAsync(
            Guid id,
            EpgSourcePatch patch,
            CancellationToken cancellationToken = default)
        {
            var source = await _sources.GetAsync(id, cancellationToken);

            if (source is null)
            {
                return NotFound(id);
            }

            // Merged first and judged afterwards, so the rules below see the row as it is
            // about to become rather than as it was.
            var name = patch.Name is null ? source.Name : patch.Name.Trim();
            var url = patch.Url is null ? source.Url : patch.Url.Trim();
            var daysKeep = patch.DaysKeep ?? source.DaysKeep;

            var errors = EpgSourceRules.Validate(name, url, daysKeep);

            if (errors.Count > 0)
            {
                return Invalid(errors, id);
            }

            if (await _sources.IsNameTakenAsync(name, id, cancellationToken))
            {
                return AlreadyExists(nameof(EpgSourcePatch.Name), id);
            }

            await _sources.UpdateAsync(
                id,
                new EpgSourceWrite
                {
                    Name = name,
                    Url = url,
                    DaysKeep = daysKeep
                },
                cancellationToken);

            if (await _unitOfWork.TrySaveAsync(cancellationToken) is { } updateFailure)
            {
                return updateFailure.IsConstraint(EpgSourceRules.NameIndexName)
                    ? AlreadyExists(nameof(EpgSourcePatch.Name), id)
                    : throw updateFailure;
            }

            return new EpgSourceOutcome
            {
                Action = EpgSourceAction.Updated,
                SourceId = id
            };
        }

        /// <inheritdoc />
        public async Task<EpgSourceOutcome> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            // A stream that reads its guide from this source is a reason to refuse rather than
            // to break the stream: the foreign key would refuse it anyway, and this answers
            // with a code and a count instead of a database failure. The programme rows filed
            // under the source are deleted by the cascade, which is exactly what "the guide
            // goes with its source" means.
            var streamsUsing = await _sources.CountStreamsUsingAsync(id, cancellationToken);

            if (streamsUsing > 0)
            {
                return new EpgSourceOutcome
                {
                    Action = EpgSourceAction.InUse,
                    SourceId = id,
                    StreamsUsing = streamsUsing
                };
            }

            if (await _sources.RemoveAsync(id, cancellationToken) is false)
            {
                return NotFound(id);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new EpgSourceOutcome
            {
                Action = EpgSourceAction.Removed,
                SourceId = id
            };
        }

        /// <summary>Turns the stored model into the shape a response is built from.</summary>
        private static EpgSourceView ToView(EpgSource source) => new()
        {
            Id = source.Id,
            Name = source.Name,
            Url = source.Url,
            DaysKeep = source.DaysKeep,
            LastEpgUpdatedAt = source.LastEpgUpdatedAt
        };

        private static EpgSourceOutcome NotFound(Guid id)
            => new() { Action = EpgSourceAction.NotFound, SourceId = id };

        private static EpgSourceOutcome AlreadyExists(string field, Guid id = default)
            => new()
            {
                Action = EpgSourceAction.AlreadyExists,
                SourceId = id,
                Errors = Field(field, ["Another source already has that name."])
            };

        private static EpgSourceOutcome Invalid(
            IReadOnlyDictionary<string, string[]> errors,
            Guid id = default)
            => new() { Action = EpgSourceAction.Invalid, SourceId = id, Errors = errors };

        private static Dictionary<string, string[]> Field(string name, string[] sentences)
            => new(StringComparer.Ordinal) { [name] = sentences };
    }
}
