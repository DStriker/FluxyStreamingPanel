using Fluxy.Application.Content.Services;
using Fluxy.Core.Content.Commands;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Models;
using Fluxy.Core.Content.ReadModels;
using Fluxy.Core.Content.Repositories;
using Fluxy.Core.Content.Utils;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Utils;

namespace Fluxy.UnitTests.Content
{
    /// <summary>
    /// The rules the bouquet service owns: a name unique without regard to case, an order that
    /// must name every row exactly once, content that must name existing items of its own kind,
    /// and a bouquet that may only be deleted while it holds nothing.
    /// </summary>
    /// <remarks>
    /// The repository is faked rather than mocked, the way every other service test here is:
    /// the rules are what is under test, and a fake that answers the four questions the service
    /// asks is the smallest thing that can hold them. The database itself is exercised by
    /// <c>BouquetTests</c>, which is where the index and the cascade are proved.
    /// </remarks>
    public sealed class BouquetServiceTests
    {
        [Fact]
        public async Task CreateTrimsTheNameAndPutsItAfterTheLastOne()
        {
            var repository = new FakeBouquets { LastSortOrder = 4 };
            var service = new BouquetService(repository, new FakeUnitOfWork());

            var outcome = await service.CreateAsync(new NewBouquet { Name = "  Cinema  " }, TestContext.Current.CancellationToken);

            Assert.Equal(BouquetAction.Created, outcome.Action);
            Assert.Equal("Cinema", repository.AddedName);
            Assert.Equal(5, repository.AddedSortOrder);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateRefusesABlankName(string name)
        {
            var repository = new FakeBouquets();
            var service = new BouquetService(repository, new FakeUnitOfWork());

            var outcome = await service.CreateAsync(new NewBouquet { Name = name }, TestContext.Current.CancellationToken);

            Assert.Equal(BouquetAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewBouquet.Name)));
            Assert.Null(repository.AddedName);
        }

