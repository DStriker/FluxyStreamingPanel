using Fluxy.Application.Subscriptions.Services;
using Fluxy.Application.Subscriptions.Utils;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Models;
using Fluxy.Core.Subscriptions.Outcomes;
using Fluxy.Core.Subscriptions.ReadModels;
using Fluxy.Core.Subscriptions.Repositories;
using Fluxy.Core.Subscriptions.Rows;
using Fluxy.Core.Subscriptions.Utils;
using Fluxy.Core.Users.Enums;

namespace Fluxy.UnitTests.Subscriptions
{
    /// <summary>
    /// The rules the subscription service owns beyond the pure ones: the plan as the source of
    /// truth, the minimum-one-service and minimum-one-bouquet refusals, the plan-availability
    /// check, the expiry recomputed from creation, and the three uniqueness probes.
    /// </summary>
    /// <remarks>
    /// The repository is faked rather than mocked, the way every other service test here is: the
    /// rules are what is under test, and a fake that answers the questions the service asks is the
    /// smallest thing that can hold them. The database itself is exercised by
    /// <c>SubscriptionTests</c>, which is where the indexes and the cascades are proved.
    /// </remarks>
    public sealed class SubscriptionServiceTests
    {
        private static readonly Guid Owner = Guid.Parse("00000000-0000-0000-0000-00000000c001");
        private static readonly Guid OwnerGroup = Guid.Parse("00000000-0000-0000-0000-00000000c002");
        private static readonly Guid Issuer = Guid.Parse("00000000-0000-0000-0000-00000000c003");
        private static readonly Guid Plan = Guid.Parse("00000000-0000-0000-0000-00000000c004");
        private static readonly Guid Bouquet = Guid.Parse("00000000-0000-0000-0000-00000000c005");

        [Fact]
        public async Task ACreateWithoutAPlanStoresTheRequestsOwnParameters()
        {
            var repository = new FakeSubscriptions();
            var service = NewService(repository);

            var outcome = await service.CreateAsync(
                New(playlistId: Playlist(), maxConnections: 5),
                TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.Created, outcome.Action);
            Assert.Null(repository.Added!.TariffPlanId);
            Assert.Equal(5, repository.Added.MaxConnections);
            Assert.Equal(SubscriptionOutputFormat.MpegTs, repository.Added.OutputFormats);
        }

        [Fact]
        public async Task ACreateWithAPlanIgnoresTheDependentValuesTheRequestNames()
        {
            // The scenario the specification names: a save carrying a plan also names different
            // connections and formats, and the stored values are the plan's.
            var repository = new FakeSubscriptions
            {
                Plan = PlanContext(supportsM3u: true, maxConnections: 2)
            };
            var service = NewService(repository);

            var outcome = await service.CreateAsync(
                New(
                    planId: Plan,
                    playlistId: Playlist(),
                    maxConnections: 99,
                    outputFormats: SubscriptionOutputFormat.Hls),
                TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.Created, outcome.Action);
            Assert.Equal(Plan, repository.Added!.TariffPlanId);
            Assert.Equal(2, repository.Added.MaxConnections);
            Assert.Equal(SubscriptionOutputFormat.MpegTs, repository.Added.OutputFormats);
        }

