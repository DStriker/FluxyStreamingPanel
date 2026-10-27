using Fluxy.Core.Content.Commands;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Models;
using Fluxy.Core.Content.ReadModels;

namespace Fluxy.Core.Content.Repositories
{
    /// <summary>
    /// Reads and writes the bouquets and the membership rows that tie content to them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The repository owns the query shape - the order of the list, the unique-name probe, the
    /// position a new row takes, and which of the four membership tables a call is about - while
    /// the service owns the rules that decide whether a request may reach a write at all.
    /// </para>
    /// <para>
    /// <b>Two ways in, one meaning.</b> <see cref="SetContentAsync"/> writes a whole kind of
    /// content and its order, because the content manager knows both: the operator has just
    /// arranged the list. <see cref="SetItemBouquetIdsAsync"/> writes one item's membership
    /// across bouquets, because a content form knows only that: the operator ticked packages in
    /// a picker and never chose a position. The second therefore **appends**: a package the item
    /// just joined takes it at the end of that package's list, and a package it was already in
    /// keeps the position the bouquet screen gave it. A tick in a picker must never re-order a
    /// list the operator arranged somewhere else.
    /// </para>
    /// </remarks>
    public interface IBouquetRepository
    {
        /// <summary>
        /// Reads every bouquet with the counts of what it holds, lowest position first, with the
        /// same tie-breaks every read of the list uses.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<IReadOnlyList<BouquetListItem>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads every bouquet for a reorder, leaving the rows tracked for the intent methods.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<IReadOnlyList<Bouquet>> GetTrackedAsync(CancellationToken cancellationToken = default);

        /// <summary>Finds one bouquet for a change, leaving its row tracked.</summary>
        /// <param name="id">Identifier of the bouquet.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<Bouquet?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Whether any bouquet exists at all.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> AnyAsync(CancellationToken cancellationToken = default);

        /// <summary>Whether another bouquet already holds the name, compared without regard to case.</summary>
        /// <param name="name">Name to probe.</param>
        /// <param name="exceptBouquet">Bouquet to compare against itself, or null for a create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> IsNameTakenAsync(
            string name,
            Guid? exceptBouquet,
            CancellationToken cancellationToken = default);

        /// <summary>Reads the highest position among the bouquets, or zero when there are none.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<int> GetLastSortOrderAsync(CancellationToken cancellationToken = default);

        /// <summary>Stages a new bouquet at the given position.</summary>
        /// <param name="bouquet">Name of the new bouquet.</param>
        /// <param name="sortOrder">Position the row must carry.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Identifier the new row carries.</returns>
        Task<Guid> AddAsync(
            NewBouquet bouquet,
            int sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a new name on a bouquet read for update.</summary>
        /// <param name="id">Identifier of the bouquet.</param>
        /// <param name="name">Name the row must carry.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetNameAsync(Guid id, string name, CancellationToken cancellationToken = default);

        /// <summary>Stages a position on a bouquet read for update.</summary>
        /// <param name="id">Identifier of the bouquet.</param>
        /// <param name="sortOrder">Position the row must carry.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetSortOrderAsync(Guid id, int sortOrder, CancellationToken cancellationToken = default);

        /// <summary>Stages the removal of a bouquet read for update.</summary>
        /// <param name="id">Identifier of the bouquet.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task RemoveAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts every content item the bouquet holds, across all four kinds.
        /// </summary>
        /// <remarks>
        /// **Every kind**, for the reason <c>ContentCategoryRepository.CountContentAsync</c>
        /// gives: the question is "may this bouquet go?", and a bouquet holding one series is
        /// just as full as one holding a thousand streams.
        /// </remarks>
        /// <param name="bouquetId">Identifier of the bouquet.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<int> CountItemsAsync(Guid bouquetId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads what the bouquet holds of one kind, in the order it holds it, and everything of
        /// that kind it does not hold, ordered by name.
        /// </summary>
        /// <param name="bouquetId">Identifier of the bouquet.</param>
        /// <param name="type">Which of the four content kinds is wanted.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<BouquetContent> GetContentAsync(
            Guid bouquetId,
            ContentType type,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages the membership and order of one kind of content: the list becomes exactly the
        /// items it names, at positions 1..N in the order it names them.
        /// </summary>
        /// <remarks>
        /// A replacement rather than a diff, because the caller has the whole list on screen.
        /// The rows of every other kind are untouched, and so is their order.
        /// </remarks>
        /// <param name="bouquetId">Identifier of the bouquet.</param>
        /// <param name="type">Which of the four content kinds is being set.</param>
        /// <param name="contentIds">Every item of that kind, in the order it must hold them.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetContentAsync(
            Guid bouquetId,
            ContentType type,
            IReadOnlyList<Guid> contentIds,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the bouquets one content item belongs to, in the bouquets' own order.
        /// </summary>
        /// <param name="type">Which of the four content kinds the item is.</param>
        /// <param name="contentId">Identifier of the content item.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<IReadOnlyList<Guid>> GetItemBouquetIdsAsync(
            ContentType type,
            Guid contentId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages one content item's membership across bouquets, appending it to the end of every
        /// bouquet it joins and leaving its position in every bouquet it was already in alone.
        /// </summary>
        /// <remarks>
        /// Membership is a set here, not a list: the order of <paramref name="bouquetIds"/> means
        /// nothing. A bouquet named twice is one membership, and a bouquet not named is a link
        /// that is removed - the position it held is not closed up, because positions are allowed
        /// gaps and renumbering is the content manager's job rather than an edit form's.
        /// </remarks>
        /// <param name="type">Which of the four content kinds the item is.</param>
        /// <param name="contentId">Identifier of the content item.</param>
        /// <param name="bouquetIds">Identifiers of every bouquet the item must belong to.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetItemBouquetIdsAsync(
            ContentType type,
            Guid contentId,
            IReadOnlyList<Guid> bouquetIds,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads which of the named bouquets exist.
        /// </summary>
        /// <remarks>
        /// The probe behind the picker's validation: a body naming a bouquet that has been
        /// deleted a moment ago must be refused with the field named, not written as a
        /// membership row the foreign key would then throw on.
        /// </remarks>
        /// <param name="bouquetIds">Identifiers to test.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The subset of <paramref name="bouquetIds"/> that names a bouquet.</returns>
        Task<IReadOnlySet<Guid>> GetExistingIdsAsync(
            IReadOnlyCollection<Guid> bouquetIds,
            CancellationToken cancellationToken = default);
    }
}
