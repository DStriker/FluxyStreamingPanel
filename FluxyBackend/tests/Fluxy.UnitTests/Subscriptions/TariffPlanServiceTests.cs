using Fluxy.Application.Subscriptions.Services;
using Fluxy.Core.Persistence.Exceptions;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Models;
using Fluxy.Core.Subscriptions.Outcomes;
using Fluxy.Core.Subscriptions.ReadModels;
using Fluxy.Core.Subscriptions.Repositories;
using Fluxy.Core.Subscriptions.Rows;

namespace Fluxy.UnitTests.Subscriptions
{
    /// <summary>
    /// The rules the tariff plan service owns beyond the pure ones: a name unique without regard
    /// to case, a patch merged into the row before it is judged, the two relations replaced by
    /// the set the request names, and an identifier in either relation that names nothing
    /// refused with the field named.
    /// </summary>
    /// <remarks>
    /// The repository is faked rather than mocked, the way every other service test here is: the
    /// rules are what is under test, and a fake that answers the questions the service asks is
    /// the smallest thing that can hold them. The database itself is exercised by
    /// <c>TariffPlansTests</c>, which is where the index and the cascade are proved.
    /// </remarks>
    public sealed class TariffPlanServiceTests
    {
        [Fact]
        public async Task CreateTrimsTheNameAndStoresEverySetting()
        {
            var repository = new FakePlans
            {
                BaseGroupIds = [DefaultGroup],
                ExistingGroupIds = [DefaultGroup],
                BouquetIds = [DefaultBouquet]
            };
            var service = new TariffPlanService(repository, new FakeSubscriptions(), new FakeUnitOfWork());

            var outcome = await service.CreateAsync(NewPlan(name: "  Standard  "), TestContext.Current.CancellationToken);

            Assert.Equal(TariffPlanAction.Created, outcome.Action);
            Assert.Equal("Standard", repository.Added!.Name);
            Assert.Equal(120, repository.Added.Cost);
            Assert.Equal(TariffPlanDurationUnit.Months, repository.Added.DurationUnit);
            Assert.Equal(2, repository.Added.MaxConnections);
            Assert.True(repository.Added.AllowMultipleCountries);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateRefusesABlankName(string name)
        {
            var repository = new FakePlans();
            var service = new TariffPlanService(repository, new FakeSubscriptions(), new FakeUnitOfWork());

            var outcome = await service.CreateAsync(NewPlan(name: name), TestContext.Current.CancellationToken);

            Assert.Equal(TariffPlanAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewTariffPlan.Name)));
            Assert.Null(repository.Added);
        }