        [Fact]
        public async Task ACreateWithAPlanComputesTheExpiryFromCreation()
        {
            var repository = new FakeSubscriptions { Plan = PlanContext() };
            var service = NewService(repository);

            await service.CreateAsync(
                New(planId: Plan, playlistId: Playlist(), expireAt: DateTimeOffset.UtcNow.AddYears(5)),
                TestContext.Current.CancellationToken);

            // The plan's one month from the subscription's own creation, not the body's 5 years.
            Assert.Equal(
                repository.StoreCreatedAt.AddMonths(1),
                repository.Added!.ExpireAt!.Value,
                TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task ACreateRefusesAPlanTheOwnersGroupIsNotIn()
        {
            var repository = new FakeSubscriptions
            {
                Plan = PlanContext(userGroupIds: [Guid.NewGuid()])
            };
            var service = NewService(repository);

            var outcome = await service.CreateAsync(
                New(planId: Plan, playlistId: Playlist()),
                TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey("TariffPlanId"));
            Assert.Null(repository.Added);
        }

        [Fact]
        public async Task ACreateRefusesASaveThatLeavesNoService()
        {
            var repository = new FakeSubscriptions();
            var service = NewService(repository);

            var outcome = await service.CreateAsync(
                New(playlistId: null, magMac: null, enigma2Mac: null),
                TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.Invalid, outcome.Action);
            Assert.Null(repository.Added);
        }

        [Fact]
        public async Task ACreateClearsAServiceTheAttachedPlanDoesNotSupport()
        {
            // The plan delivers a playlist and nothing else, and the body names a MAG too. The
            // MAG is cleared rather than refused, because the plan is the operator's own statement.
            var repository = new FakeSubscriptions
            {
                Plan = PlanContext(supportsM3u: true, supportsMag: false, supportsEnigma2: false)
            };
            var service = NewService(repository);

            var outcome = await service.CreateAsync(
                New(planId: Plan, playlistId: Playlist(), magMac: "AA-BB-CC-DD-EE-FF"),
                TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.Created, outcome.Action);
            Assert.Null(repository.Added!.MagMac);
            Assert.NotNull(repository.Added.PlaylistId);
        }

        [Fact]
        public async Task ACreateRefusesAPlanThatWouldClearTheOnlyServiceTheBodyNames()
        {
            // A plan with no delivery method at all would clear the playlist and leave nothing.
            var repository = new FakeSubscriptions
            {
                Plan = PlanContext(
                    supportsM3u: false,
                    supportsMag: false,
                    supportsEnigma2: false)
            };
            var service = NewService(repository);

            var outcome = await service.CreateAsync(
                New(planId: Plan, playlistId: Playlist()),
                TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.Invalid, outcome.Action);
            Assert.Null(repository.Added);
        }

        [Fact]
        public async Task ACreateWithoutAPlanPreselectsEveryBouquetWhenTheBodyNamesNone()
        {
            var other = Guid.NewGuid();
            var repository = new FakeSubscriptions { AllBouquets = [Bouquet, other] };
            var service = NewService(repository);

            var outcome = await service.CreateAsync(
                New(playlistId: Playlist(), bouquetIds: null),
                TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.Created, outcome.Action);
            Assert.Equal([Bouquet, other], repository.AddedBouquets);
        }

        [Fact]
        public async Task ACreateWithoutAPlanRefusesAnEmptyBouquetList()
        {
            var repository = new FakeSubscriptions { AllBouquets = [Bouquet] };
            var service = NewService(repository);

            var outcome = await service.CreateAsync(
                New(playlistId: Playlist(), bouquetIds: []),
                TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey("BouquetIds"));
            Assert.Null(repository.Added);
        }

        [Fact]
        public async Task ACreateWithAPlanDoesNotWriteTheBouquetSet()
        {
            var repository = new FakeSubscriptions { Plan = PlanContext() };
            var service = NewService(repository);

            await service.CreateAsync(
                New(planId: Plan, playlistId: Playlist(), bouquetIds: []),
                TestContext.Current.CancellationToken);

            // With a plan the plan's bouquets are the source of truth; the subscription's own list
            // is staged empty and the plan delivers what it delivers.
            Assert.Empty(repository.AddedBouquets!);
        }

        [Fact]
        public async Task ACreateRefusesAnOwnerThatNamesNoAccount()
        {
            var repository = new FakeSubscriptions { OwnerMissing = true };
            var service = NewService(repository);

            var outcome = await service.CreateAsync(New(playlistId: Playlist()), TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewSubscription.UserId)));
        }