        [Fact]
        public async Task CreateRefusesANameLongerThanTheColumn()
        {
            var service = new BouquetService(new FakeBouquets(), new FakeUnitOfWork());

            var outcome = await service.CreateAsync(
                new NewBouquet { Name = new string('a', BouquetLimits.NameMaxLength + 1) }, TestContext.Current.CancellationToken);

            Assert.Equal(BouquetAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewBouquet.Name)));
        }

        [Fact]
        public async Task CreateRefusesANameAnotherBouquetHolds()
        {
            var repository = new FakeBouquets { NameTaken = true };
            var service = new BouquetService(repository, new FakeUnitOfWork());

            var outcome = await service.CreateAsync(new NewBouquet { Name = "Cinema" }, TestContext.Current.CancellationToken);

            Assert.Equal(BouquetAction.AlreadyExists, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewBouquet.Name)));
            Assert.Null(repository.AddedName);
        }

        [Fact]
        public async Task CreateAnswersAUniqueViolationFromTheNameIndexAsATakenName()
        {
            // Two operators racing: the probe above passed, the write lost. The answer has to be
            // the same one the probe would have given, or the second operator is told the
            // database failed when a name was simply taken.
            var repository = new FakeBouquets
            {
                SaveViolation = new Fluxy.Core.Persistence.Exceptions.UniqueViolationException(
                    "IX_bouquets_name_lower",
                    null)
            };

            var service = new BouquetService(repository, new FakeUnitOfWork(repository.SaveViolation));

            var outcome = await service.CreateAsync(new NewBouquet { Name = "Cinema" }, TestContext.Current.CancellationToken);

            Assert.Equal(BouquetAction.AlreadyExists, outcome.Action);
        }

        [Fact]
        public async Task RenameComparesTheNameAgainstEveryOtherBouquet()
        {
            var id = Guid.NewGuid();
            var repository = new FakeBouquets
            {
                Existing = new Bouquet { Id = id, Name = "Cinema", SortOrder = 1 },
                NameTaken = true
            };

            var service = new BouquetService(repository, new FakeUnitOfWork());

            var outcome = await service.UpdateAsync(id, new BouquetPatch { Name = "Sport" }, TestContext.Current.CancellationToken);

            Assert.Equal(BouquetAction.AlreadyExists, outcome.Action);
            Assert.Equal(id, outcome.BouquetId);
            Assert.Null(repository.RenamedTo);

            // The comparison is told which bouquet it is comparing against itself, which is what
            // lets a rename to the row's own name in another case pass.
            Assert.Equal(id, repository.ExceptBouquet);
        }

        [Fact]
        public async Task AReorderMustNameEveryBouquetExactlyOnce()
        {
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();

            var repository = new FakeBouquets
            {
                Stored =
                [
                    new Bouquet { Id = first, Name = "First", SortOrder = 1 },
                    new Bouquet { Id = second, Name = "Second", SortOrder = 2 }
                ]
            };

            var service = new BouquetService(repository, new FakeUnitOfWork());

            var outcome = await service.ReorderAsync(
                new ReorderBouquets { OrderedIds = [second, first] }, TestContext.Current.CancellationToken);

            Assert.Equal(BouquetAction.Reordered, outcome.Action);

            // Positions are written 1..N in the order the request named them, not carried over
            // from wherever a row happened to sit: the list in the body is the new order.
            Assert.Equal([1, 2], repository.WrittenPositions);
        }

        [Theory]
        [InlineData(new[] { 0 })]
        [InlineData(new[] { 0, 0 })]
        [InlineData(new[] { 0, 1, 2 })]
        public async Task AReorderOfASubsetADuplicateOrAForeignIdIsRefused(int[] pattern)
        {
            var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

            var repository = new FakeBouquets
            {
                Stored =
                [
                    new Bouquet { Id = ids[0], Name = "First", SortOrder = 1 },
                    new Bouquet { Id = ids[1], Name = "Second", SortOrder = 2 }
                ]
            };

            var service = new BouquetService(repository, new FakeUnitOfWork());

            var outcome = await service.ReorderAsync(
                new ReorderBouquets { OrderedIds = pattern.Select(index => ids[index]).ToList() }, TestContext.Current.CancellationToken);

            Assert.Equal(BouquetAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(ReorderBouquets.OrderedIds)));
            Assert.Empty(repository.WrittenPositions);
        }

        [Fact]
        public async Task ABouquetThatHoldsContentIsNotDeletable()
        {
            var id = Guid.NewGuid();
            var repository = new FakeBouquets
            {
                Existing = new Bouquet { Id = id, Name = "Cinema", SortOrder = 1 },
                ItemCount = 3
            };

            var service = new BouquetService(repository, new FakeUnitOfWork());

            var outcome = await service.DeleteAsync(id, TestContext.Current.CancellationToken);

            Assert.Equal(BouquetAction.NotEmpty, outcome.Action);
            Assert.Equal(3, outcome.ItemCount);
            Assert.False(repository.Removed);
        }

        [Fact]
        public async Task AnEmptyBouquetIsDeletable()
        {
            var id = Guid.NewGuid();
            var repository = new FakeBouquets
            {
                Existing = new Bouquet { Id = id, Name = "Cinema", SortOrder = 1 }
            };

            var service = new BouquetService(repository, new FakeUnitOfWork());

            var outcome = await service.DeleteAsync(id, TestContext.Current.CancellationToken);

            Assert.Equal(BouquetAction.Removed, outcome.Action);
            Assert.True(repository.Removed);
        }

        [Fact]
        public async Task SetContentRefusesDuplicatesAndIdentifiersOfNothing()
        {
            var id = Guid.NewGuid();
            var known = Guid.NewGuid();
            var category = Guid.NewGuid();

            var repository = new FakeBouquets
            {
                Existing = new Bouquet { Id = id, Name = "Cinema", SortOrder = 1 },
                Content = new BouquetContent
                {
                    Members = [],
                    Available = [new BouquetMember { Id = known, Name = "Alpha", CategoryId = category, CategoryName = "Category" }]
                }
            };

            var service = new BouquetService(repository, new FakeUnitOfWork());

            var duplicate = await service.SetContentAsync(
                id,
                new SetBouquetContent
                {
                    ContentType = ContentType.VideoStream,
                    ContentIds = [known, known]
                }, TestContext.Current.CancellationToken);
            Assert.Equal(BouquetAction.Invalid, duplicate.Action);
            Assert.True(duplicate.Errors!.ContainsKey(nameof(SetBouquetContent.ContentIds)));

            var unknown = await service.SetContentAsync(
                id,
                new SetBouquetContent
                {
                    ContentType = ContentType.VideoStream,
                    ContentIds = [Guid.NewGuid()]
                }, TestContext.Current.CancellationToken);
            Assert.Equal(BouquetAction.Invalid, unknown.Action);
            Assert.True(unknown.Errors!.ContainsKey(nameof(SetBouquetContent.ContentIds)));

            Assert.Null(repository.WrittenContent);
        }

        [Fact]
        public async Task SetContentKeepsTheOrderTheRequestNames()
        {
            var id = Guid.NewGuid();
            var alpha = Guid.NewGuid();
            var beta = Guid.NewGuid();
            var category = Guid.NewGuid();

            var repository = new FakeBouquets
            {
                Existing = new Bouquet { Id = id, Name = "Cinema", SortOrder = 1 },
                Content = new BouquetContent
                {
                    Members = [],
                    Available =
                    [
                        new BouquetMember { Id = alpha, Name = "Alpha", CategoryId = category, CategoryName = "Category" },
                        new BouquetMember { Id = beta, Name = "Beta", CategoryId = category, CategoryName = "Category" }
                    ]
                }
            };

            var service = new BouquetService(repository, new FakeUnitOfWork());

            var outcome = await service.SetContentAsync(
                id,
                new SetBouquetContent
                {
                    ContentType = ContentType.VideoStream,
                    ContentIds = [beta, alpha]
                }, TestContext.Current.CancellationToken);
            Assert.Equal(BouquetAction.ContentSet, outcome.Action);
            Assert.Equal([beta, alpha], repository.WrittenContent);
        }

        [Fact]
        public async Task SetContentOnAMissingBouquetIsNotFound()
        {
            var service = new BouquetService(new FakeBouquets(), new FakeUnitOfWork());

            var outcome = await service.SetContentAsync(
                Guid.NewGuid(),
                new SetBouquetContent
                {
                    ContentType = ContentType.VideoStream,
                    ContentIds = []
                }, TestContext.Current.CancellationToken);
            Assert.Equal(BouquetAction.NotFound, outcome.Action);
        }

        /// <summary>The repository as the service sees it, with the few facts a test sets.</summary>
        private sealed class FakeBouquets : IBouquetRepository
        {
            public Bouquet? Existing { get; init; }

            public IReadOnlyList<Bouquet> Stored { get; init; } = [];

            public int LastSortOrder { get; init; }

            public int ItemCount { get; init; }

            public bool NameTaken { get; init; }

            public BouquetContent Content { get; init; } = new() { Members = [], Available = [] };

            public Exception? SaveViolation { get; init; }

            public string? AddedName { get; private set; }

            public int? AddedSortOrder { get; private set; }

            public string? RenamedTo { get; private set; }

            public Guid? ExceptBouquet { get; private set; }

            public List<int> WrittenPositions { get; } = [];

            public IReadOnlyList<Guid>? WrittenContent { get; private set; }

            public bool Removed { get; private set; }

            public Task<IReadOnlyList<BouquetListItem>> GetAllAsync(CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<BouquetListItem>>([]);

            public Task<IReadOnlyList<Bouquet>> GetTrackedAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(Stored);

            public Task<Bouquet?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
                => Task.FromResult(Existing);

            public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(Existing is not null);

            public Task<bool> IsNameTakenAsync(
                string name,
                Guid? exceptBouquet,
                CancellationToken cancellationToken = default)
            {
                ExceptBouquet = exceptBouquet;

                return Task.FromResult(NameTaken);
            }

            public Task<int> GetLastSortOrderAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(LastSortOrder);

            public Task<Guid> AddAsync(
                NewBouquet bouquet,
                int sortOrder,
                CancellationToken cancellationToken = default)
            {
                AddedName = bouquet.Name;
                AddedSortOrder = sortOrder;

                return Task.FromResult(Guid.NewGuid());
            }

            public Task SetNameAsync(Guid id, string name, CancellationToken cancellationToken = default)
            {
                RenamedTo = name;

                return Task.CompletedTask;
            }

            public Task SetSortOrderAsync(Guid id, int sortOrder, CancellationToken cancellationToken = default)
            {
                WrittenPositions.Add(sortOrder);

                return Task.CompletedTask;
            }

            public Task RemoveAsync(Guid id, CancellationToken cancellationToken = default)
            {
                Removed = true;

                return Task.CompletedTask;
            }

            public Task<int> CountItemsAsync(Guid bouquetId, CancellationToken cancellationToken = default)
                => Task.FromResult(ItemCount);

            public Task<BouquetContent> GetContentAsync(
                Guid bouquetId,
                ContentType type,
                CancellationToken cancellationToken = default)
                => Task.FromResult(Content);

            public Task SetContentAsync(
                Guid bouquetId,
                ContentType type,
                IReadOnlyList<Guid> contentIds,
                CancellationToken cancellationToken = default)
            {
                WrittenContent = contentIds;

                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<Guid>> GetItemBouquetIdsAsync(
                ContentType type,
                Guid contentId,
                CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<Guid>>([]);

            public Task SetItemBouquetIdsAsync(
                ContentType type,
                Guid contentId,
                IReadOnlyList<Guid> bouquetIds,
                CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task<IReadOnlySet<Guid>> GetExistingIdsAsync(
                IReadOnlyCollection<Guid> bouquetIds,
                CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlySet<Guid>>(bouquetIds.ToHashSet());
        }

        /// <summary>The unit of work as the service sees it, with one failure to report.</summary>
        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            private readonly Exception? _failure;

            public FakeUnitOfWork(Exception? failure = null) => _failure = failure;

            public Task SaveChangesAsync(CancellationToken cancellationToken = default)
                => _failure is null ? Task.CompletedTask : Task.FromException(_failure);
        }
    }
}

