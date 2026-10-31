using Fluxy.Application.Blacklists.Utils;
using Fluxy.Core.Blacklists.Commands;
using Fluxy.Core.Blacklists.Enums;
using Fluxy.Core.Blacklists.Interfaces;
using Fluxy.Core.Blacklists.Models;
using Fluxy.Core.Blacklists.Outcomes;
using Fluxy.Core.Blacklists.ReadModels;
using Fluxy.Core.Blacklists.Repositories;
using Fluxy.Core.Persistence.Exceptions;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Utils;

namespace Fluxy.Application.Blacklists.Services
{
    /// <summary>
    /// Reads the three blacklists and changes their entries on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An entry is a value, the list it belongs to, a counter of blocked attempts and a note.
    /// Everything an operator may change is the value and the note: the list is what the screen
    /// already is, and the counter and the added date belong to the enforcement that will read
    /// this table later.
    /// </para>
    /// <para>
    /// <b>Uniqueness is checked twice on purpose.</b> The pre-check answers the friendly message
    /// for the ordinary case; the named index is what actually guarantees it, because two
    /// requests can both pass the check and only one can win the insert. Both paths answer
    /// <see cref="BlacklistEntryAction.AlreadyExists"/>, so a caller cannot tell which one it
    /// met - and a violation from any other index is rethrown rather than dressed up as a
    /// taken value.
    /// </para>
    /// <para>
    /// Nothing here knows about HTTP. Which <see cref="BlacklistEntryAction"/> becomes a 201 and
    /// which a 409 is the transport layer's business.
    /// </para>
    /// </remarks>
    public sealed class BlacklistEntryService : IBlacklistEntryService
    {
        private readonly IBlacklistEntryRepository _entries;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlacklistEntryService"/> class.
        /// </summary>
        /// <param name="entries">Repository holding the entries.</param>
        /// <param name="unitOfWork">Commits the changes one operation stages.</param>
        public BlacklistEntryService(
            IBlacklistEntryRepository entries,
            IUnitOfWork unitOfWork)
        {
            _entries = entries;
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public Task<BlacklistEntryPage> GetPageAsync(
            BlacklistType type,
            int page,
            int pageSize,
            string? search,
            BlacklistSortField sortBy,
            BlacklistSortOrder sortOrder,
            CancellationToken cancellationToken = default)
            => _entries.GetPageAsync(
                type,
                page,
                pageSize,
                search,
                sortBy,
                sortOrder,
                cancellationToken);

        /// <inheritdoc />
        public async Task<BlacklistEntryView?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var entry = await _entries.GetAsync(id, cancellationToken);

            return entry is null ? null : ToView(entry);
        }

        /// <inheritdoc />
        public async Task<BlacklistEntryOutcome> CreateAsync(
            NewBlacklistEntry input,
            CancellationToken cancellationToken = default)
        {
            var value = input.Value.Trim();
            var note = NormalizeNote(input.Note);

            var errors = BlacklistEntryRules.Validate(input.Type, value, note);

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            if (await _entries.IsValueTakenAsync(
                input.Type,
                value,
                exceptEntry: null,
                cancellationToken))
            {
                return AlreadyExists(nameof(NewBlacklistEntry.Value));
            }

            var entryId = await _entries.AddAsync(
                input.Type,
                new BlacklistEntryWrite { Value = value, Note = note },
                cancellationToken);

            if (await _unitOfWork.TrySaveAsync(cancellationToken) is { } createFailure)
            {
                return createFailure.IsConstraint(BlacklistEntryRules.TypeValueIndexName)
                    ? AlreadyExists(nameof(NewBlacklistEntry.Value))
                    : throw createFailure;
            }

            return new BlacklistEntryOutcome
            {
                Action = BlacklistEntryAction.Created,
                EntryId = entryId
            };
        }

