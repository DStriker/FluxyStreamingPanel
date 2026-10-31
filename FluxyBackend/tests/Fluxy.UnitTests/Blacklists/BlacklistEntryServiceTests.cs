using Fluxy.Application.Blacklists.Services;
using Fluxy.Application.Blacklists.Utils;
using Fluxy.Core.Blacklists.Commands;
using Fluxy.Core.Blacklists.Enums;
using Fluxy.Core.Blacklists.Models;
using Fluxy.Core.Blacklists.Outcomes;
using Fluxy.Core.Blacklists.ReadModels;
using Fluxy.Core.Blacklists.Repositories;
using Fluxy.Core.Persistence.Exceptions;
using Fluxy.Core.Persistence.Interfaces;

namespace Fluxy.UnitTests.Blacklists
{
    /// <summary>
    /// The rules the blacklist service owns beyond the pure ones: the value trimmed before it is
    /// judged, uniqueness scoped to one list, a patch merged into the row before it is validated,
    /// and the counter and the added date never written.
    /// </summary>
    /// <remarks>
    /// The repository is faked rather than mocked, the way every other service test here is:
    /// the rules are what is under test, and a fake that answers the questions the service asks
    /// is the smallest thing that can hold them. The index itself is proved by the integration
    /// suite.
    /// </remarks>
    public sealed class BlacklistEntryServiceTests
    {
        [Fact]
        public async Task CreateTrimsTheValueAndStoresTheNote()
        {
            var repository = new FakeEntries();
            var service = new BlacklistEntryService(repository, new FakeUnitOfWork());

            var outcome = await service.CreateAsync(
                new NewBlacklistEntry
                {
                    Type = BlacklistType.Isp,
                    Value = "  Example Telecom  ",
                    Note = "  abusive network  "
                },
                TestContext.Current.CancellationToken);

            Assert.Equal(BlacklistEntryAction.Created, outcome.Action);
            Assert.Equal("Example Telecom", repository.Added!.Value);
            Assert.Equal("abusive network", repository.Added.Note);
            Assert.Equal(BlacklistType.Isp, repository.AddedType);
        }

        [Fact]
        public async Task CreateStoresNoNoteWhenTheRequestNamesNone()
        {
            var repository = new FakeEntries();
            var service = new BlacklistEntryService(repository, new FakeUnitOfWork());

            await service.CreateAsync(
                new NewBlacklistEntry
                {
                    Type = BlacklistType.UserAgent,
                    Value = "curl/8.0",
                    Note = "   "
                },
                TestContext.Current.CancellationToken);

            Assert.Null(repository.Added!.Note);
        }

