using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Models;
using Fluxy.Core.Subscriptions.ReadModels;

namespace Fluxy.Core.Subscriptions.Repositories
{
    /// <summary>
    /// Reads and writes the tariff plans and the two junction tables that tie them to groups and
    /// bouquets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The repository owns the query shape - the page, the unique-name probe, the counts the list
    /// draws and the replacement of the two link sets - while the service owns the rules that
    /// decide whether a request may reach a write at all.
    /// </para>
    /// <para>
    /// Membership is a set here, not an ordered list: nothing about a plan depends on the order
    /// its groups or its bouquets are related in, so no position column exists and the reads
    /// return the identifiers in a deterministic order the picking UI can rely on.
    /// </para>
    /// </remarks>
    public interface ITariffPlanRepository
    {
        /// <summary>Reads one filtered page of the table, counted before paging.</summary>
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

        /// <summary>Reads one plan with its two link sets, for the edit form.</summary>
        /// <param name="id">Identifier of the plan.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<TariffPlanDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Reads one plan without its links, leaving the row tracked for a change.</summary>
        /// <param name="id">Identifier of the plan.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<TariffPlan?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Whether another plan already holds the name, compared without regard to case.</summary>
        /// <param name="name">Name to probe.</param>
        /// <param name="exceptPlan">Plan to compare against itself, or null for a create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<bool> IsNameTakenAsync(
            string name,
            Guid? exceptPlan,
            CancellationToken cancellationToken = default);

        /// <summary>Stages a new plan and its two link sets.</summary>
        /// <param name="plan">Values the row is written with.</param>
        /// <param name="userGroupIds">Groups that may be sold it.</param>
        /// <param name="bouquetIds">Bouquets it delivers.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Identifier the new row carries.</returns>
        Task<Guid> AddAsync(
            NewTariffPlan plan,
            IReadOnlyList<Guid> userGroupIds,
            IReadOnlyList<Guid> bouquetIds,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a change to the settings of a plan read for update.
        /// </summary>
        /// <param name="id">Identifier of the plan.</param>
        /// <param name="plan">Values the row must carry after the change.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>False when no row carries the identifier.</returns>
        Task<bool> UpdateAsync(
            Guid id,
            TariffPlan plan,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages the removal of a plan read for update. Its links go with it.
        /// </summary>
        /// <param name="id">Identifier of the plan.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>False when no row carries the identifier.</returns>
        Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages the replacement of the groups that may be sold the plan.
        /// </summary>
        /// <param name="planId">Identifier of the plan.</param>
        /// <param name="userGroupIds">Every group that may be sold it, empty to link none.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetUserGroupsAsync(
            Guid planId,
            IReadOnlyList<Guid> userGroupIds,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages the replacement of the bouquets the plan delivers.
        /// </summary>
        /// <param name="planId">Identifier of the plan.</param>
        /// <param name="bouquetIds">Every bouquet it delivers, empty to deliver none.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetBouquetsAsync(
            Guid planId,
            IReadOnlyList<Guid> bouquetIds,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads which of the named user groups exist.
        /// </summary>
        /// <remarks>
        /// The probe behind a body that names a group deleted a moment ago: it must be refused
        /// with the field named rather than written as a link the foreign key would then throw on.
        /// </remarks>
        /// <param name="userGroupIds">Identifiers to test.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The subset of <paramref name="userGroupIds"/> that names a group.</returns>
        Task<IReadOnlySet<Guid>> GetExistingUserGroupIdsAsync(
            IReadOnlyCollection<Guid> userGroupIds,
            CancellationToken cancellationToken = default);

        /// <summary>Reads which of the named bouquets exist.</summary>
        /// <param name="bouquetIds">Identifiers to test.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The subset of <paramref name="bouquetIds"/> that names a bouquet.</returns>
        Task<IReadOnlySet<Guid>> GetExistingBouquetIdsAsync(
            IReadOnlyCollection<Guid> bouquetIds,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the three base groups a new plan is offered to when the caller names none.
        /// </summary>
        /// <remarks>
        /// The default the domain states: a fresh plan is available to every base group, so an
        /// operator who has not thought about the audience yet still gets a plan that can be
        /// sold. The identifiers are the fixed ones in <c>BaseUserGroups</c>; this returns the
        /// subset that exists, because a repository read cannot invent rows.
        /// </remarks>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<IReadOnlyList<Guid>> GetBaseUserGroupIdsAsync(
            CancellationToken cancellationToken = default);
    }
}
