using Fluxy.Core.Vod.Commands;
using Fluxy.Core.Vod.Enums;
using Fluxy.Core.Vod.Models;
using Fluxy.Core.Vod.Outcomes;
using Fluxy.Core.Vod.ReadModels;

namespace Fluxy.Core.Vod.Interfaces
{
    /// <summary>
    /// Reads the on-demand catalogue and changes it on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// A separate service rather than extra methods on a stream service: an on-demand item
    /// shares no field, no rule and no refusal with a live stream. The interface says nothing
    /// about HTTP; which <see cref="VodAction"/> becomes a 201 and which a 409 is the
    /// transport's business.
    /// </remarks>
    public interface IVodService
    {
        /// <summary>
        /// Reads one page of the table, filtered and ordered the way the caller asked.
        /// </summary>
        /// <remarks>
        /// Search, filters, order and page are all applied in the query and before the count,
        /// because <c>Total</c> is what the pager draws.
        /// </remarks>
        Task<VodPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? categoryId,
            VodStatus? status,
            VodType? type,
            VodSortField sortBy,
            VodSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>Reads one item in full.</summary>
        /// <param name="id">Identifier of the item.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The item, or null when there is no such one.</returns>
        Task<VodItem?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Creates an item in its category.</summary>
        /// <param name="input">Everything the item is.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, carrying the identifier once the row exists.</returns>
        Task<VodOutcome> CreateAsync(
            NewVodItem input,
            CancellationToken cancellationToken = default);

        /// <summary>Changes an item. Every field a body can name is optional.</summary>
        /// <param name="id">Identifier of the item to change.</param>
        /// <param name="patch">What to change about it.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, naming the field that was refused when one was.</returns>
        Task<VodOutcome> UpdateAsync(
            Guid id,
            VodItemPatch patch,
            CancellationToken cancellationToken = default);

        /// <summary>Deletes an item.</summary>
        /// <param name="id">Identifier of the item to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome: removed, or not found.</returns>
        Task<VodOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