        [Fact]
        public async Task ACreateRefusesAMacAnotherSubscriptionHolds()
        {
            var repository = new FakeSubscriptions { MagTaken = true };
            var service = NewService(repository);

            var outcome = await service.CreateAsync(
                New(playlistId: Playlist(), magMac: "AA:BB:CC:DD:EE:FF"),
                TestContext.Current.CancellationToken);

            // A duplicate is a conflict with the table rather than a malformed request, which is
            // why the outcome is `AlreadyExists` (409) and not `Invalid` (400).
            Assert.Equal(SubscriptionAction.AlreadyExists, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey("MagMac"));
            Assert.Null(repository.Added);
        }

        [Fact]
        public async Task ACreateAnswersAUniqueViolationFromAPlaylistIndexAsATakenField()
        {
            var violation = new Core.Persistence.Exceptions.UniqueViolationException(
                SubscriptionRules.PlaylistIdIndexName,
                null);
            var repository = new FakeSubscriptions();
            var service = new SubscriptionService(
                repository,
                new FakeUnitOfWork(violation),
                TimeProvider.System);

            var outcome = await service.CreateAsync(
                New(playlistId: Playlist()),
                TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.AlreadyExists, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey("PlaylistId"));
        }

        [Fact]
        public async Task ACreateRethrowsAViolationFromAnIndexItDoesNotKnow()
        {
            var violation = new Core.Persistence.Exceptions.UniqueViolationException(
                "IX_something_else",
                null);
            var service = new SubscriptionService(
                new FakeSubscriptions(),
                new FakeUnitOfWork(violation),
                TimeProvider.System);

            await Assert.ThrowsAsync<Core.Persistence.Exceptions.UniqueViolationException>(
                () => service.CreateAsync(New(playlistId: Playlist()), TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task UpdateKeepsEveryFieldThePatchDoesNotName()
        {
            var id = Guid.NewGuid();
            var repository = new FakeSubscriptions { Existing = Stored(id) };
            var service = NewService(repository);

            var outcome = await service.UpdateAsync(
                id,
                new SubscriptionPatch { MaxConnections = 7 },
                TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.Updated, outcome.Action);
            Assert.Equal(7, repository.Updated!.MaxConnections);
            Assert.Equal("0123456789abcdef0123456789abcdef", repository.Updated.PlaylistId);
        }

        [Fact]
        public async Task UpdateClearingThePlanReactivatesTheSubscriptionsOwnValues()
        {
            // The scenario the specification names: a save nulls the plan and names max
            // connections 5, so the reference is cleared and the value is stored.
            var id = Guid.NewGuid();
            var repository = new FakeSubscriptions
            {
                Existing = Stored(id, planId: Plan),
                Plan = PlanContext(maxConnections: 2)
            };
            var service = NewService(repository);

            var outcome = await service.UpdateAsync(
                id,
                new SubscriptionPatch { ClearTariffPlan = true, MaxConnections = 5 },
                TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.Updated, outcome.Action);
            Assert.Null(repository.Updated!.TariffPlanId);
            Assert.Equal(5, repository.Updated.MaxConnections);
        }

        [Fact]
        public async Task UpdateWithAPlanRecomputesTheExpiryFromTheCreationDate()
        {
            var id = Guid.NewGuid();
            var createdAt = new DateTimeOffset(2026, 1, 31, 9, 0, 0, TimeSpan.Zero);
            var repository = new FakeSubscriptions
            {
                Existing = Stored(id, createdAt: createdAt),
                Plan = PlanContext()
            };
            var service = NewService(repository);

            await service.UpdateAsync(
                id,
                new SubscriptionPatch { TariffPlanId = Plan },
                TestContext.Current.CancellationToken);

            Assert.Equal(
                new DateTimeOffset(2026, 2, 28, 9, 0, 0, TimeSpan.Zero),
                repository.Updated!.ExpireAt);
        }

        [Fact]
        public async Task UpdateRefusesRemovingTheOnlyService()
        {
            var id = Guid.NewGuid();
            var repository = new FakeSubscriptions { Existing = Stored(id) };
            var service = NewService(repository);

            var outcome = await service.UpdateAsync(
                id,
                new SubscriptionPatch { PlaylistId = string.Empty },
                TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.Invalid, outcome.Action);
            Assert.Null(repository.Updated);
        }

        [Fact]
        public async Task UpdateRefusesReassigningTheIssuerToAClient()
        {
            var id = Guid.NewGuid();
            var repository = new FakeSubscriptions
            {
                Existing = Stored(id),
                IssuerOverride = new SubscriptionOwnerContext
                {
                    Id = Guid.NewGuid(),
                    Username = "client",
                    GroupId = Guid.NewGuid(),
                    Role = UserRole.Client,
                    Blocked = false
                }
            };
            var service = NewService(repository);

            var outcome = await service.UpdateAsync(
                id,
                new SubscriptionPatch { ResellerId = Guid.NewGuid() },
                TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(SubscriptionPatch.ResellerId)));
            Assert.Null(repository.Updated);
        }

        [Fact]
        public async Task UpdateAnswersAnUnknownIdentifierAsNotFound()
        {
            var service = NewService(new FakeSubscriptions());

            var outcome = await service.UpdateAsync(
                Guid.NewGuid(),
                new SubscriptionPatch { MaxConnections = 1 },
                TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.NotFound, outcome.Action);
        }

        [Fact]
        public async Task DeleteAnswersAnUnknownIdentifierAsNotFound()
        {
            var service = NewService(new FakeSubscriptions { Removable = false });

            var outcome = await service.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.NotFound, outcome.Action);
        }

        [Fact]
        public async Task TheGeneratorMintsAThirtyTwoHexIdNoSubscriptionHolds()
        {
            var service = NewService(new FakeSubscriptions());

            var id = await service.GeneratePlaylistIdAsync(TestContext.Current.CancellationToken);

            Assert.True(SubscriptionNormalizer.IsPlaylistId(id));
            Assert.Equal(SubscriptionLimits.PlaylistIdLength, id.Length);
        }

        [Fact]
        public async Task TheGeneratorSkipsAnIdTheTableAlreadyHolds()
        {
            // The fake reports the first candidate as taken and nothing after it. A generator that
            // trusted its first draw would return that taken id and make the operator's save fail.
            var repository = new FakeSubscriptions { ExistingPlaylistIds = 1 };
            var service = NewService(repository);

            var id = await service.GeneratePlaylistIdAsync(TestContext.Current.CancellationToken);

            Assert.True(SubscriptionNormalizer.IsPlaylistId(id));
            Assert.True(repository.Probes > 1);
        }

        private static SubscriptionService NewService(FakeSubscriptions repository)
            => new(repository, new FakeUnitOfWork(null), TimeProvider.System);

        private static string Playlist() => "0123456789abcdef0123456789abcdef";

        private static NewSubscription New(
            string? playlistId = "0123456789abcdef0123456789abcdef",
            string? magMac = null,
            string? enigma2Mac = null,
            Guid? planId = null,
            int? maxConnections = 2,
            SubscriptionOutputFormat outputFormats = SubscriptionOutputFormat.MpegTs,
            DateTimeOffset? expireAt = null,
            IReadOnlyList<Guid>? bouquetIds = null)
            => new()
            {
                UserId = Owner,
                ResellerId = Issuer,
                PlaylistId = playlistId,
                MagMac = magMac,
                Enigma2Mac = enigma2Mac,
                TariffPlanId = planId,
                MaxConnections = maxConnections,
                IsRestreamer = false,
                IpWhitelist = [],
                UserAgentWhitelist = [],
                IsIspLock = false,
                IsCountryLock = false,
                IsTrial = false,
                OutputFormats = outputFormats,
                IsStbLock = false,
                // Left as the caller sent it, including null. The difference is the domain's:
                // an absent list means "every bouquet the installation has", while an empty one
                // is the operator's own statement and is refused by the minimum-one rule.
                BouquetIds = bouquetIds
            };

        private static Subscription Stored(
            Guid id,
            Guid? planId = null,
            DateTimeOffset? createdAt = null)
            => new()
            {
                Id = id,
                UserId = Owner,
                PlaylistId = "0123456789abcdef0123456789abcdef",
                ExpireAt = null,
                TariffPlanId = planId,
                MaxConnections = 2,
                IsRestreamer = false,
                IpWhitelist = [],
                UserAgentWhitelist = [],
                IsIspLock = false,
                IsCountryLock = false,
                IsTrial = false,
                ResellerId = Issuer,
                OutputFormats = SubscriptionOutputFormat.MpegTs,
                MagMac = null,
                IsStbLock = false,
                Enigma2Mac = null,
                BouquetIds = [Bouquet],
                CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
                UpdatedAt = createdAt ?? DateTimeOffset.UtcNow
            };

        private static SubscriptionPlanContext PlanContext(
            IReadOnlyList<Guid>? userGroupIds = null,
            bool supportsM3u = true,
            bool supportsMag = true,
            bool supportsEnigma2 = true,
            int maxConnections = 2)
            => new()
            {
                Id = Plan,
                Name = "Standard",
                IsTrial = false,
                DurationValue = 1,
                DurationUnit = TariffPlanDurationUnit.Months,
                SupportsM3u = supportsM3u,
                SupportsMag = supportsMag,
                SupportsEnigma2 = supportsEnigma2,
                StrictStbDeviceId = false,
                AllowRestream = false,
                MaxConnections = maxConnections,
                AllowMultipleCountries = true,
                OutputMpegTs = true,
                OutputHls = false,
                UserGroupIds = userGroupIds ?? [OwnerGroup],
                BouquetIds = []
            };

        /// <summary>The repository as the service sees it, with the few facts a test sets.</summary>
        private sealed class FakeSubscriptions : ISubscriptionRepository
        {
            /// <summary>
            /// The owner context the service reads back, or null to simulate an account that is
            /// not there. Null on this property means "answer a default context"; the explicit
            /// <see cref="OwnerMissing"/> flag is what makes the account absent, so the two cases
            /// stay readable at the call site.
            /// </summary>
            public SubscriptionOwnerContext? Owner { get; init; }

            /// <summary>Whether the owner lookup answers nothing.</summary>
            public bool OwnerMissing { get; init; }

            /// <summary>Overrides the issuer lookup for the creator-role test.</summary>
            public SubscriptionOwnerContext? IssuerOverride { get; init; }

            public SubscriptionPlanContext? Plan { get; init; }

            public Subscription? Existing { get; init; }

            public bool Removable { get; init; } = true;

            public bool MagTaken { get; init; }

            public bool Enigma2Taken { get; init; }

            public bool PlaylistTaken { get; init; }

            public IReadOnlyList<Guid> AllBouquets { get; init; } = [Bouquet];

            /// <summary>How many playlist ids the generator is pretended to find already taken.</summary>
            public int ExistingPlaylistIds { get; init; }
            public int Probes { get; private set; }

            /// <summary>
            /// The creation stamp the row reports, so a test can prove the expiry is computed from
            /// the subscription's own issue date rather than from the clock.
            /// </summary>
            public DateTimeOffset StoreCreatedAt { get; } = DateTimeOffset.UtcNow;

            public NewSubscription? Added { get; private set; }

            public IReadOnlyList<Guid>? AddedBouquets { get; private set; }

            public Subscription? Updated { get; private set; }

            public Task<SubscriptionPage> GetPageAsync(
                int page,
                int pageSize,
                string? search,
                Guid? ownerId,
                Guid? creatorId,
                SubscriptionStatus? status,
                SubscriptionSortField sortBy,
                SubscriptionSortOrder sortOrder,
                CancellationToken cancellationToken = default)
                => Task.FromResult(new SubscriptionPage
                {
                    Items = [],
                    Total = 0,
                    Page = page,
                    PageSize = pageSize
                });

            public Task<SubscriptionDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default)
                => Task.FromResult<SubscriptionDetail?>(null);

            public Task<Subscription?> FindForUpdateAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => Task.FromResult(Existing);

            public Task<bool> IsPlaylistIdTakenAsync(
                string playlistId,
                Guid? exceptSubscription,
                CancellationToken cancellationToken = default)
                => Task.FromResult(PlaylistTaken);

            public Task<bool> PlaylistIdExistsAsync(
                string playlistId,
                CancellationToken cancellationToken = default)
            {
                Probes++;

                return Task.FromResult(Probes <= ExistingPlaylistIds);
            }

            public Task<bool> IsMagMacTakenAsync(
                string mac,
                Guid? exceptSubscription,
                CancellationToken cancellationToken = default)
                => Task.FromResult(MagTaken);

            public Task<bool> IsEnigma2MacTakenAsync(
                string mac,
                Guid? exceptSubscription,
                CancellationToken cancellationToken = default)
                => Task.FromResult(Enigma2Taken);

            public Task<SubscriptionOwnerContext?> GetOwnerContextAsync(
                Guid userId,
                CancellationToken cancellationToken = default)
            {
                if (IssuerOverride is not null && userId != OwnerUserId)
                {
                    return Task.FromResult<SubscriptionOwnerContext?>(IssuerOverride);
                }

                if (OwnerMissing && userId == OwnerUserId)
                {
                    return Task.FromResult<SubscriptionOwnerContext?>(null);
                }

                return Task.FromResult<SubscriptionOwnerContext?>(Owner ?? DefaultContext(userId));
            }

            public Task<SubscriptionPlanContext?> GetPlanContextAsync(
                Guid planId,
                CancellationToken cancellationToken = default)
                => Task.FromResult(Plan);

            public Task<IReadOnlyList<Guid>> GetAllBouquetIdsAsync(
                CancellationToken cancellationToken = default)
                => Task.FromResult(AllBouquets);

            public Task<IReadOnlySet<Guid>> GetExistingBouquetIdsAsync(
                IReadOnlyCollection<Guid> bouquetIds,
                CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlySet<Guid>>(
                    bouquetIds.Where(id => AllBouquets.Contains(id)).ToHashSet());

            public Task<Guid> AddAsync(
                NewSubscription subscription,
                IReadOnlyList<Guid> bouquetIds,
                CancellationToken cancellationToken = default)
            {
                Added = subscription;
                AddedBouquets = bouquetIds;

                return Task.FromResult(Guid.NewGuid());
            }

            public Task<bool> UpdateAsync(
                Guid id,
                Subscription subscription,
                CancellationToken cancellationToken = default)
            {
                Updated = subscription;

                return Task.FromResult(true);
            }

            public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
                => Task.FromResult(Removable);

            public Task ReplaceBouquetsAsync(
                Guid subscriptionId,
                IReadOnlyList<Guid> bouquetIds,
                CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task<int> CountForPlanAsync(Guid planId, CancellationToken cancellationToken = default)
                => Task.FromResult(0);

            private static readonly Guid OwnerUserId =
                Guid.Parse("00000000-0000-0000-0000-00000000c001");

            private static SubscriptionOwnerContext DefaultContext(Guid userId)
                => new()
                {
                    Id = userId,
                    Username = userId == OwnerUserId ? "owner" : "issuer",
                    GroupId = OwnerGroup,
                    Role = UserRole.Admin,
                    Blocked = false
                };
        }

        /// <summary>The unit of work as the service sees it, with one failure to report.</summary>
        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            private readonly Exception? _failure;

            public FakeUnitOfWork(Exception? failure) => _failure = failure;

            public Task SaveChangesAsync(CancellationToken cancellationToken = default)
                => _failure is null ? Task.CompletedTask : Task.FromException(_failure);
        }
    }
}
