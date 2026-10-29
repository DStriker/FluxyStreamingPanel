using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Models;
using Fluxy.Core.Subscriptions.ReadModels;
using Fluxy.Core.Subscriptions.Repositories;
using Fluxy.Core.Subscriptions.Rows;
using Fluxy.Core.Subscriptions.Utils;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Utils;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="ISubscriptionRepository"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The page read projects the <b>computed</b> status and the <b>effective</b> parameters in one
    /// query. That is what keeps the tag and the filter in step: the status filter is translated
    /// into the very predicate the projection uses before the count, so a row counted as expired
    /// is a row drawn as expired.
    /// </para>
    /// <para>
    /// Every order ends with the identifier, for the reason the other repositories give: two rows
    /// can share a creation stamp or an expiry, and without a tie-break the database is free to
    /// answer a different order on every read, which would make a page turn show a row twice or
    /// skip one.
    /// </para>
    /// </remarks>
    internal sealed class SubscriptionRepository : ISubscriptionRepository
    {
        private readonly FluxyDbContext _context;
        private readonly TimeProvider _time;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionRepository"/> class.
        /// </summary>
        /// <param name="context">Context the rows are read from and staged in.</param>
        /// <param name="time">
        /// Clock the computed status is asked about, injected so a test can decide what "now" is
        /// rather than wait for a real expiry to pass.
        /// </param>
        public SubscriptionRepository(FluxyDbContext context, TimeProvider time)
        {
            _context = context;
            _time = time;
        }

        /// <inheritdoc />
        public async Task<SubscriptionPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? ownerId,
            Guid? creatorId,
            SubscriptionStatus? status,
            SubscriptionSortField sortBy,
            SubscriptionSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            var now = _time.GetUtcNow();

            var query = _context.Subscriptions.AsNoTracking();

            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                // Search is over the three service identifiers a support call quotes, not over
                // the owner's name: "which subscription holds this MAC" is the question the box
                // exists to answer. A MAC is stored in canonical form with colons, and an operator
                // pasting one from a device label or a ticket may well have written hyphens, so
                // the separator is transliterated rather than treated as a different character.
                var macNeedle = needle.Replace('-', ':');

                query = query.Where(subscription =>
                    (subscription.PlaylistId != null
                        && subscription.PlaylistId.ToLower().Contains(needle))
                    || (subscription.MagMac != null
                        && subscription.MagMac.ToLower().Contains(macNeedle))
                    || (subscription.Enigma2Mac != null
                        && subscription.Enigma2Mac.ToLower().Contains(macNeedle)));
            }

            if (ownerId is { } owner)
            {
                query = query.Where(subscription => subscription.UserId == owner);
            }

            if (creatorId is { } creator)
            {
                query = query.Where(subscription => subscription.ResellerId == creator);
            }

            if (status is { } wanted)
            {
                query = ApplyStatus(query, wanted, now);
            }

            var total = await query.CountAsync(cancellationToken);

            query = Order(query, sortBy, sortOrder);

            var rows = await query
                // Arithmetic in long: page comes from a query string, so (page - 1) * pageSize can
                // overflow int and reach EF Core as a negative Skip.
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .Select(subscription => new
                {
                    // The owner's blocked-ness and the plan-driven values are correlated reads, so
                    // the list answers them once per row rather than the caller doing it per row.
                    OwnerBlocked = _context.Users.Any(user =>
                        user.Id == subscription.UserId
                        && (user.Status == UserStatus.Blocked
                            || user.Group!.Status == UserStatus.Blocked)),

                    EffectiveTrial = subscription.TariffPlanId == null
                        ? subscription.IsTrial
                        : _context.TariffPlans
                            .Where(plan => plan.Id == subscription.TariffPlanId)
                            .Select(plan => plan.IsTrial)
                            .First(),

                    EffectiveMaxConnections = subscription.TariffPlanId == null
                        ? subscription.MaxConnections
                        : _context.TariffPlans
                            .Where(plan => plan.Id == subscription.TariffPlanId)
                            .Select(plan => (int?)plan.MaxConnections)
                            .First(),

                    SupportsPlaylist = subscription.TariffPlanId == null
                        || _context.TariffPlans
                            .Where(plan => plan.Id == subscription.TariffPlanId)
                            .Select(plan => plan.SupportsM3u)
                            .First(),

                    SupportsMag = subscription.TariffPlanId == null
                        || _context.TariffPlans
                            .Where(plan => plan.Id == subscription.TariffPlanId)
                            .Select(plan => plan.SupportsMag)
                            .First(),

                    SupportsEnigma2 = subscription.TariffPlanId == null
                        || _context.TariffPlans
                            .Where(plan => plan.Id == subscription.TariffPlanId)
                            .Select(plan => plan.SupportsEnigma2)
                            .First(),

                    Item = new SubscriptionListItem
                    {
                        Id = subscription.Id,
                        UserId = subscription.UserId,
                        Username = _context.Users
                            .Where(user => user.Id == subscription.UserId)
                            .Select(user => user.Username)
                            .First(),
                        OwnerBlocked = false,
                        CreatorUsername = subscription.ResellerId == null
                            ? null
                            : _context.Users
                                .Where(user => user.Id == subscription.ResellerId)
                                .Select(user => user.Username)
                                .FirstOrDefault(),
                        TariffPlanId = subscription.TariffPlanId,
                        TariffPlanName = subscription.TariffPlanId == null
                            ? null
                            : _context.TariffPlans
                                .Where(plan => plan.Id == subscription.TariffPlanId)
                                .Select(plan => plan.Name)
                                .FirstOrDefault(),
                        CreatedAt = subscription.CreatedAt,
                        ExpireAt = subscription.ExpireAt,
                        IsTrial = false,
                        Status = SubscriptionStatus.Active,
                        MaxConnections = null,
                        PlaylistId = subscription.PlaylistId,
                        MagMac = subscription.MagMac,
                        Enigma2Mac = subscription.Enigma2Mac,
                        SupportsPlaylist = false,
                        SupportsMag = false,
                        SupportsEnigma2 = false
                    }
                })
                .ToListAsync(cancellationToken);

            var items = rows
                .Select(row => row.Item with
                {
                    OwnerBlocked = row.OwnerBlocked,
                    IsTrial = row.EffectiveTrial,
                    MaxConnections = row.EffectiveMaxConnections,
                    SupportsPlaylist = row.SupportsPlaylist,
                    SupportsMag = row.SupportsMag,
                    SupportsEnigma2 = row.SupportsEnigma2,
                    Status = SubscriptionStatusComposition.Compose(
                        row.Item.ExpireAt,
                        row.OwnerBlocked,
                        row.EffectiveTrial,
                        now)
                })
                .ToList();

            return new SubscriptionPage
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public async Task<SubscriptionDetail?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var row = await _context.Subscriptions
                .AsNoTracking()
                .Where(subscription => subscription.Id == id)
                .Select(subscription => new
                {
                    Subscription = subscription,
                    Username = _context.Users
                        .Where(user => user.Id == subscription.UserId)
                        .Select(user => user.Username)
                        .First(),
                    OwnerBlocked = _context.Users.Any(user =>
                        user.Id == subscription.UserId
                        && (user.Status == UserStatus.Blocked
                            || user.Group!.Status == UserStatus.Blocked)),
                    CreatorUsername = subscription.ResellerId == null
                        ? null
                        : _context.Users
                            .Where(user => user.Id == subscription.ResellerId)
                            .Select(user => user.Username)
                            .FirstOrDefault(),
                    TariffPlanName = subscription.TariffPlanId == null
                        ? null
                        : _context.TariffPlans
                            .Where(plan => plan.Id == subscription.TariffPlanId)
                            .Select(plan => plan.Name)
                            .FirstOrDefault()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (row is null)
            {
                return null;
            }

            var now = _time.GetUtcNow();
            var subscription = row.Subscription;

            // The bouquet set and the plan-derived values are two reads rather than one flattened
            // join: the relations are independent, and a join over both would multiply their rows.
            var bouquetIds = await _context.SubscriptionBouquets
                .AsNoTracking()
                .Where(link => link.SubscriptionId == id)
                .Join(
                    _context.Bouquets.AsNoTracking(),
                    link => link.BouquetId,
                    bouquet => bouquet.Id,
                    (link, bouquet) => new { bouquet.Id, bouquet.SortOrder })
                .OrderBy(entry => entry.SortOrder)
                .ThenBy(entry => entry.Id)
                .Select(entry => entry.Id)
                .ToListAsync(cancellationToken);

            SubscriptionDerivedValues? derived = null;

            if (subscription.TariffPlanId is { } planId)
            {
                var plan = await GetPlanContextAsync(planId, cancellationToken);

                if (plan is not null)
                {
                    derived = new SubscriptionDerivedValues
                    {
                        MaxConnections = plan.MaxConnections,
                        IsRestreamer = plan.AllowRestream,
                        OutputFormats = OutputFormatsOf(plan.OutputMpegTs, plan.OutputHls),
                        IsStbLock = plan.StrictStbDeviceId,
                        IsCountryLock = !plan.AllowMultipleCountries,
                        IsTrial = plan.IsTrial,
                        SupportsPlaylist = plan.SupportsM3u,
                        SupportsMag = plan.SupportsMag,
                        SupportsEnigma2 = plan.SupportsEnigma2,
                        BouquetIds = plan.BouquetIds
                    };
                }
            }

            var effectiveTrial = derived?.IsTrial ?? subscription.IsTrial;

            return new SubscriptionDetail
            {
                Id = subscription.Id,
                UserId = subscription.UserId,
                Username = row.Username,
                ResellerId = subscription.ResellerId,
                CreatorUsername = row.CreatorUsername,
                PlaylistId = subscription.PlaylistId,
                ExpireAt = subscription.ExpireAt,
                TariffPlanId = subscription.TariffPlanId,
                TariffPlanName = row.TariffPlanName,
                Status = SubscriptionStatusComposition.Compose(
                    subscription.ExpireAt,
                    row.OwnerBlocked,
                    effectiveTrial,
                    now),
                MaxConnections = subscription.MaxConnections,
                IsRestreamer = subscription.IsRestreamer,
                IpWhitelist = subscription.IpWhitelist,
                UserAgentWhitelist = subscription.UserAgentWhitelist,
                IsIspLock = subscription.IsIspLock,
                IsCountryLock = subscription.IsCountryLock,
                IsTrial = subscription.IsTrial,
                OutputFormats = subscription.OutputFormats,
                MagMac = subscription.MagMac,
                IsStbLock = subscription.IsStbLock,
                Enigma2Mac = subscription.Enigma2Mac,
                BouquetIds = bouquetIds,
                CreatedAt = subscription.CreatedAt,
                Derived = derived
            };
        }

        /// <inheritdoc />
        public async Task<Subscription?> FindForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            // The bouquet links are included because a replacement compares the wanted set against
            // the stored one - and an unloaded collection reads as empty, never as null, so the
            // comparison would always differ and the fresh rows would collide with the ones still
            // in the table. That is the bug the streams' update carried once.
            var entity = await _context.Subscriptions
                .Include(subscription => subscription.Bouquets)
                .FirstOrDefaultAsync(subscription => subscription.Id == id, cancellationToken);

            return entity?.ToModel();
        }

        /// <inheritdoc />
        public Task<bool> IsPlaylistIdTakenAsync(
            string playlistId,
            Guid? exceptSubscription,
            CancellationToken cancellationToken = default)
            => _context.Subscriptions
                .AsNoTracking()
                .AnyAsync(
                    subscription => subscription.PlaylistId == playlistId
                        && (exceptSubscription == null || subscription.Id != exceptSubscription),
                    cancellationToken);

        /// <inheritdoc />
        public Task<bool> PlaylistIdExistsAsync(
            string playlistId,
            CancellationToken cancellationToken = default)
            => _context.Subscriptions
                .AsNoTracking()
                .AnyAsync(subscription => subscription.PlaylistId == playlistId, cancellationToken);

        /// <inheritdoc />
        public Task<bool> IsMagMacTakenAsync(
            string mac,
            Guid? exceptSubscription,
            CancellationToken cancellationToken = default)
            => _context.Subscriptions
                .AsNoTracking()
                .AnyAsync(
                    subscription => subscription.MagMac == mac
                        && (exceptSubscription == null || subscription.Id != exceptSubscription),
                    cancellationToken);

        /// <inheritdoc />
        public Task<bool> IsEnigma2MacTakenAsync(
            string mac,
            Guid? exceptSubscription,
            CancellationToken cancellationToken = default)
            => _context.Subscriptions
                .AsNoTracking()
                .AnyAsync(
                    subscription => subscription.Enigma2Mac == mac
                        && (exceptSubscription == null || subscription.Id != exceptSubscription),
                    cancellationToken);

        /// <inheritdoc />
        public Task<SubscriptionOwnerContext?> GetOwnerContextAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
            => _context.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new SubscriptionOwnerContext
                {
                    Id = user.Id,
                    Username = user.Username,
                    GroupId = user.GroupId,
                    Role = user.Group!.Role,
                    Blocked = UserStatusComposition.Combine(user.Status, user.Group!.Status)
                        == UserStatus.Blocked
                })
                .FirstOrDefaultAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<SubscriptionPlanContext?> GetPlanContextAsync(
            Guid planId,
            CancellationToken cancellationToken = default)
        {
            var plan = await _context.TariffPlans
                .AsNoTracking()
                .Where(row => row.Id == planId)
                .Select(row => new
                {
                    row.Id,
                    row.Name,
                    row.IsTrial,
                    row.DurationValue,
                    row.DurationUnit,
                    row.SupportsM3u,
                    row.SupportsMag,
                    row.SupportsEnigma2,
                    row.StrictStbDeviceId,
                    row.AllowRestream,
                    row.MaxConnections,
                    row.AllowMultipleCountries,
                    row.OutputMpegTs,
                    row.OutputHls
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (plan is null)
            {
                return null;
            }

            var groupIds = await _context.TariffPlanUserGroups
                .AsNoTracking()
                .Where(link => link.TariffPlanId == planId)
                .Select(link => link.UserGroupId)
                .ToListAsync(cancellationToken);

            var bouquetIds = await _context.TariffPlanBouquets
                .AsNoTracking()
                .Where(link => link.TariffPlanId == planId)
                .Join(
                    _context.Bouquets.AsNoTracking(),
                    link => link.BouquetId,
                    bouquet => bouquet.Id,
                    (link, bouquet) => new { bouquet.Id, bouquet.SortOrder })
                .OrderBy(entry => entry.SortOrder)
                .ThenBy(entry => entry.Id)
                .Select(entry => entry.Id)
                .ToListAsync(cancellationToken);

            return new SubscriptionPlanContext
            {
                Id = plan.Id,
                Name = plan.Name,
                IsTrial = plan.IsTrial,
                DurationValue = plan.DurationValue,
                DurationUnit = plan.DurationUnit,
                SupportsM3u = plan.SupportsM3u,
                SupportsMag = plan.SupportsMag,
                SupportsEnigma2 = plan.SupportsEnigma2,
                StrictStbDeviceId = plan.StrictStbDeviceId,
                AllowRestream = plan.AllowRestream,
                MaxConnections = plan.MaxConnections,
                AllowMultipleCountries = plan.AllowMultipleCountries,
                OutputMpegTs = plan.OutputMpegTs,
                OutputHls = plan.OutputHls,
                UserGroupIds = groupIds,
                BouquetIds = bouquetIds
            };
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Guid>> GetAllBouquetIdsAsync(
            CancellationToken cancellationToken = default)
            => await _context.Bouquets
                .AsNoTracking()
                .OrderBy(bouquet => bouquet.SortOrder)
                .ThenBy(bouquet => bouquet.Id)
                .Select(bouquet => bouquet.Id)
                .ToListAsync(cancellationToken);

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
        public Task<Guid> AddAsync(
            NewSubscription subscription,
            IReadOnlyList<Guid> bouquetIds,
            CancellationToken cancellationToken = default)
        {
            var entity = new SubscriptionEntity
            {
                UserId = subscription.UserId,
                PlaylistId = subscription.PlaylistId,
                ExpireAt = subscription.ExpireAt,
                TariffPlanId = subscription.TariffPlanId,
                MaxConnections = subscription.MaxConnections,
                IsRestreamer = subscription.IsRestreamer,
                IpWhitelist = subscription.IpWhitelist.ToArray(),
                UserAgentWhitelist = subscription.UserAgentWhitelist.ToArray(),
                IsIspLock = subscription.IsIspLock,
                IsCountryLock = subscription.IsCountryLock,
                IsTrial = subscription.IsTrial,
                ResellerId = subscription.ResellerId,
                OutputFormats = subscription.OutputFormats,
                MagMac = subscription.MagMac,
                IsStbLock = subscription.IsStbLock,
                Enigma2Mac = subscription.Enigma2Mac
            };

            _context.Subscriptions.Add(entity);

            foreach (var bouquetId in bouquetIds.Distinct())
            {
                _context.SubscriptionBouquets.Add(new SubscriptionBouquetEntity
                {
                    SubscriptionId = entity.Id,
                    BouquetId = bouquetId
                });
            }

            return Task.FromResult(entity.Id);
        }

        /// <inheritdoc />
        public async Task<bool> UpdateAsync(
            Guid id,
            Subscription subscription,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Subscriptions
                .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

            if (entity is null)
            {
                return false;
            }

            entity.FromModel(subscription);

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Subscriptions
                .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

            if (entity is null)
            {
                return false;
            }

            _context.Subscriptions.Remove(entity);

            return true;
        }

        /// <inheritdoc />
        public async Task ReplaceBouquetsAsync(
            Guid subscriptionId,
            IReadOnlyList<Guid> bouquetIds,
            CancellationToken cancellationToken = default)
        {
            // Tracked rather than ExecuteDelete, so the membership change commits with the row in
            // one transaction and a failed save leaves both halves as they were.
            var rows = await _context.SubscriptionBouquets
                .Where(link => link.SubscriptionId == subscriptionId)
                .ToListAsync(cancellationToken);

            var wanted = bouquetIds.Distinct().ToHashSet();

            _context.SubscriptionBouquets.RemoveRange(
                rows.Where(row => !wanted.Contains(row.BouquetId)));

            var current = rows.Select(row => row.BouquetId).ToHashSet();

            foreach (var bouquetId in wanted.Where(bouquetId => !current.Contains(bouquetId)))
            {
                _context.SubscriptionBouquets.Add(new SubscriptionBouquetEntity
                {
                    SubscriptionId = subscriptionId,
                    BouquetId = bouquetId
                });
            }
        }

        /// <inheritdoc />
        public Task<int> CountForPlanAsync(
            Guid planId,
            CancellationToken cancellationToken = default)
            => _context.Subscriptions
                .AsNoTracking()
                .CountAsync(subscription => subscription.TariffPlanId == planId, cancellationToken);

        /// <summary>
        /// Narrows a page to one computed state, with the same three facts the projection composes.
        /// </summary>
        /// <remarks>
        /// Written once here rather than inlined three times in the page read, because the filter
        /// is the half that must agree with the tag: the three branches below are the precedence
        /// rule of <c>SubscriptionStatusComposition</c> restated as SQL, and a second copy
        /// elsewhere would be the day the two answered differently.
        /// </remarks>
        private IQueryable<SubscriptionEntity> ApplyStatus(
            IQueryable<SubscriptionEntity> query,
            SubscriptionStatus status,
            DateTimeOffset now)
            => status switch
            {
                // Expired first, and the only branch that does not care about the owner: a lapsed
                // subscription is lapsed whatever the account is doing.
                SubscriptionStatus.Expired => query.Where(subscription =>
                    subscription.ExpireAt != null && subscription.ExpireAt <= now),

                // Blocked: not expired, and the owner's effective state is Blocked.
                SubscriptionStatus.Blocked => query.Where(subscription =>
                    (subscription.ExpireAt == null || subscription.ExpireAt > now)
                    && _context.Users.Any(user =>
                        user.Id == subscription.UserId
                        && (user.Status == UserStatus.Blocked
                            || user.Group!.Status == UserStatus.Blocked))),

                // Trial: not expired, owner not blocked, and the effective trial flag is set.
                SubscriptionStatus.Trial => query.Where(subscription =>
                    (subscription.ExpireAt == null || subscription.ExpireAt > now)
                    && !_context.Users.Any(user =>
                        user.Id == subscription.UserId
                        && (user.Status == UserStatus.Blocked
                            || user.Group!.Status == UserStatus.Blocked))
                    && (subscription.TariffPlanId == null
                        ? subscription.IsTrial
                        : _context.TariffPlans
                            .Where(plan => plan.Id == subscription.TariffPlanId)
                            .Select(plan => plan.IsTrial)
                            .First())),

                // Active: the complement of the three above.
                _ => query.Where(subscription =>
                    (subscription.ExpireAt == null || subscription.ExpireAt > now)
                    && !_context.Users.Any(user =>
                        user.Id == subscription.UserId
                        && (user.Status == UserStatus.Blocked
                            || user.Group!.Status == UserStatus.Blocked))
                    && !(subscription.TariffPlanId == null
                        ? subscription.IsTrial
                        : _context.TariffPlans
                            .Where(plan => plan.Id == subscription.TariffPlanId)
                            .Select(plan => plan.IsTrial)
                            .First()))
            };

        /// <summary>
        /// Turns a plan's two output flags into the flag set a subscription row carries.
        /// </summary>
        /// <param name="mpegTs">Whether the plan delivers MPEG-TS.</param>
        /// <param name="hls">Whether the plan delivers HLS.</param>
        private static SubscriptionOutputFormat OutputFormatsOf(bool mpegTs, bool hls)
        {
            var formats = SubscriptionOutputFormat.None;

            if (mpegTs)
            {
                formats |= SubscriptionOutputFormat.MpegTs;
            }

            if (hls)
            {
                formats |= SubscriptionOutputFormat.Hls;
            }

            return formats;
        }

        /// <summary>
        /// Orders the table the way the request asked, with the tie-breaks that make a page turn
        /// repeatable: the requested column, then creation time, then the identifier.
        /// </summary>
        /// <remarks>
        /// An expiry order groups the never-expiring rows last in <b>both</b> directions, which is
        /// what <c>ChainNullsLast</c> gives the accounts table for its two correlated columns: a
        /// page opened "newest expiry first" should not lead with subscriptions that have no
        /// expiry at all.
        /// </remarks>
        private static IQueryable<SubscriptionEntity> Order(
            IQueryable<SubscriptionEntity> query,
            SubscriptionSortField sortBy,
            SubscriptionSortOrder sortOrder)
        {
            var descending = sortOrder == SubscriptionSortOrder.Descending;

            if (sortBy is SubscriptionSortField.ExpireAt)
            {
                var grouped = query.OrderBy(subscription => subscription.ExpireAt == null);

                return descending
                    ? grouped.ThenByDescending(subscription => subscription.ExpireAt)
                        .ThenByDescending(subscription => subscription.CreatedAt)
                        .ThenByDescending(subscription => subscription.Id)
                    : grouped.ThenBy(subscription => subscription.ExpireAt)
                        .ThenBy(subscription => subscription.CreatedAt)
                        .ThenBy(subscription => subscription.Id);
            }

            // Creation time is the default, and it answers both the untouched screen and an
            // explicit sortBy=createdAt. Newest first is the default order.
            return descending
                ? query.OrderByDescending(subscription => subscription.CreatedAt)
                    .ThenByDescending(subscription => subscription.Id)
                : query.OrderBy(subscription => subscription.CreatedAt)
                    .ThenBy(subscription => subscription.Id);
        }
    }
}