        [Fact]
        public async Task CreateRefusesANameAnotherPlanHolds()
        {
            var repository = new FakePlans { NameTaken = true };
            var service = new TariffPlanService(repository, new FakeSubscriptions(), new FakeUnitOfWork());

            var outcome = await service.CreateAsync(NewPlan(), TestContext.Current.CancellationToken);

            Assert.Equal(TariffPlanAction.AlreadyExists, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewTariffPlan.Name)));
            Assert.Null(repository.Added);
        }

        [Fact]
        public async Task CreateAnswersAUniqueViolationFromTheNameIndexAsATakenName()
        {
            // Two operators racing: the probe above passed, the write lost. The answer has to be
            // the same one the probe would have given, or the second operator is told the
            // database failed when a name was simply taken.
            var violation = new UniqueViolationException("IX_tariff_plans_name_lower", null);
            var repository = new FakePlans
            {
                BaseGroupIds = [DefaultGroup],
                ExistingGroupIds = [DefaultGroup],
                BouquetIds = [DefaultBouquet]
            };
            var service = new TariffPlanService(repository, new FakeSubscriptions(), new FakeUnitOfWork(violation));

            var outcome = await service.CreateAsync(NewPlan(), TestContext.Current.CancellationToken);

            Assert.Equal(TariffPlanAction.AlreadyExists, outcome.Action);
        }

        [Fact]
        public async Task CreateRethrowsAViolationFromAnIndexItDoesNotKnow()
        {
            var violation = new UniqueViolationException("IX_something_else", null);
            var service = new TariffPlanService(
                new FakePlans
                {
                    BaseGroupIds = [DefaultGroup],
                    ExistingGroupIds = [DefaultGroup],
                    BouquetIds = [DefaultBouquet]
                },
                new FakeSubscriptions(),
                new FakeUnitOfWork(violation));

            await Assert.ThrowsAsync<UniqueViolationException>(
                () => service.CreateAsync(NewPlan(), TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task CreateRefusesAGroupIdentifierThatNamesNothing()
        {
            var repository = new FakePlans { ExistingGroupIds = [] };
            var service = new TariffPlanService(repository, new FakeSubscriptions(), new FakeUnitOfWork());

            var outcome = await service.CreateAsync(
                NewPlan(userGroupIds: [Guid.NewGuid()]),
                TestContext.Current.CancellationToken);

            Assert.Equal(TariffPlanAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewTariffPlan.UserGroupIds)));
            Assert.Null(repository.Added);
        }

        [Fact]
        public async Task CreateRefusesABouquetIdentifierThatNamesNothing()
        {
            var repository = new FakePlans { ExistingBouquetIds = [] };
            var service = new TariffPlanService(repository, new FakeSubscriptions(), new FakeUnitOfWork());

            var outcome = await service.CreateAsync(
                NewPlan(bouquetIds: [Guid.NewGuid()]),
                TestContext.Current.CancellationToken);

            Assert.Equal(TariffPlanAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewTariffPlan.BouquetIds)));
        }

        [Fact]
        public async Task CreateRefusesAnIdentifierNamedTwice()
        {
            var repeated = Guid.NewGuid();
            var repository = new FakePlans { ExistingGroupIds = [repeated] };
            var service = new TariffPlanService(repository, new FakeSubscriptions(), new FakeUnitOfWork());

            var outcome = await service.CreateAsync(
                NewPlan(userGroupIds: [repeated, repeated]),
                TestContext.Current.CancellationToken);

            Assert.Equal(TariffPlanAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewTariffPlan.UserGroupIds)));
        }

        [Fact]
        public async Task CreateOffersThePlanToEveryBaseGroupWhenTheBodyNamesNone()
        {
            var baseGroups = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            // The base groups a create defaults to are real rows, so the existence probe has to
            // recognise them - that is what `GetBaseUserGroupIdsAsync` answers in production.
            var repository = new FakePlans
            {
                BaseGroupIds = baseGroups,
                ExistingGroupIds = baseGroups
            };
            var service = new TariffPlanService(repository, new FakeSubscriptions(), new FakeUnitOfWork());

            var outcome = await service.CreateAsync(
                NewPlan(userGroupIds: null, bouquetIds: [Guid.NewGuid()]),
                TestContext.Current.CancellationToken);

            Assert.Equal(TariffPlanAction.Created, outcome.Action);

            // The default the domain states: an operator who has not decided the audience yet
            // still gets a plan the three base groups may buy.
            Assert.Equal(baseGroups, repository.AddedGroups);
        }

        [Fact]
        public async Task CreateRefusesAPlanOfferedToNobody()
        {
            var repository = new FakePlans { BouquetIds = [DefaultBouquet] };
            var service = new TariffPlanService(repository, new FakeSubscriptions(), new FakeUnitOfWork());

            var outcome = await service.CreateAsync(
                NewPlan(userGroupIds: [], bouquetIds: [Guid.NewGuid()]),
                TestContext.Current.CancellationToken);

            Assert.Equal(TariffPlanAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewTariffPlan.UserGroupIds)));
            Assert.Null(repository.Added);
        }

        [Fact]
        public async Task CreateRefusesAPlanThatDeliversNothing()
        {
            var repository = new FakePlans();
            var service = new TariffPlanService(repository, new FakeSubscriptions(), new FakeUnitOfWork());

            var outcome = await service.CreateAsync(
                NewPlan(bouquetIds: []),
                TestContext.Current.CancellationToken);

            Assert.Equal(TariffPlanAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewTariffPlan.BouquetIds)));
            Assert.Null(repository.Added);
        }

        [Fact]
        public async Task CreateStoresTheRelationsTheBodyNames()
        {
            var group = Guid.NewGuid();
            var bouquet = Guid.NewGuid();
            var repository = new FakePlans { ExistingGroupIds = [group], ExistingBouquetIds = [bouquet] };
            var service = new TariffPlanService(repository, new FakeSubscriptions(), new FakeUnitOfWork());

            var outcome = await service.CreateAsync(
                NewPlan(userGroupIds: [group], bouquetIds: [bouquet]),
                TestContext.Current.CancellationToken);

            Assert.Equal(TariffPlanAction.Created, outcome.Action);
            Assert.Equal([group], repository.AddedGroups);
            Assert.Equal([bouquet], repository.AddedBouquets);
        }

        [Fact]
        public async Task UpdateMergesThePatchIntoTheRowBeforeJudgingIt()
        {
            // The stored row supports MAG with the strict STB check on. Turning MAG off alone
            // would leave the strict check on a plan that has no MAG - the merged row is what
            // the rules must see, not the four fields the body happened to name.
            var id = Guid.NewGuid();
            var repository = new FakePlans
            {
                Existing = Stored(id, supportsMag: true, strictStbDeviceId: true)
            };

            var service = new TariffPlanService(repository, new FakeSubscriptions(), new FakeUnitOfWork());

            var outcome = await service.UpdateAsync(
                id,
                new TariffPlanPatch { SupportsMag = false },
                TestContext.Current.CancellationToken);

            Assert.Equal(TariffPlanAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewTariffPlan.StrictStbDeviceId)));
            Assert.Null(repository.Updated);
        }

        [Fact]
        public async Task UpdateKeepsEveryFieldThePatchDoesNotName()
        {
            var id = Guid.NewGuid();
            var repository = new FakePlans { Existing = Stored(id) };
            var service = new TariffPlanService(repository, new FakeSubscriptions(), new FakeUnitOfWork());

            var outcome = await service.UpdateAsync(
                id,
                new TariffPlanPatch { Cost = 250 },
                TestContext.Current.CancellationToken);

            Assert.Equal(TariffPlanAction.Updated, outcome.Action);
            Assert.Equal(250, repository.Updated!.Cost);
            Assert.Equal("Stored", repository.Updated.Name);
            Assert.Equal(2, repository.Updated.MaxConnections);
        }

        [Fact]
        public async Task UpdateLeavesTheRelationsAloneWhenThePatchNamesNone()
        {
            var id = Guid.NewGuid();
            var repository = new FakePlans { Existing = Stored(id) };
            var service = new TariffPlanService(repository, new FakeSubscriptions(), new FakeUnitOfWork());

            await service.UpdateAsync(id, new TariffPlanPatch { Name = "Renamed" }, TestContext.Current.CancellationToken);

            Assert.Null(repository.WrittenGroupIds);
            Assert.Null(repository.WrittenBouquetIds);
        }

        [Fact]
        public async Task UpdateRefusesClearingTheLastGroup()
        {
            var id = Guid.NewGuid();
            var repository = new FakePlans { Existing = Stored(id) };
            var service = new TariffPlanService(repository, new FakeSubscriptions(), new FakeUnitOfWork());

            var outcome = await service.UpdateAsync(
                id,
                new TariffPlanPatch { UserGroupIds = [] },
                TestContext.Current.CancellationToken);

            Assert.Equal(TariffPlanAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewTariffPlan.UserGroupIds)));
            Assert.Null(repository.Updated);
        }

        [Fact]
        public async Task UpdateRefusesClearingTheLastBouquet()
        {
            var id = Guid.NewGuid();
            var repository = new FakePlans { Existing = Stored(id) };
            var service = new TariffPlanService(repository, new FakeSubscriptions(), new FakeUnitOfWork());

            var outcome = await service.UpdateAsync(
                id,
                new TariffPlanPatch { BouquetIds = [] },
                TestContext.Current.CancellationToken);

            Assert.Equal(TariffPlanAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewTariffPlan.BouquetIds)));
            Assert.Null(repository.Updated);
        }

        [Fact]
        public async Task UpdateReplacesTheRelationsWithTheSetItNames()
        {
            var id = Guid.NewGuid();
            var group = Guid.NewGuid();
            var bouquet = Guid.NewGuid();
            var repository = new FakePlans
            {
                Existing = Stored(id),
                ExistingGroupIds = [group],
                ExistingBouquetIds = [bouquet]
            };
            var service = new TariffPlanService(repository, new FakeSubscriptions(), new FakeUnitOfWork());

            var outcome = await service.UpdateAsync(
                id,
                new TariffPlanPatch { UserGroupIds = [group], BouquetIds = [bouquet] },
                TestContext.Current.CancellationToken);

            Assert.Equal(TariffPlanAction.Updated, outcome.Action);
            Assert.Equal([group], repository.WrittenGroupIds);
            Assert.Equal([bouquet], repository.WrittenBouquetIds);
        }

        [Fact]
        public async Task UpdateAnswersAnUnknownIdentifierAsNotFound()
        {
            var service = new TariffPlanService(new FakePlans(), new FakeSubscriptions(), new FakeUnitOfWork());

            var outcome = await service.UpdateAsync(
                Guid.NewGuid(),
                new TariffPlanPatch { Cost = 1 },
                TestContext.Current.CancellationToken);

            Assert.Equal(TariffPlanAction.NotFound, outcome.Action);
        }

        [Fact]
        public async Task DeleteAnswersAnUnknownIdentifierAsNotFound()
        {
            var service = new TariffPlanService(new FakePlans { Removable = false }, new FakeSubscriptions(), new FakeUnitOfWork());

            var outcome = await service.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

            Assert.Equal(TariffPlanAction.NotFound, outcome.Action);
        }

        [Fact]
        public async Task DeleteRemovesThePlan()
        {
            var id = Guid.NewGuid();
            var repository = new FakePlans { Removable = true };
            var service = new TariffPlanService(repository, new FakeSubscriptions(), new FakeUnitOfWork());

            var outcome = await service.DeleteAsync(id, TestContext.Current.CancellationToken);

            Assert.Equal(TariffPlanAction.Deleted, outcome.Action);
            Assert.Equal(id, outcome.PlanId);
            Assert.True(repository.Removed);
        }

        [Fact]
        public async Task DeleteRefusesAPlanASubscriptionIsStillSoldUnder()
        {
            // The plan's parameters are derived rather than stored while it is attached, so
            // deleting it would silently re-mean the subscriber's subscription. The foreign key is
            // RESTRICT; this refusal is the service asking first so the operator gets a code and a
            // count instead of a provider error.
            var id = Guid.NewGuid();
            var repository = new FakePlans { Removable = true };
            var service = new TariffPlanService(
                repository,
                new FakeSubscriptions { SubscriptionsForPlan = 3 },
                new FakeUnitOfWork());

            var outcome = await service.DeleteAsync(id, TestContext.Current.CancellationToken);

            Assert.Equal(TariffPlanAction.InUse, outcome.Action);
            Assert.Equal(3, outcome.SubscriptionsUsing);
            Assert.False(repository.Removed);
        }

        /// <summary>A plan the service would accept, with the one value a test overrides.</summary>
        /// <remarks>
        /// Both relations start at one identifier that the fake reports as existing, because the
        /// service now refuses a plan offered to nobody and a plan that delivers nothing. A test
        /// that is about neither names its own list.
        /// </remarks>
        private static NewTariffPlan NewPlan(
            string name = "Standard",
            IReadOnlyList<Guid>? userGroupIds = null,
            IReadOnlyList<Guid>? bouquetIds = null)
            => new()
            {
                Name = name,
                Cost = 120,
                DurationValue = 1,
                DurationUnit = TariffPlanDurationUnit.Months,
                SupportsM3u = true,
                SupportsMag = true,
                OutputMpegTs = true,
                OutputHls = true,
                MaxConnections = 2,
                AllowMultipleCountries = true,
                UserGroupIds = userGroupIds,
                BouquetIds = bouquetIds ?? [DefaultBouquet]
            };

        /// <summary>The bouquet every plan a test creates delivers, unless it names its own.</summary>
        private static readonly Guid DefaultBouquet =
            Guid.Parse("00000000-0000-0000-0000-00000000b001");

        /// <summary>The base group a create defaults to when the body names none.</summary>
        private static readonly Guid DefaultGroup =
            Guid.Parse("00000000-0000-0000-0000-00000000a001");

        /// <summary>A stored plan the service reads before merging a patch into it.</summary>
        private static TariffPlan Stored(
            Guid id,
            bool supportsMag = true,
            bool strictStbDeviceId = false)
            => new()
            {
                Id = id,
                Name = "Stored",
                IsTrial = false,
                Cost = 100,
                DurationValue = 1,
                DurationUnit = TariffPlanDurationUnit.Months,
                SupportsM3u = true,
                SupportsMag = supportsMag,
                SupportsEnigma2 = false,
                OutputMpegTs = true,
                OutputHls = false,
                StrictStbDeviceId = strictStbDeviceId,
                AllowRestream = false,
                MaxConnections = 2,
                AllowMultipleCountries = true
            };

        /// <summary>The repository as the service sees it, with the few facts a test sets.</summary>
        private sealed class FakePlans : ITariffPlanRepository
        {
            public TariffPlan? Existing { get; init; }

            public bool NameTaken { get; init; }

            public bool Removable { get; init; } = true;

            public IReadOnlyList<Guid> GroupIds { get; init; } = [];

            public IReadOnlyList<Guid> BouquetIds { get; init; } = [DefaultBouquet];

            public IReadOnlyList<Guid>? ExistingGroupIds { get; init; }

            public IReadOnlyList<Guid>? ExistingBouquetIds { get; init; }

            public IReadOnlyList<Guid> BaseGroupIds { get; init; } = [];

            public NewTariffPlan? Added { get; private set; }

            public IReadOnlyList<Guid>? AddedGroups { get; private set; }

            public IReadOnlyList<Guid>? AddedBouquets { get; private set; }

            public TariffPlan? Updated { get; private set; }

            public IReadOnlyList<Guid>? WrittenGroupIds { get; private set; }

            public IReadOnlyList<Guid>? WrittenBouquetIds { get; private set; }

            public bool Removed { get; private set; }

            public Task<TariffPlanPage> GetPageAsync(
                int page,
                int pageSize,
                string? search,
                TariffPlanSortField sortBy,
                TariffPlanSortOrder sortOrder,
                CancellationToken cancellationToken = default)
                => Task.FromResult(new TariffPlanPage
                {
                    Items = [],
                    Total = 0,
                    Page = page,
                    PageSize = pageSize
                });

            public Task<TariffPlanDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default)
                => Task.FromResult<TariffPlanDetail?>(null);

            public Task<TariffPlan?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
                => Task.FromResult(Existing);

            public Task<bool> IsNameTakenAsync(
                string name,
                Guid? exceptPlan,
                CancellationToken cancellationToken = default)
                => Task.FromResult(NameTaken);

            public Task<Guid> AddAsync(
                NewTariffPlan plan,
                IReadOnlyList<Guid> userGroupIds,
                IReadOnlyList<Guid> bouquetIds,
                CancellationToken cancellationToken = default)
            {
                Added = plan;
                AddedGroups = userGroupIds;
                AddedBouquets = bouquetIds;

                return Task.FromResult(Guid.NewGuid());
            }

            public Task<bool> UpdateAsync(
                Guid id,
                TariffPlan plan,
                CancellationToken cancellationToken = default)
            {
                Updated = plan;

                return Task.FromResult(true);
            }

            public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
            {
                Removed = true;

                return Task.FromResult(Removable);
            }

            public Task SetUserGroupsAsync(
                Guid planId,
                IReadOnlyList<Guid> userGroupIds,
                CancellationToken cancellationToken = default)
            {
                WrittenGroupIds = userGroupIds;

                return Task.CompletedTask;
            }

            public Task SetBouquetsAsync(
                Guid planId,
                IReadOnlyList<Guid> bouquetIds,
                CancellationToken cancellationToken = default)
            {
                WrittenBouquetIds = bouquetIds;

                return Task.CompletedTask;
            }

            public Task<IReadOnlySet<Guid>> GetExistingUserGroupIdsAsync(
                IReadOnlyCollection<Guid> userGroupIds,
                CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlySet<Guid>>(
                    (ExistingGroupIds ?? GroupIds).ToHashSet());

            public Task<IReadOnlySet<Guid>> GetExistingBouquetIdsAsync(
                IReadOnlyCollection<Guid> bouquetIds,
                CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlySet<Guid>>(
                    (ExistingBouquetIds ?? BouquetIds).ToHashSet());

            public Task<IReadOnlyList<Guid>> GetBaseUserGroupIdsAsync(
                CancellationToken cancellationToken = default)
                => Task.FromResult(BaseGroupIds);
        }

        /// <summary>
        /// The subscription repository as the plan service sees it: all it asks is how many
        /// subscriptions are still sold under a plan.
        /// </summary>
        /// <remarks>
        /// The other members throw rather than answer. A plan operation that started asking the
        /// subscription repository anything else would be a change the test wants to notice, and a
        /// silent default would hide it.
        /// </remarks>
        private sealed class FakeSubscriptions : ISubscriptionRepository
        {
            public int SubscriptionsForPlan { get; init; }

            public Task<int> CountForPlanAsync(Guid planId, CancellationToken cancellationToken = default)
                => Task.FromResult(SubscriptionsForPlan);

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
                => throw new NotSupportedException();

            public Task<SubscriptionDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Subscription?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> IsPlaylistIdTakenAsync(
                string playlistId,
                Guid? exceptSubscription,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> PlaylistIdExistsAsync(
                string playlistId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> IsMagMacTakenAsync(
                string mac,
                Guid? exceptSubscription,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> IsEnigma2MacTakenAsync(
                string mac,
                Guid? exceptSubscription,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<SubscriptionOwnerContext?> GetOwnerContextAsync(
                Guid userId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<SubscriptionPlanContext?> GetPlanContextAsync(
                Guid planId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IReadOnlyList<Guid>> GetAllBouquetIdsAsync(
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IReadOnlySet<Guid>> GetExistingBouquetIdsAsync(
                IReadOnlyCollection<Guid> bouquetIds,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Guid> AddAsync(
                NewSubscription subscription,
                IReadOnlyList<Guid> bouquetIds,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> UpdateAsync(
                Guid id,
                Subscription subscription,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task ReplaceBouquetsAsync(
                Guid subscriptionId,
                IReadOnlyList<Guid> bouquetIds,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        /// <summary>The unit of work as the service sees it, with one failure to report.</summary>
        private sealed class FakeUnitOfWork : IUnitOfWork        {
            private readonly Exception? _failure;

            public FakeUnitOfWork(Exception? failure = null) => _failure = failure;

            public Task SaveChangesAsync(CancellationToken cancellationToken = default)
                => _failure is null ? Task.CompletedTask : Task.FromException(_failure);
        }
    }
}
