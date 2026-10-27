using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Repositories;

namespace Fluxy.Application.Content.Utils
{
    /// <summary>
    /// The one place a content service asks whether the bouquets a body named exist.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Four content services carry the same optional field, so the rule that decides whether the
    /// field may be written is written once. It is a rule rather than a query: <b>a bouquet a
    /// body names has to exist</b>, because the membership row it would become points at that
    /// bouquet through a foreign key, and a refusal naming the field is worth more to the
    /// operator than a provider's exception at the write.
    /// </para>
    /// <para>
    /// What this helper does <i>not</i> do is stage anything. The membership rows are staged by
    /// <see cref="IBouquetRepository.SetItemBouquetIdsAsync"/> inside the caller's own unit of
    /// work, so a stream and the bouquets it joins are written by one <c>SaveChanges</c> - a
    /// stream that joined half its packages because the second write failed is a row nobody
    /// asked for.
    /// </para>
    /// </remarks>
    public static class BouquetMembership
    {
        /// <summary>
        /// Whether every bouquet the body named exists.
        /// </summary>
        /// <param name="bouquets">Repository answering which bouquets exist.</param>
        /// <param name="bouquetIds">Identifiers a body sent, or null when the field was absent.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// True when the field was absent or every identifier names a bouquet. A duplicate
        /// identifier is not a failure of this check - membership is a set, and the repository
        /// collapses it.
        /// </returns>
        public static async Task<bool> AreKnownAsync(
            IBouquetRepository bouquets,
            IReadOnlyList<Guid>? bouquetIds,
            CancellationToken cancellationToken = default)
        {
            if (bouquetIds is null || bouquetIds.Count == 0)
            {
                return true;
            }

            var existing = await bouquets.GetExistingIdsAsync(bouquetIds, cancellationToken);

            return bouquetIds.All(existing.Contains);
        }

        /// <summary>
        /// Stages the membership of one content item, appending it to every bouquet it joins.
        /// </summary>
        /// <param name="bouquets">Repository staging the membership rows.</param>
        /// <param name="type">Which of the four content kinds the item is.</param>
        /// <param name="contentId">Identifier of the content item.</param>
        /// <param name="bouquetIds">Identifiers the body sent, or null when the field was absent.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task that completes when the rows are staged.</returns>
        /// <remarks>
        /// Null means "the body said nothing about bouquets", which is the patch convention of
        /// this API and leaves every link as it is. An empty list is a statement: it leaves every
        /// bouquet, and is therefore not the same thing as an absent field.
        /// </remarks>
        public static Task StageAsync(
            IBouquetRepository bouquets,
            ContentType type,
            Guid contentId,
            IReadOnlyList<Guid>? bouquetIds,
            CancellationToken cancellationToken = default)
            => bouquetIds is null
                ? Task.CompletedTask
                : bouquets.SetItemBouquetIdsAsync(type, contentId, bouquetIds, cancellationToken);
    }
}
