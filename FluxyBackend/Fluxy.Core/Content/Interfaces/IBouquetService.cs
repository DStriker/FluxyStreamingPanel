using Fluxy.Core.Content.Commands;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Models;
using Fluxy.Core.Content.ReadModels;

namespace Fluxy.Core.Content.Interfaces
{
    /// <summary>
    /// Reads the bouquets and changes them - and the content they hold - on an admin's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately a separate service rather than extra methods on
    /// <see cref="IContentCategoryService"/>, for the reason that interface gives about itself:
    /// a bouquet shares no field, no rule and no refusal with a category, and the two answer
    /// different questions - a category files one kind of content, a bouquet packages all four.
    /// </para>
    /// <para>
    /// The interface says nothing about HTTP, codes or cookies. It reports
    /// <see cref="BouquetAction"/>, and which of those becomes a 201 and which a 409 is the
    /// transport layer's business.
    /// </para>
    /// </remarks>
    public interface IBouquetService
    {
        /// <summary>
        /// Reads every bouquet with the counts of what it holds, in the order an operator
        /// arranged them in.
        /// </summary>
        /// <remarks>
        /// There is deliberately no paging here, for the reason the categories list gives: the
        /// bouquets are a screenful of packages rather than a per-row record, and a pager over
        /// them would promise a total nobody asked about.
        /// </remarks>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Every bouquet, lowest position first.</returns>
        Task<IReadOnlyList<BouquetListItem>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the two lists one tab of the content manager draws for one kind of content.
        /// </summary>
        /// <param name="bouquetId">Identifier of the bouquet being read.</param>
        /// <param name="type">Which of the four content kinds the tab is about.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The two lists, or null when no bouquet answers to that identifier.</returns>
        Task<BouquetContent?> GetContentAsync(
            Guid bouquetId,
            ContentType type,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the bouquets one content item belongs to.
        /// </summary>
        /// <remarks>
        /// The read the four content edit forms make to fill their bouquet picker. The order is
        /// the bouquets' own, so the same item always opens with its packages in the same
        /// sequence rather than in whatever order the membership rows happen to have.
        /// </remarks>
        /// <param name="type">Which of the four content kinds the item is.</param>
        /// <param name="contentId">Identifier of the content item.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The identifiers of every bouquet holding the item, possibly empty.</returns>
        Task<IReadOnlyList<Guid>> GetItemBouquetIdsAsync(
            ContentType type,
            Guid contentId,
            CancellationToken cancellationToken = default);

        /// <summary>Creates a bouquet at the end of the list.</summary>
        /// <param name="input">Name of the new bouquet.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, carrying the identifier once the row exists.</returns>
        Task<BouquetOutcome> CreateAsync(
            NewBouquet input,
            CancellationToken cancellationToken = default);

        /// <summary>Changes a bouquet. The one field a body can name is the name.</summary>
        /// <param name="id">Identifier of the bouquet to change.</param>
        /// <param name="patch">What to change about it.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, naming the field that was refused when one was.</returns>
        Task<BouquetOutcome> UpdateAsync(
            Guid id,
            BouquetPatch patch,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a bouquet, but only while it holds nothing.
        /// </summary>
        /// <param name="id">Identifier of the bouquet to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome: removed, refused as not empty with a count, or not found.</returns>
        Task<BouquetOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Rewrites the order of the bouquets to the one the request names.
        /// </summary>
        /// <param name="input">Every bouquet, in the new order.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The outcome: reordered, refused because the list did not name every bouquet exactly
        /// once, or refused because there is nothing to reorder.
        /// </returns>
        Task<BouquetOutcome> ReorderAsync(
            ReorderBouquets input,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces the content of one kind that a bouquet holds, in the order the request names.
        /// </summary>
        /// <param name="bouquetId">Identifier of the bouquet whose content is being set.</param>
        /// <param name="input">The kind and every item of it, in the new order.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The outcome: content set, refused because an identifier names nothing of that kind or
        /// names something twice, or not found.
        /// </returns>
        Task<BouquetOutcome> SetContentAsync(
            Guid bouquetId,
            SetBouquetContent input,
            CancellationToken cancellationToken = default);
    }
}