        /// <inheritdoc />
        public async Task<BlacklistEntryOutcome> UpdateAsync(
            Guid id,
            BlacklistEntryPatch patch,
            CancellationToken cancellationToken = default)
        {
            var entry = await _entries.GetAsync(id, cancellationToken);

            if (entry is null)
            {
                return NotFound(id);
            }

            // Merged first and judged afterwards, so the rules below see the row as it is
            // about to become rather than as it was. An absent note keeps the stored one; an
            // empty string is the one way to clear it.
            var value = patch.Value is null ? entry.Value : patch.Value.Trim();
            var note = patch.Note is null ? entry.Note : NormalizeNote(patch.Note);

            var errors = BlacklistEntryRules.Validate(entry.Type, value, note);

            if (errors.Count > 0)
            {
                return Invalid(errors, id);
            }

            // The list is unchanged, so the probe is scoped to the entry's own type: the same
            // text in another list is a different rule, not a conflict.
            if (await _entries.IsValueTakenAsync(entry.Type, value, id, cancellationToken))
            {
                return AlreadyExists(nameof(BlacklistEntryPatch.Value), id);
            }

            // The write reports false for a row that no longer exists: an entry deleted between
            // the read above and this call would otherwise commit an empty save and answer 200
            // for a record the same id's GET answers 404 for. The same rule DeleteAsync keeps.
            if (await _entries.UpdateAsync(
                id,
                new BlacklistEntryWrite { Value = value, Note = note },
                cancellationToken) is false)
            {
                return NotFound(id);
            }

            if (await _unitOfWork.TrySaveAsync(cancellationToken) is { } updateFailure)
            {
                return updateFailure.IsConstraint(BlacklistEntryRules.TypeValueIndexName)
                    ? AlreadyExists(nameof(BlacklistEntryPatch.Value), id)
                    : throw updateFailure;
            }

            return new BlacklistEntryOutcome
            {
                Action = BlacklistEntryAction.Updated,
                EntryId = id
            };
        }

        /// <inheritdoc />
        public async Task<BlacklistEntryOutcome> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            if (await _entries.RemoveAsync(id, cancellationToken) is false)
            {
                return NotFound(id);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new BlacklistEntryOutcome
            {
                Action = BlacklistEntryAction.Removed,
                EntryId = id
            };
        }

        /// <inheritdoc />
        /// <remarks>
        /// The outcome names no single entry, because the command addresses a whole list: an
        /// empty list is not a refusal, it is the command already satisfied, so clearing twice
        /// answers the same success. The rows are deleted in one statement by the repository,
        /// which is why this does not go through the unit of work - there is nothing staged to
        /// commit, and a save after it would be a no-op.
        /// </remarks>
        public async Task<BlacklistEntryOutcome> ClearAsync(
            BlacklistType type,
            CancellationToken cancellationToken = default)
        {
            await _entries.RemoveAllAsync(type, cancellationToken);

            return new BlacklistEntryOutcome
            {
                Action = BlacklistEntryAction.Cleared,
                EntryId = Guid.Empty
            };
        }

        /// <summary>
        /// Trims a note and turns a blank one into null, so "no note" has one spelling in the
        /// database.
        /// </summary>
        /// <remarks>
        /// The caller decides whether a null means "none" or "leave the stored one alone": a
        /// create keeps the null it is handed, while a change only reaches this method when the
        /// request named a note at all. An empty string therefore clears on a change and stores
        /// nothing on a create, which is the same rule the other PATCH endpoints here follow.
        /// </remarks>
        private static string? NormalizeNote(string? note)
        {
            if (note is null)
            {
                return null;
            }

            var trimmed = note.Trim();

            return trimmed.Length == 0 ? null : trimmed;
        }

        /// <summary>Turns the stored model into the shape a response is built from.</summary>
        private static BlacklistEntryView ToView(BlacklistEntry entry) => new()
        {
            Id = entry.Id,
            Type = entry.Type,
            Value = entry.Value,
            BlockedAttempts = entry.BlockedAttempts,
            Note = entry.Note,
            CreatedAt = entry.CreatedAt
        };

        private static BlacklistEntryOutcome NotFound(Guid id)
            => new() { Action = BlacklistEntryAction.NotFound, EntryId = id };

        private static BlacklistEntryOutcome AlreadyExists(string field, Guid id = default)
            => new()
            {
                Action = BlacklistEntryAction.AlreadyExists,
                EntryId = id,
                Errors = Field(field, ["This list already holds that value."])
            };

        private static BlacklistEntryOutcome Invalid(
            IReadOnlyDictionary<string, string[]> errors,
            Guid id = default)
            => new() { Action = BlacklistEntryAction.Invalid, EntryId = id, Errors = errors };

        private static Dictionary<string, string[]> Field(string name, string[] sentences)
            => new(StringComparer.Ordinal) { [name] = sentences };
    }
}
