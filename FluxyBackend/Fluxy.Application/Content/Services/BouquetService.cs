using Fluxy.Core.Content.Commands;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Interfaces;
using Fluxy.Core.Content.ReadModels;
using Fluxy.Core.Content.Repositories;
using Fluxy.Core.Content.Utils;
using Fluxy.Core.Persistence.Exceptions;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Utils;

namespace Fluxy.Application.Content.Services
{
    /// <summary>
    /// Reads the bouquets and changes them - and the content they hold - on an admin's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three rules live here rather than in the controller so that a second endpoint reaching
    /// the same action cannot forget them: a name is unique for the whole installation
    /// <i>without regard to case</i>, a reorder names <b>every</b> bouquet exactly once before
    /// anything is written, and a bouquet is deleted only while it holds nothing - refused with
    /// a count rather than allowed to strand its membership rows.
    /// </para>
    /// <para>
    /// The content list of one kind is a replacement, because the caller has the whole list on
    /// screen: what it names is what the bouquet holds, and the order it names them in is the
    /// order the bouquet holds them in, written as 1..N. That is the one place an order is
    /// decided; the edit forms' bouquet picker only ever appends (see
    /// <see cref="IBouquetRepository.SetItemBouquetIdsAsync"/>), so a tick there cannot re-order
    /// a list an operator arranged here.
    /// </para>
    /// <para>
    /// Nothing here knows about HTTP. Which <see cref="BouquetAction"/> becomes a 201 and which
    /// becomes a 409 is the transport layer's business.
    /// </para>
    /// </remarks>
    public sealed class BouquetService : IBouquetService
    {
        /// <summary>The sentence a name that is blank or too long gets.</summary>
        private static readonly string[] NameRule =
        [
            "The name must be between 1 and "
                + BouquetLimits.NameMaxLength
                + " characters."
        ];

        /// <summary>The sentence a reorder list that does not match the stored rows gets.</summary>
        private static readonly string[] OrderRule =
        [
            "The list must name every bouquet exactly once, in the new order."
        ];

        /// <summary>The sentence a content list naming nothing of that kind gets.</summary>
        private static readonly string[] ContentRule =
        [
            "Every item must be an existing item of the chosen content type, named once."
        ];

        /// <summary>The sentence a content type that names no member of the enum gets.</summary>
        private static readonly string[] TypeRule =
        [
            "The content type must be one of: videostream, audiostream, videoondemand, series."
        ];

