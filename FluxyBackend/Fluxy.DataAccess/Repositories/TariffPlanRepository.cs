using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Models;
using Fluxy.Core.Subscriptions.ReadModels;
using Fluxy.Core.Subscriptions.Repositories;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="ITariffPlanRepository"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The page read keeps a deterministic order for every sortable column - the column asked for
    /// first, creation time and identifier as tie-breaks - for the reason the other repositories
    /// give: two rows can share a name prefix, a price or a stamp, and without a tie-break the
    /// database is free to answer a different order on every read, which would make a page turn
    /// show a row twice or skip one.
    /// </para>
    /// <para>
    /// The counts the list draws are two grouped reads rather than a correlated subquery per row:
    /// each is a range scan grouped by the plan identifier, and the answers are stitched together
    /// in memory where the page is at most a thousand rows.
    /// </para>
    /// </remarks>
    internal sealed class TariffPlanRepository : ITariffPlanRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="TariffPlanRepository"/> class.
        /// </summary>
        /// <param name="context">Context the plan rows are read from and staged in.</param>
        public TariffPlanRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<TariffPlanPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            TariffPlanSortField sortBy,
            TariffPlanSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            var query = _context.TariffPlans.AsNoTracking();

            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                query = query.Where(plan => plan.Name.ToLower().Contains(needle));
            }

            var total = await query.CountAsync(cancellationToken);

            query = Order(query, sortBy, sortOrder);

            var rows = await query
                .Select(plan => new
                {
                    plan.CreatedAt,
                    Plan = new TariffPlanListItem
                    {
                        Id = plan.Id,
                        Name = plan.Name,
                        IsTrial = plan.IsTrial,
                        Cost = plan.Cost,
                        DurationValue = plan.DurationValue,
                        DurationUnit = plan.DurationUnit,
                        SupportsM3u = plan.SupportsM3u,
                        SupportsMag = plan.SupportsMag,
                        SupportsEnigma2 = plan.SupportsEnigma2,
                        OutputMpegTs = plan.OutputMpegTs,
                        OutputHls = plan.OutputHls,
                        StrictStbDeviceId = plan.StrictStbDeviceId,
                        AllowRestream = plan.AllowRestream,
                        MaxConnections = plan.MaxConnections,
                        AllowMultipleCountries = plan.AllowMultipleCountries,

                        // The two counts are filled in below, from two grouped reads over the
                        // page's own identifiers. A correlated subquery per row would run twice
                        // per row across a page that is at most a thousand rows.
                        UserGroupCount = 0,
                        BouquetCount = 0
                    }
                })
                // Arithmetic is done in long: page comes from a query string, so
                // (page - 1) * pageSize can overflow int and reach EF Core as a negative Skip.
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var items = rows.Select(row => row.Plan).ToList();

            if (items.Count > 0)
            {
                var planIds = items.Select(item => item.Id).ToList();

                var groupCounts = await _context.TariffPlanUserGroups
                    .AsNoTracking()
                    .Where(row => planIds.Contains(row.TariffPlanId))
                    .GroupBy(row => row.TariffPlanId)
                    .Select(group => new { PlanId = group.Key, Count = group.Count() })
                    .ToDictionaryAsync(entry => entry.PlanId, entry => entry.Count, cancellationToken);

                var bouquetCounts = await _context.TariffPlanBouquets
                    .AsNoTracking()
                    .Where(row => planIds.Contains(row.TariffPlanId))
                    .GroupBy(row => row.TariffPlanId)
                    .Select(group => new { PlanId = group.Key, Count = group.Count() })
                    .ToDictionaryAsync(entry => entry.PlanId, entry => entry.Count, cancellationToken);

                items = items
                    .Select(item => item with
                    {
                        UserGroupCount = groupCounts.GetValueOrDefault(item.Id),
                        BouquetCount = bouquetCounts.GetValueOrDefault(item.Id)
                    })
                    .ToList();
            }

            return new TariffPlanPage
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public async Task<TariffPlanDetail?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var plan = await _context.TariffPlans
                .AsNoTracking()
                .Where(row => row.Id == id)
                .Select(row => new TariffPlanDetail
                {
                    Id = row.Id,
                    Name = row.Name,
                    IsTrial = row.IsTrial,
                    Cost = row.Cost,
                    DurationValue = row.DurationValue,
                    DurationUnit = row.DurationUnit,
                    SupportsM3u = row.SupportsM3u,
                    SupportsMag = row.SupportsMag,
                    SupportsEnigma2 = row.SupportsEnigma2,
                    OutputMpegTs = row.OutputMpegTs,
                    OutputHls = row.OutputHls,
                    StrictStbDeviceId = row.StrictStbDeviceId,
                    AllowRestream = row.AllowRestream,
                    MaxConnections = row.MaxConnections,
                    AllowMultipleCountries = row.AllowMultipleCountries,
                    UserGroupIds = Array.Empty<Guid>(),
                    BouquetIds = Array.Empty<Guid>()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (plan is null)
            {
                return null;
            }

            // Two reads rather than one flattened join: the two relations are independent, and a
            // join over both would multiply their rows and need a distinct afterwards.
            var groupIds = await _context.TariffPlanUserGroups
                .AsNoTracking()
                .Where(row => row.TariffPlanId == id)
                .Join(
                    _context.UserGroups.AsNoTracking(),
                    link => link.UserGroupId,
                    group => group.Id,
                    (link, group) => new { group.Id, group.Name })
                .OrderBy(entry => entry.Name)
                .ThenBy(entry => entry.Id)
                .Select(entry => entry.Id)
                .ToListAsync(cancellationToken);

            var bouquetIds = await _context.TariffPlanBouquets
                .AsNoTracking()
                .Where(row => row.TariffPlanId == id)
                .Join(
                    _context.Bouquets.AsNoTracking(),
                    link => link.BouquetId,
                    bouquet => bouquet.Id,
                    (link, bouquet) => new { bouquet.Id, bouquet.SortOrder })
                .OrderBy(entry => entry.SortOrder)
                .ThenBy(entry => entry.Id)
                .Select(entry => entry.Id)
                .ToListAsync(cancellationToken);

            return plan with { UserGroupIds = groupIds, BouquetIds = bouquetIds };
        }

        /// <inheritdoc />
        public async Task<TariffPlan?> FindForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.TariffPlans
                .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

            return entity?.ToModel();
        }

        /// <inheritdoc />
        public Task<bool> IsNameTakenAsync(
            string name,
            Guid? exceptPlan,
            CancellationToken cancellationToken = default)
        {
            // Compared in lower case on both sides, because the rule is "one name however it is
            // spelled". This is the same comparison the unique index over lower(name) performs,
            // so the probe and the constraint agree about what a duplicate is; the index remains
            // the authority when two operators race.
            var wanted = name.ToLowerInvariant();

            return _context.TariffPlans
                .AsNoTracking()
                .AnyAsync(
                    row => row.Name.ToLower() == wanted
                        && (exceptPlan == null || row.Id != exceptPlan),
                    cancellationToken);
        }

        /// <inheritdoc />
        public Task<Guid> AddAsync(
            NewTariffPlan plan,
            IReadOnlyList<Guid> userGroupIds,
            IReadOnlyList<Guid> bouquetIds,
            CancellationToken cancellationToken = default)
        {
            var entity = new TariffPlanEntity
            {
                Name = plan.Name,
                IsTrial = plan.IsTrial,
                Cost = plan.Cost,
                DurationValue = plan.DurationValue,
                DurationUnit = plan.DurationUnit,
                SupportsM3u = plan.SupportsM3u,
                SupportsMag = plan.SupportsMag,
                SupportsEnigma2 = plan.SupportsEnigma2,
                OutputMpegTs = plan.OutputMpegTs,
                OutputHls = plan.OutputHls,
                StrictStbDeviceId = plan.StrictStbDeviceId,
                AllowRestream = plan.AllowRestream,
                MaxConnections = plan.MaxConnections,
                AllowMultipleCountries = plan.AllowMultipleCountries
            };

            _context.TariffPlans.Add(entity);

            StageUserGroups(entity.Id, userGroupIds);
            StageBouquets(entity.Id, bouquetIds);

            return Task.FromResult(entity.Id);
        }

        /// <inheritdoc />
        public async Task<bool> UpdateAsync(
            Guid id,
            TariffPlan plan,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.TariffPlans
                .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

            if (entity is null)
            {
                return false;
            }

            entity.FromModel(plan);

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.TariffPlans
                .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

            if (entity is null)
            {
                return false;
            }

            _context.TariffPlans.Remove(entity);

            return true;
        }

        /// <inheritdoc />
        public async Task SetUserGroupsAsync(
            Guid planId,
            IReadOnlyList<Guid> userGroupIds,
            CancellationToken cancellationToken = default)
        {
            var rows = await _context.TariffPlanUserGroups
                .Where(row => row.TariffPlanId == planId)
                .ToListAsync(cancellationToken);

            var wanted = userGroupIds.Distinct().ToHashSet();

            _context.TariffPlanUserGroups.RemoveRange(
                rows.Where(row => !wanted.Contains(row.UserGroupId)));

            var current = rows.Select(row => row.UserGroupId).ToHashSet();

            foreach (var groupId in wanted.Where(groupId => !current.Contains(groupId)))
            {
                _context.TariffPlanUserGroups.Add(new TariffPlanUserGroupEntity
                {
                    TariffPlanId = planId,
                    UserGroupId = groupId
                });
            }
        }

        /// <inheritdoc />
        public async Task SetBouquetsAsync(
            Guid planId,
            IReadOnlyList<Guid> bouquetIds,
            CancellationToken cancellationToken = default)
        {
            var rows = await _context.TariffPlanBouquets
                .Where(row => row.TariffPlanId == planId)
                .ToListAsync(cancellationToken);

            var wanted = bouquetIds.Distinct().ToHashSet();

            _context.TariffPlanBouquets.RemoveRange(
                rows.Where(row => !wanted.Contains(row.BouquetId)));

            var current = rows.Select(row => row.BouquetId).ToHashSet();

            foreach (var bouquetId in wanted.Where(bouquetId => !current.Contains(bouquetId)))
            {
                _context.TariffPlanBouquets.Add(new TariffPlanBouquetEntity
                {
                    TariffPlanId = planId,
                    BouquetId = bouquetId
                });
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlySet<Guid>> GetExistingUserGroupIdsAsync(
            IReadOnlyCollection<Guid> userGroupIds,
            CancellationToken cancellationToken = default)
        {
            if (userGroupIds.Count == 0)
            {
                return new HashSet<Guid>();
            }

            var found = await _context.UserGroups
                .AsNoTracking()
                .Where(group => userGroupIds.Contains(group.Id))
                .Select(group => group.Id)
                .ToListAsync(cancellationToken);

            return found.ToHashSet();
        }

        /// <inheritdoc />
        public async Task<IReadOnlySet<Guid>> GetExistingBouquetIdsAsync(
            IReadOnlyCollection<Guid> bouquetIds,
            CancellationToken cancellationToken = default)
        {
            if (bouquetIds.Count == 0)
            {
                return new HashSet<Guid>();
            }

            var found = await _context.Bouquets
                .AsNoTracking()
                .Where(bouquet => bouquetIds.Contains(bouquet.Id))
                .Select(bouquet => bouquet.Id)
                .ToListAsync(cancellationToken);

            return found.ToHashSet();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Guid>> GetBaseUserGroupIdsAsync(
            CancellationToken cancellationToken = default)
        {
            // The three identifiers are constants, and the row that carries one may in principle
            // have been removed by hand - the seeder repairs that on the next start, but a create
            // in between must not link a group that is not there and let the foreign key throw.
            var wanted = Fluxy.Core.Users.Utils.BaseUserGroups.All;

            var found = await _context.UserGroups
                .AsNoTracking()
                .Where(group => wanted.Contains(group.Id))
                .Select(group => group.Id)
                .ToListAsync(cancellationToken);

            // Returned in the enum order the constants are declared in, so the same create
            // produces the same links and the reads downstream open the same way.
            return wanted.Where(found.Contains).ToList();
        }

        /// <summary>Stages the groups a new plan is offered to.</summary>
        private void StageUserGroups(Guid planId, IReadOnlyList<Guid> userGroupIds)
        {
            foreach (var groupId in userGroupIds.Distinct())
            {
                _context.TariffPlanUserGroups.Add(new TariffPlanUserGroupEntity
                {
                    TariffPlanId = planId,
                    UserGroupId = groupId
                });
            }
        }

        /// <summary>Stages the bouquets a new plan delivers.</summary>
        private void StageBouquets(Guid planId, IReadOnlyList<Guid> bouquetIds)
        {
            foreach (var bouquetId in bouquetIds.Distinct())
            {
                _context.TariffPlanBouquets.Add(new TariffPlanBouquetEntity
                {
                    TariffPlanId = planId,
                    BouquetId = bouquetId
                });
            }
        }

        /// <summary>
        /// Orders the table the way the request asked, with the tie-breaks that make a page turn
        /// repeatable: the requested column, then creation time, then the identifier.
        /// </summary>
        private static IQueryable<TariffPlanEntity> Order(
            IQueryable<TariffPlanEntity> query,
            TariffPlanSortField sortBy,
            TariffPlanSortOrder sortOrder)
        {
            var descending = sortOrder == TariffPlanSortOrder.Descending;

            return sortBy switch
            {
                TariffPlanSortField.Cost => descending
                    ? query.OrderByDescending(plan => plan.Cost)
                        .ThenByDescending(plan => plan.CreatedAt)
                        .ThenByDescending(plan => plan.Id)
                    : query.OrderBy(plan => plan.Cost)
                        .ThenBy(plan => plan.CreatedAt)
                        .ThenBy(plan => plan.Id),

                TariffPlanSortField.CreatedAt => descending
                    ? query.OrderByDescending(plan => plan.CreatedAt)
                        .ThenByDescending(plan => plan.Id)
                    : query.OrderBy(plan => plan.CreatedAt)
                        .ThenBy(plan => plan.Id),

                // Name is the default, and it answers both the untouched screen and an explicit
                // sortBy=name. Lower case is the comparison the unique index already makes, so a
                // list sorted this way and a duplicate refused by the database agree on what
                // "the same name" means.
                _ => descending
                    ? query.OrderByDescending(plan => plan.Name.ToLower())
                        .ThenByDescending(plan => plan.CreatedAt)
                        .ThenByDescending(plan => plan.Id)
                    : query.OrderBy(plan => plan.Name.ToLower())
                        .ThenBy(plan => plan.CreatedAt)
                        .ThenBy(plan => plan.Id)
            };
        }
    }
}