        [Theory]
        [InlineData(BlacklistType.Ip, "")]
        [InlineData(BlacklistType.Ip, "   ")]
        [InlineData(BlacklistType.Ip, "300.1.2.3")]
        [InlineData(BlacklistType.Ip, "203.0.113.0/24")]
        public async Task CreateRefusesAValueTheListCannotHold(BlacklistType type, string value)
        {
            var repository = new FakeEntries();
            var service = new BlacklistEntryService(repository, new FakeUnitOfWork());

            var outcome = await service.CreateAsync(
                new NewBlacklistEntry { Type = type, Value = value },
                TestContext.Current.CancellationToken);

            Assert.Equal(BlacklistEntryAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewBlacklistEntry.Value)));
            Assert.Null(repository.Added);
        }

        [Fact]
        public async Task CreateRefusesAnOversizedNote()
        {
            var repository = new FakeEntries();
            var service = new BlacklistEntryService(repository, new FakeUnitOfWork());

            var outcome = await service.CreateAsync(
                new NewBlacklistEntry
                {
                    Type = BlacklistType.Isp,
                    Value = "Example Telecom",
                    Note = new string('a', 1001)
                },
                TestContext.Current.CancellationToken);

            Assert.Equal(BlacklistEntryAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewBlacklistEntry.Note)));
            Assert.Null(repository.Added);
        }

        [Fact]
        public async Task CreateRefusesAValueTheSameListAlreadyHolds()
        {
            var repository = new FakeEntries { ValueTaken = true };
            var service = new BlacklistEntryService(repository, new FakeUnitOfWork());

            var outcome = await service.CreateAsync(
                new NewBlacklistEntry { Type = BlacklistType.Ip, Value = "203.0.113.5" },
                TestContext.Current.CancellationToken);

            Assert.Equal(BlacklistEntryAction.AlreadyExists, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewBlacklistEntry.Value)));
            Assert.Null(repository.Added);
        }

        [Fact]
        public async Task CreateProbesUniquenessAgainstItsOwnListOnly()
        {
            // The same text is already an IP entry. An ISP create must not be refused for it.
            var repository = new FakeEntries();
            var service = new BlacklistEntryService(repository, new FakeUnitOfWork());

            var outcome = await service.CreateAsync(
                new NewBlacklistEntry { Type = BlacklistType.Isp, Value = "203.0.113.5" },
                TestContext.Current.CancellationToken);

            Assert.Equal(BlacklistEntryAction.Created, outcome.Action);
            Assert.Equal(BlacklistType.Isp, repository.ProbedType);
        }

        [Fact]
        public async Task CreateAnswersARaceOnTheIndexAsATakenValue()
        {
            // Two operators racing: the probe passed, the write lost. The answer has to be the
            // same one the probe would have given.
            var repository = new FakeEntries();
            var service = new BlacklistEntryService(
                repository,
                new FakeUnitOfWork(new UniqueViolationException(
                    BlacklistEntryRules.TypeValueIndexName,
                    null)));

            var outcome = await service.CreateAsync(
                new NewBlacklistEntry { Type = BlacklistType.Ip, Value = "203.0.113.5" },
                TestContext.Current.CancellationToken);

            Assert.Equal(BlacklistEntryAction.AlreadyExists, outcome.Action);
        }

        [Fact]
        public async Task CreateRethrowsAViolationFromAnIndexItDoesNotKnow()
        {
            var service = new BlacklistEntryService(
                new FakeEntries(),
                new FakeUnitOfWork(new UniqueViolationException("IX_something_else", null)));

            await Assert.ThrowsAsync<UniqueViolationException>(() => service.CreateAsync(
                new NewBlacklistEntry { Type = BlacklistType.Ip, Value = "203.0.113.5" },
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task UpdateMergesThePatchAndKeepsTheTypeAndCounter()
        {
            var existing = Stored();
            var repository = new FakeEntries { Existing = existing };
            var service = new BlacklistEntryService(repository, new FakeUnitOfWork());

            var outcome = await service.UpdateAsync(
                existing.Id,
                new BlacklistEntryPatch { Value = "  203.0.113.6  ", Note = "  why  " },
                TestContext.Current.CancellationToken);

            Assert.Equal(BlacklistEntryAction.Updated, outcome.Action);
            Assert.Equal("203.0.113.6", repository.Written!.Value);
            Assert.Equal("why", repository.Written.Note);
            Assert.Equal(existing.Type, repository.ProbedType);
        }

        [Fact]
        public async Task UpdateKeepsTheStoredNoteWhenTheRequestNamesNone()
        {
            var existing = Stored();
            var repository = new FakeEntries { Existing = existing };
            var service = new BlacklistEntryService(repository, new FakeUnitOfWork());

            await service.UpdateAsync(
                existing.Id,
                new BlacklistEntryPatch { Value = "203.0.113.7" },
                TestContext.Current.CancellationToken);

            Assert.Equal("kept", repository.Written!.Note);
        }

        [Fact]
        public async Task UpdateClearsTheNoteWhenTheRequestSendsAnEmptyOne()
        {
            var existing = Stored();
            var repository = new FakeEntries { Existing = existing };
            var service = new BlacklistEntryService(repository, new FakeUnitOfWork());

            await service.UpdateAsync(
                existing.Id,
                new BlacklistEntryPatch { Note = "   " },
                TestContext.Current.CancellationToken);

            Assert.Null(repository.Written!.Note);

            // The value the request did not name stays what the row already held.
            Assert.Equal(existing.Value, repository.Written.Value);
        }

        [Fact]
        public async Task UpdateRefusesAnIpValueThatIsNotAnAddress()
        {
            var existing = Stored();
            var repository = new FakeEntries { Existing = existing };
            var service = new BlacklistEntryService(repository, new FakeUnitOfWork());

            var outcome = await service.UpdateAsync(
                existing.Id,
                new BlacklistEntryPatch { Value = "300.1.2.3" },
                TestContext.Current.CancellationToken);

            Assert.Equal(BlacklistEntryAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(BlacklistEntryPatch.Value)));
            Assert.Null(repository.Written);
        }

        [Fact]
        public async Task UpdateRefusesAValueAnotherEntryOfTheListHolds()
        {
            var existing = Stored();
            var repository = new FakeEntries { Existing = existing, ValueTaken = true };
            var service = new BlacklistEntryService(repository, new FakeUnitOfWork());

            var outcome = await service.UpdateAsync(
                existing.Id,
                new BlacklistEntryPatch { Value = "203.0.113.6" },
                TestContext.Current.CancellationToken);

            Assert.Equal(BlacklistEntryAction.AlreadyExists, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(BlacklistEntryPatch.Value)));
            Assert.Null(repository.Written);
        }

        [Fact]
        public async Task UpdateOfAMissingEntryIsNotFound()
        {
            var service = new BlacklistEntryService(new FakeEntries(), new FakeUnitOfWork());

            var outcome = await service.UpdateAsync(
                Guid.NewGuid(),
                new BlacklistEntryPatch { Value = "203.0.113.5" },
                TestContext.Current.CancellationToken);

            Assert.Equal(BlacklistEntryAction.NotFound, outcome.Action);
        }

        [Fact]
        public async Task DeleteAnswersRemovedThenNotFound()
        {
            var existing = Stored();
            var repository = new FakeEntries { Existing = existing };
            var service = new BlacklistEntryService(repository, new FakeUnitOfWork());

            var removed = await service.DeleteAsync(
                existing.Id,
                TestContext.Current.CancellationToken);

            Assert.Equal(BlacklistEntryAction.Removed, removed.Action);

            var missing = await service.DeleteAsync(
                existing.Id,
                TestContext.Current.CancellationToken);

            Assert.Equal(BlacklistEntryAction.NotFound, missing.Action);
        }

        [Fact]
        public async Task ClearEmptiesTheListItIsGiven()
        {
            var repository = new FakeEntries { ClearedCount = 3 };
            var service = new BlacklistEntryService(repository, new FakeUnitOfWork());

            var outcome = await service.ClearAsync(
                BlacklistType.UserAgent,
                TestContext.Current.CancellationToken);

            Assert.Equal(BlacklistEntryAction.Cleared, outcome.Action);

            // The command names a list, so the outcome names no single entry.
            Assert.Equal(Guid.Empty, outcome.EntryId);

            // And the list it was given is the list that was cleared - not the IP default.
            Assert.Equal(BlacklistType.UserAgent, repository.ClearedType);
        }

        [Fact]
        public async Task ClearOfAnEmptyListIsStillASuccess()
        {
            // The command is already satisfied, and answering a refusal would make the button
            // report a failure for having worked.
            var repository = new FakeEntries { ClearedCount = 0 };
            var service = new BlacklistEntryService(repository, new FakeUnitOfWork());

            var outcome = await service.ClearAsync(
                BlacklistType.Isp,
                TestContext.Current.CancellationToken);

            Assert.Equal(BlacklistEntryAction.Cleared, outcome.Action);
        }

        [Fact]
        public async Task GetAnswersTheStoredRowAsItsView()
        {
            var existing = Stored();
            var service = new BlacklistEntryService(
                new FakeEntries { Existing = existing },
                new FakeUnitOfWork());

            var view = await service.GetAsync(existing.Id, TestContext.Current.CancellationToken);

            Assert.NotNull(view);
            Assert.Equal(existing.Id, view.Id);
            Assert.Equal(existing.Value, view.Value);
            Assert.Equal(existing.BlockedAttempts, view.BlockedAttempts);
            Assert.Equal(existing.CreatedAt, view.CreatedAt);
        }

        [Fact]
        public async Task UpdateOfARowDeletedAfterTheReadIsNotFound()
        {
            // The row is there when the service reads it, and gone by the time the write runs -
            // another operator deleted it in between. The repository reports that as false, and
            // the answer has to be the same 404 the same id's GET gives, rather than a 200 for
            // a record that no longer exists.
            var existing = Stored();
            var repository = new FakeEntries { Existing = existing, VanishedBeforeWrite = true };
            var unitOfWork = new FakeUnitOfWork();
            var service = new BlacklistEntryService(repository, unitOfWork);

            var outcome = await service.UpdateAsync(
                existing.Id,
                new BlacklistEntryPatch { Value = "203.0.113.6" },
                TestContext.Current.CancellationToken);

            Assert.Equal(BlacklistEntryAction.NotFound, outcome.Action);

            // And nothing was committed: an empty save would be a write with no row behind it.
            Assert.Equal(0, unitOfWork.Saves);
        }

        /// <summary>A stored IP entry with a note the patch tests can keep or clear.</summary>
        private static BlacklistEntry Stored()
        {
            var moment = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

            return new BlacklistEntry
            {
                Id = Guid.NewGuid(),
                Type = BlacklistType.Ip,
                Value = "203.0.113.5",
                BlockedAttempts = 12,
                Note = "kept",
                CreatedAt = moment,
                UpdatedAt = moment
            };
        }

        /// <summary>
        /// The repository, answering from what the test declares and recording what the service
        /// asked it to write.
        /// </summary>
        private sealed class FakeEntries : IBlacklistEntryRepository
        {
            public BlacklistEntry? Existing { get; init; }

            /// <summary>Whether the probe answers "taken", whatever list is asked about.</summary>
            public bool ValueTaken { get; init; }

            /// <summary>
            /// Whether the row disappears between the read the service performs and the write
            /// that follows it. The read still answers the entry; the write answers false.
            /// </summary>
            public bool VanishedBeforeWrite { get; init; }

            public BlacklistEntryWrite? Added { get; private set; }

            public BlacklistType AddedType { get; private set; }

            public BlacklistEntryWrite? Written { get; private set; }

            /// <summary>The list the uniqueness probe was scoped to.</summary>
            public BlacklistType? ProbedType { get; private set; }

            private bool _removed;

            public Task<BlacklistEntryPage> GetPageAsync(
                BlacklistType type,
                int page,
                int pageSize,
                string? search,
                BlacklistSortField sortBy,
                BlacklistSortOrder sortOrder,
                CancellationToken cancellationToken = default)
                => Task.FromResult(new BlacklistEntryPage
                {
                    Items = [],
                    Total = 0,
                    Page = page,
                    PageSize = pageSize
                });

            public Task<BlacklistEntry?> GetAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => Task.FromResult(
                    Existing is not null && Existing.Id == id && !_removed ? Existing : null);

            public Task<bool> IsValueTakenAsync(
                BlacklistType type,
                string value,
                Guid? exceptEntry,
                CancellationToken cancellationToken = default)
            {
                ProbedType = type;

                return Task.FromResult(ValueTaken);
            }

            public Task<Guid> AddAsync(
                BlacklistType type,
                BlacklistEntryWrite entry,
                CancellationToken cancellationToken = default)
            {
                AddedType = type;
                Added = entry;

                return Task.FromResult(Guid.NewGuid());
            }

            public Task<bool> UpdateAsync(
                Guid id,
                BlacklistEntryWrite entry,
                CancellationToken cancellationToken = default)
            {
                Written = entry;

                return Task.FromResult(
                    !VanishedBeforeWrite && Existing is not null && Existing.Id == id);
            }

            public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
            {
                var removed = Existing is not null && Existing.Id == id && !_removed;
                _removed |= removed;

                return Task.FromResult(removed);
            }

            /// <summary>The list the clear-all was scoped to, and how many rows it reports.</summary>
            public BlacklistType? ClearedType { get; private set; }

            public int ClearedCount { get; init; }

            public Task<int> RemoveAllAsync(
                BlacklistType type,
                CancellationToken cancellationToken = default)
            {
                ClearedType = type;

                return Task.FromResult(ClearedCount);
            }
        }

        /// <summary>The unit of work, either committing or failing with what the test declares.</summary>
        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            private readonly Exception? _failure;

            public FakeUnitOfWork(Exception? failure = null) => _failure = failure;

            /// <summary>How many times the service asked for a commit.</summary>
            public int Saves { get; private set; }

            public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                Saves += 1;

                return _failure is null ? Task.CompletedTask : Task.FromException(_failure);
            }
        }
    }
}
