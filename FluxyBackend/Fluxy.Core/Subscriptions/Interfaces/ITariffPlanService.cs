using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Outcomes;
using Fluxy.Core.Subscriptions.ReadModels;

namespace Fluxy.Core.Subscriptions.Interfaces
{
    /// <summary>
    /// Reads the tariff plans and changes them on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately a service of its own rather than extra methods on the bouquet or the group
    /// service: a plan shares no rule with either. A bouquet is a package of content and a group
    /// is a set of accounts, while a plan is a price with a length and a delivery shape - and it
    /// only *points at* the other two.
    /// </para>
    /// <para>
    /// The interface says nothing about HTTP, codes or cookies. It reports
    /// <see cref="TariffPlanAction"/>, and which of those becomes a 201 and which a 409 is the
    /// transport layer's business.
    /// </para>
    /// </remarks>
    public interface ITariffPlanService
    {
        /// <summary>Reads one filtered page of the plan table.</summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many plans one page holds.</param>
        /// <param name="search">Case-insensitive substring of the name, or null.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<TariffPlanPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            TariffPlanSortField sortBy,
            TariffPlanSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>Reads one plan in full, with its two relations.</summary>
        /// <param name="id">Identifier of the plan.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The plan, or null when no row carries that identifier.</returns>
        Task<TariffPlanDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Creates a plan.</summary>
        /// <param name="input">Everything the form supplied.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, carrying the identifier once the row exists.</returns>
        Task<TariffPlanOutcome> CreateAsync(
            NewTariffPlan input,
            CancellationToken cancellationToken = default);

        /// <summary>Changes a plan. An absent field keeps the value the row holds.</summary>
        /// <param name="id">Identifier of the plan to change.</param>
        /// <param name="patch">What to change about it.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, naming the field that was refused when one was.</returns>
        Task<TariffPlanOutcome> UpdateAsync(
            Guid id,
            TariffPlanPatch patch,
            CancellationToken cancellationToken = default);

        /// <summary>Deletes a plan and the links it holds.</summary>
        /// <param name="id">Identifier of the plan to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome: deleted, or not found.</returns>
        Task<TariffPlanOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