        private readonly IBouquetRepository _bouquets;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="BouquetService"/> class.
        /// </summary>
        /// <param name="bouquets">Repository holding the bouquet and membership rows.</param>
        /// <param name="unitOfWork">Commits the changes one operation stages.</param>
        public BouquetService(IBouquetRepository bouquets, IUnitOfWork unitOfWork)
        {
            _bouquets = bouquets;
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<BouquetListItem>> GetAllAsync(CancellationToken cancellationToken = default)
            => _bouquets.GetAllAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<BouquetContent?> GetContentAsync(
            Guid bouquetId,
            ContentType type,
            CancellationToken cancellationToken = default)
        {
            if (!Enum.IsDefined(type))
            {
                return null;
            }

            // The bouquet is read first so that a caller naming an identifier nobody holds gets
            // the same 404 every other read of one row gives, rather than two empty lists that
            // read like a bouquet that holds nothing.
            if (await _bouquets.FindForUpdateAsync(bouquetId, cancellationToken) is null)
            {
                return null;
            }

            return await _bouquets.GetContentAsync(bouquetId, type, cancellationToken);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<Guid>> GetItemBouquetIdsAsync(
            ContentType type,
            Guid contentId,
            CancellationToken cancellationToken = default)
            => !Enum.IsDefined(type)
                ? Task.FromResult<IReadOnlyList<Guid>>([])
                : _bouquets.GetItemBouquetIdsAsync(type, contentId, cancellationToken);

        /// <inheritdoc />
        public async Task<BouquetOutcome> CreateAsync(
            NewBouquet input,
            CancellationToken cancellationToken = default)
        {
            var name = input.Name.Trim();

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (name.Length is < 1 or > BouquetLimits.NameMaxLength)
            {
                errors[nameof(NewBouquet.Name)] = NameRule;
            }

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            if (await _bouquets.IsNameTakenAsync(name, exceptBouquet: null, cancellationToken))
            {
                return AlreadyExists(nameof(NewBouquet.Name));
            }

            // The end of the list is the highest position plus one - read by the repository
            // rather than claimed by the caller, because the caller's screen can be one move
            // behind the database and the end is a fact about the rows.
            var highest = await _bouquets.GetLastSortOrderAsync(cancellationToken);
            var bouquetId = await _bouquets.AddAsync(input with { Name = name }, highest + 1, cancellationToken);

            if (await SaveAsync(cancellationToken) is not null)
            {
                // A name another request took between the check above and this write - in
                // another spelling, which is exactly what the lower(name) index catches. The
                // answer is the same one the check would have given.
                return AlreadyExists(nameof(NewBouquet.Name));
            }

            return new BouquetOutcome
            {
                Action = BouquetAction.Created,
                BouquetId = bouquetId
            };
        }

        /// <inheritdoc />
        public async Task<BouquetOutcome> UpdateAsync(
            Guid id,
            BouquetPatch patch,
            CancellationToken cancellationToken = default)
        {
            var bouquet = await _bouquets.FindForUpdateAsync(id, cancellationToken);

            if (bouquet is null)
            {
                return NotFound(id);
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var name = patch.Name?.Trim();
            if (patch.Name is not null && name!.Length is < 1 or > BouquetLimits.NameMaxLength)
            {
                errors[nameof(BouquetPatch.Name)] = NameRule;
            }

            if (errors.Count > 0)
            {
                return Invalid(errors, id);
            }

            if (name is not null
                && await _bouquets.IsNameTakenAsync(name, id, cancellationToken))
            {
                return AlreadyExists(nameof(BouquetPatch.Name)) with { BouquetId = id };
            }

            if (name is not null)
            {
                await _bouquets.SetNameAsync(id, name, cancellationToken);
            }

            if (await SaveAsync(cancellationToken) is not null)
            {
                return AlreadyExists(nameof(BouquetPatch.Name)) with { BouquetId = id };
            }

            return new BouquetOutcome
            {
                Action = BouquetAction.Updated,
                BouquetId = id
            };
        }

        /// <inheritdoc />
        public async Task<BouquetOutcome> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var bouquet = await _bouquets.FindForUpdateAsync(id, cancellationToken);

            if (bouquet is null)
            {
                return NotFound(id);
            }

            // Counted before the write rather than by catching the foreign key's refusal, for
            // the reason the categories' delete gives: one answer is "this many items are still
            // in it", the other is a provider's exception wearing a 500. The key would refuse
            // the row anyway; this is what turns that refusal into a sentence.
            var items = await _bouquets.CountItemsAsync(id, cancellationToken);

            if (items > 0)
            {
                return new BouquetOutcome
                {
                    Action = BouquetAction.NotEmpty,
                    BouquetId = id,
                    ItemCount = items
                };
            }

            await _bouquets.RemoveAsync(id, cancellationToken);

            // Deletion leaves a gap in the positions, and nothing closes it. Renumbering the
            // survivors here would be a second source for a fact the reorder request owns.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new BouquetOutcome
            {
                Action = BouquetAction.Removed,
                BouquetId = id
            };
        }

        /// <inheritdoc />
        public async Task<BouquetOutcome> ReorderAsync(
            ReorderBouquets input,
            CancellationToken cancellationToken = default)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            // Read for the comparison, then written over - the same rows, one load. Tracking
            // rather than AsNoTracking because the rows that survive the check are the ones the
            // write below moves.
            var stored = await _bouquets.GetTrackedAsync(cancellationToken);

            // The caller has just dragged a row on a screen that shows the whole list, so it is
            // in a position to name all of it - and naming all of it is the only interpretation
            // a server can apply without guessing. A subset leaves rows without a position, a
            // duplicate makes two claims about one row, and an unknown id means the caller's
            // screen describes rows that are not here. All three are refused whole.
            if (input.OrderedIds.Count != stored.Count
                || input.OrderedIds.Distinct().Count() != input.OrderedIds.Count
                || !input.OrderedIds.All(id => stored.Any(entry => entry.Id == id)))
            {
                errors[nameof(ReorderBouquets.OrderedIds)] = OrderRule;
                return Invalid(errors);
            }

            // Positions 1..N in the order the request named. One save, so a request interrupted
            // halfway cannot leave two bouquets sharing a position: either every row moved or
            // none did.
            for (var index = 0; index < input.OrderedIds.Count; index++)
            {
                await _bouquets.SetSortOrderAsync(input.OrderedIds[index], index + 1, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new BouquetOutcome { Action = BouquetAction.Reordered };
        }

        /// <inheritdoc />
        public async Task<BouquetOutcome> SetContentAsync(
            Guid bouquetId,
            SetBouquetContent input,
            CancellationToken cancellationToken = default)
        {
            if (await _bouquets.FindForUpdateAsync(bouquetId, cancellationToken) is null)
            {
                return NotFound(bouquetId);
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (!Enum.IsDefined(input.ContentType))
            {
                errors[nameof(SetBouquetContent.ContentType)] = TypeRule;
                return Invalid(errors, bouquetId);
            }

            // A duplicate is two claims about one position, so it is refused rather than
            // collapsed: the list is an order, and an order naming a row twice is not one.
            if (input.ContentIds.Distinct().Count() != input.ContentIds.Count)
            {
                errors[nameof(SetBouquetContent.ContentIds)] = ContentRule;
                return Invalid(errors, bouquetId);
            }

            // An identifier naming nothing of that kind is refused rather than skipped, because
            // a request that changed less than it said while answering that it had worked is
            // the shape of bug this API refuses elsewhere too. The read is the available list of
            // the tab, which is exactly "the items of this kind the bouquet does not hold": an
            // id that is neither a member nor available names nothing.
            if (input.ContentIds.Count > 0)
            {
                var content = await _bouquets.GetContentAsync(bouquetId, input.ContentType, cancellationToken);
                var known = content.Members.Select(member => member.Id)
                    .Concat(content.Available.Select(member => member.Id))
                    .ToHashSet();

                if (!input.ContentIds.All(known.Contains))
                {
                    errors[nameof(SetBouquetContent.ContentIds)] = ContentRule;
                    return Invalid(errors, bouquetId);
                }
            }

            await _bouquets.SetContentAsync(bouquetId, input.ContentType, input.ContentIds, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new BouquetOutcome
            {
                Action = BouquetAction.ContentSet,
                BouquetId = bouquetId
            };
        }

        /// <summary>Writes the staged changes, interpreting the one failure that is expected.</summary>
        /// <returns>Null when it was written, or the exception when the name index refused it.</returns>
        /// <remarks>
        /// Only the index making a name unique without regard to case is interpreted. A refusal
        /// by any other index is a bug in this service, and answering it as "that name is taken"
        /// is how such a bug hides behind a sentence about something else.
        /// </remarks>
        private async Task<UniqueViolationException?> SaveAsync(CancellationToken cancellationToken)
            => await _unitOfWork.TrySaveAsync([NameIndexName], cancellationToken);

        /// <summary>The unique index over <c>lower(name)</c> this table's migration creates.</summary>
        private const string NameIndexName = "IX_bouquets_name_lower";

        private static BouquetOutcome NotFound(Guid id)
            => new() { Action = BouquetAction.NotFound, BouquetId = id };

        private static BouquetOutcome AlreadyExists(string field)
            => new()
            {
                Action = BouquetAction.AlreadyExists,
                Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [field] = ["Another bouquet already has that name."]
                }
            };

        private static BouquetOutcome Invalid(
            IReadOnlyDictionary<string, string[]> errors,
            Guid id = default)
            => new() { Action = BouquetAction.Invalid, BouquetId = id, Errors = errors };
    }
}
