using Fluxy.Application.Servers.Services;
using Fluxy.Core.Persistence.Exceptions;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Servers.Commands;
using Fluxy.Core.Servers.Enums;
using Fluxy.Core.Servers.Models;
using Fluxy.Core.Servers.ReadModels;
using Fluxy.Core.Servers.Repositories;
using Fluxy.Core.Servers.Utils;

namespace Fluxy.UnitTests.Servers
{
    /// <summary>
    /// The rules the server service owns beyond the pure ones: the uniqueness pre-check, the
    /// write that follows it, the save, and the two refusals that are ordered rather than
    /// incidental - the main row cannot be deleted, and a missing row is missing before it can
    /// be anything else.
    /// </summary>
    /// <remarks>
    /// The repository is faked rather than mocked, the way every other service test here is:
    /// the rules are what is under test, and a fake that answers the questions the service asks
    /// is the smallest thing that can hold them. The index itself is proved by the integration
    /// suite, which is the only place a real unique violation happens.
    /// </remarks>
    public sealed class ServerServiceTests
    {
        [Fact]
        public async Task CreateNormalizesAndStoresTheRowThenCommitsOnce()
        {
            var repository = new FakeServers();
            var unitOfWork = new FakeUnitOfWork();
            var service = new ServerService(repository, unitOfWork);

            var outcome = await service.CreateAsync(
                New("  Edge one  "),
                TestContext.Current.CancellationToken);

            Assert.Equal(ServerAction.Created, outcome.Action);
            Assert.NotNull(repository.Added);
            Assert.Equal("Edge one", repository.Added!.Name);
            Assert.Equal(1, unitOfWork.Saves);
        }

        [Fact]
        public async Task CreateAppliesTheDocumentedDefaultsWhenTheRequestNamesNone()
        {
            var repository = new FakeServers();
            var service = new ServerService(repository, new FakeUnitOfWork());

            await service.CreateAsync(
                new NewServer
                {
                    Name = "Edge one",
                    Ip = "203.0.113.7",
                    Port = 5159,
                    OsName = "Debian 12",
                    NetworkInterface = "eth0"
                },
                TestContext.Current.CancellationToken);

            Assert.NotNull(repository.Added);
            Assert.Equal(0, repository.Added!.ClientCount);
            Assert.Equal(ServerStatus.Active, repository.Added.Status);
            Assert.Equal(0, repository.Added.GuaranteedSpeedKbps);
            Assert.Equal(0, repository.Added.Latency);
            Assert.False(repository.Added.TimeshiftOnly);
            Assert.Empty(repository.Added.CountryCodes);
        }

        [Fact]
        public async Task CreateRefusesAFieldTheRulesRejectBeforeItTouchesTheRepository()
        {
            var repository = new FakeServers();
            var unitOfWork = new FakeUnitOfWork();
            var service = new ServerService(repository, unitOfWork);

            var outcome = await service.CreateAsync(
                New("Edge one") with { Port = 0 },
                TestContext.Current.CancellationToken);

            Assert.Equal(ServerAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewServer.Port)));
            Assert.Null(repository.Added);
            Assert.Equal(0, unitOfWork.Saves);
        }

        [Fact]
        public async Task CreateAnswersATakenNameAsAConflictBeforeTheWrite()
        {
            var repository = new FakeServers { NameTaken = true };
            var unitOfWork = new FakeUnitOfWork();
            var service = new ServerService(repository, unitOfWork);

            var outcome = await service.CreateAsync(
                New("Taken"),
                TestContext.Current.CancellationToken);

            Assert.Equal(ServerAction.AlreadyExists, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewServer.Name)));
            Assert.Null(repository.Added);
            Assert.Equal(0, unitOfWork.Saves);
        }

        [Fact]
        public async Task CreateAnswersARaceOnTheIndexAsATakenName()
        {
            // Two operators racing: the probe passed, the write lost. The answer has to be the
            // same one the probe would have given.
            var service = new ServerService(
                new FakeServers(),
                new FakeUnitOfWork(new UniqueViolationException(
                    ServerLimits.NameIndexName,
                    null)));

            var outcome = await service.CreateAsync(
                New("Edge one"),
                TestContext.Current.CancellationToken);

            Assert.Equal(ServerAction.AlreadyExists, outcome.Action);
        }

        [Fact]
        public async Task CreateRethrowsAViolationFromAnIndexItDoesNotKnow()
        {
            var service = new ServerService(
                new FakeServers(),
                new FakeUnitOfWork(new UniqueViolationException("IX_something_else", null)));

            await Assert.ThrowsAsync<UniqueViolationException>(() => service.CreateAsync(
                New("Edge one"),
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task UpdateOfAMissingRowIsNotFound()
        {
            var service = new ServerService(new FakeServers(), new FakeUnitOfWork());

            var outcome = await service.UpdateAsync(
                Guid.NewGuid(),
                Patch("Anything"),
                TestContext.Current.CancellationToken);

            Assert.Equal(ServerAction.NotFound, outcome.Action);
        }

        [Fact]
        public async Task UpdateWritesEveryFieldAndCommitsOnce()
        {
            var existing = Stored(Guid.NewGuid());
            var repository = new FakeServers { Existing = existing };
            var unitOfWork = new FakeUnitOfWork();
            var service = new ServerService(repository, unitOfWork);

            var outcome = await service.UpdateAsync(
                existing.Id,
                Patch("  Renamed  "),
                TestContext.Current.CancellationToken);

            Assert.Equal(ServerAction.Updated, outcome.Action);
            Assert.NotNull(repository.Written);
            Assert.Equal("Renamed", repository.Written!.Name);
            Assert.Equal(existing.Id, repository.WrittenId);
            Assert.Equal(1, unitOfWork.Saves);
        }

        [Fact]
        public async Task UpdateRefusesAFieldTheRulesReject()
        {
            var existing = Stored(Guid.NewGuid());
            var repository = new FakeServers { Existing = existing };
            var service = new ServerService(repository, new FakeUnitOfWork());

            var outcome = await service.UpdateAsync(
                existing.Id,
                Patch("Renamed") with { CountryCodes = ["NOPE"] },
                TestContext.Current.CancellationToken);

            Assert.Equal(ServerAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(ServerPatch.CountryCodes)));
            Assert.Null(repository.Written);
        }

        [Fact]
        public async Task UpdateOfTheMainRowIsAnOrdinaryWrite()
        {
            var main = Stored(MainServer.Id);
            var repository = new FakeServers { Existing = main };
            var service = new ServerService(repository, new FakeUnitOfWork());

            var outcome = await service.UpdateAsync(
                main.Id,
                Patch("Renamed main"),
                TestContext.Current.CancellationToken);

            Assert.Equal(ServerAction.Updated, outcome.Action);
            Assert.Equal("Renamed main", repository.Written!.Name);
        }

        [Fact]
        public async Task DeleteOfTheMainRowIsRefusedAndTheRowSurvives()
        {
            var main = Stored(MainServer.Id);
            var repository = new FakeServers { Existing = main };
            var unitOfWork = new FakeUnitOfWork();
            var service = new ServerService(repository, unitOfWork);

            var outcome = await service.DeleteAsync(
                main.Id,
                TestContext.Current.CancellationToken);

            Assert.Equal(ServerAction.ImmutableMain, outcome.Action);
            Assert.False(repository.Removed);
            Assert.Equal(0, unitOfWork.Saves);
        }

        [Fact]
        public async Task DeleteOfAnUnknownIdIsNotFoundRatherThanImmutableMain()
        {
            // The main-server rule must not answer for a row that does not exist: the refusal
            // for a missing identifier is the absence, whatever the identifier happens to be.
            var service = new ServerService(new FakeServers(), new FakeUnitOfWork());

            var outcome = await service.DeleteAsync(
                MainServer.Id,
                TestContext.Current.CancellationToken);

            Assert.Equal(ServerAction.NotFound, outcome.Action);
        }

        [Fact]
        public async Task DeleteOfAReferencedRowIsRefusedAndTheRowSurvives()
        {
            var existing = Stored(Guid.NewGuid());
            var repository = new FakeServers { Existing = existing, References = 3 };
            var unitOfWork = new FakeUnitOfWork();
            var service = new ServerService(repository, unitOfWork);

            var outcome = await service.DeleteAsync(
                existing.Id,
                TestContext.Current.CancellationToken);

            Assert.Equal(ServerAction.InUse, outcome.Action);
            Assert.Equal(3, outcome.ReferenceCount);
            Assert.False(repository.Removed);
            Assert.Equal(0, unitOfWork.Saves);
        }

        [Fact]
        public async Task DeleteRefusalsRunInThePinnedOrder()
        {
            // A missing row is missing before it can be the main one or be in use.
            var missing = new ServerService(new FakeServers(), new FakeUnitOfWork());
            Assert.Equal(
                ServerAction.NotFound,
                (await missing.DeleteAsync(MainServer.Id, TestContext.Current.CancellationToken)).Action);

            // The main row is the main row before it can be "in use": its flag dominates the count.
            var main = new ServerService(
                new FakeServers { Existing = Stored(MainServer.Id), References = 7 },
                new FakeUnitOfWork());
            Assert.Equal(
                ServerAction.ImmutableMain,
                (await main.DeleteAsync(MainServer.Id, TestContext.Current.CancellationToken)).Action);

            // Only a row that may otherwise go is refused for being named.
            var existing = Stored(Guid.NewGuid());
            var referenced = new ServerService(
                new FakeServers { Existing = existing, References = 1 },
                new FakeUnitOfWork());
            Assert.Equal(
                ServerAction.InUse,
                (await referenced.DeleteAsync(existing.Id, TestContext.Current.CancellationToken)).Action);
        }

        [Fact]
        public async Task DeleteOfAnOrdinaryRowRemovesItAndCommitsOnce()
        {
            var existing = Stored(Guid.NewGuid());
            var repository = new FakeServers { Existing = existing };
            var unitOfWork = new FakeUnitOfWork();
            var service = new ServerService(repository, unitOfWork);

            var outcome = await service.DeleteAsync(
                existing.Id,
                TestContext.Current.CancellationToken);

            Assert.Equal(ServerAction.Removed, outcome.Action);
            Assert.True(repository.Removed);
            Assert.Equal(1, unitOfWork.Saves);
        }

        [Fact]
        public async Task GetAnswersTheStoredRowAsItsView()
        {
            var existing = Stored(Guid.NewGuid());
            var service = new ServerService(
                new FakeServers { Existing = existing },
                new FakeUnitOfWork());

            var view = await service.GetAsync(existing.Id, TestContext.Current.CancellationToken);

            Assert.NotNull(view);
            Assert.Equal(existing.Id, view.Id);
            Assert.Equal(existing.Name, view.Name);
            Assert.Equal(existing.CountryCodes, view.CountryCodes);
            Assert.Equal(existing.CreatedAt, view.CreatedAt);
        }

        [Fact]
        public async Task GetOfAMissingRowAnswersNull()
        {
            var service = new ServerService(new FakeServers(), new FakeUnitOfWork());

            Assert.Null(await service.GetAsync(
                Guid.NewGuid(),
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task OptionsAreDelegatedToTheRepository()
        {
            var existing = Stored(Guid.NewGuid());
            var service = new ServerService(
                new FakeServers { Existing = existing },
                new FakeUnitOfWork());

            var options = await service.GetOptionsAsync(TestContext.Current.CancellationToken);

            var option = Assert.Single(options);
            Assert.Equal(existing.Id, option.Id);
            Assert.Equal(existing.Name, option.Name);
            Assert.Equal(existing.TimeshiftOnly, option.TimeshiftOnly);
        }

        /// <summary>A stored ordinary row every patch fact starts from.</summary>
        private static Server Stored(Guid id)
        {
            var moment = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

            return new Server
            {
                Id = id,
                Name = "Edge one",
                Ip = "203.0.113.7",
                Port = 5159,
                ClientCount = 1000,
                OsName = "Debian 12",
                NetworkInterface = "eth0",
                Latency = 0,
                Status = ServerStatus.Active,
                GuaranteedSpeedKbps = 0,
                TimeshiftOnly = false,
                Hostname = null,
                VpnIp = null,
                CountryCodes = [],
                IsMain = id == MainServer.Id,
                CreatedAt = moment
            };
        }

        /// <summary>A valid create for a row nobody else holds.</summary>
        private static NewServer New(string name) => new()
        {
            Name = name,
            Ip = "203.0.113.7",
            Port = 5159,
            OsName = "Debian 12",
            NetworkInterface = "eth0"
        };

        /// <summary>A valid full write for a row nobody else holds.</summary>
        private static ServerPatch Patch(string name) => new()
        {
            Name = name,
            Ip = "203.0.113.8",
            Port = 6000,
            ClientCount = 0,
            OsName = "Debian 12",
            NetworkInterface = "eth0",
            Latency = 12.5,
            Status = ServerStatus.Disabled,
            GuaranteedSpeedKbps = 0,
            TimeshiftOnly = false,
            CountryCodes = ["DE"]
        };

        /// <summary>
        /// The repository, answering from what the test declares and recording what the service
        /// asked it to write.
        /// </summary>
        private sealed class FakeServers : IServerRepository
        {
            public Server? Existing { get; init; }

            /// <summary>Whether the probe answers "taken", whatever name is asked about.</summary>
            public bool NameTaken { get; init; }

            public ServerWrite? Added { get; private set; }

            public ServerWrite? Written { get; private set; }

            public Guid WrittenId { get; private set; }

            public bool Removed { get; private set; }

            /// <summary>How many rows point at the server, for the delete guard.</summary>
            public int References { get; init; }

            public Task<ServerReference?> GetReferenceAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => Task.FromResult(
                    Existing is not null && Existing.Id == id
                        ? new ServerReference
                        {
                            Id = Existing.Id,
                            Name = Existing.Name,
                            TimeshiftOnly = Existing.TimeshiftOnly
                        }
                        : null);

            public Task<IReadOnlyList<ServerOption>> GetOptionsAsync(
                CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<ServerOption>>(
                    Existing is null
                        ? []
                        : [
                            new ServerOption
                            {
                                Id = Existing.Id,
                                Name = Existing.Name,
                                TimeshiftOnly = Existing.TimeshiftOnly
                            }
                        ]);

            public Task<int> CountReferencesAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => Task.FromResult(References);

            public Task<ServerPage> GetPageAsync(
                int page,
                int pageSize,
                string? search,
                ServerSortField sortBy,
                ServerSortOrder sortOrder,
                CancellationToken cancellationToken = default)
                => Task.FromResult(new ServerPage
                {
                    Items = [],
                    Total = 0,
                    Page = page,
                    PageSize = pageSize
                });

            public Task<Server?> GetAsync(Guid id, CancellationToken cancellationToken = default)
                => Task.FromResult(
                    Existing is not null && Existing.Id == id && !Removed ? Existing : null);

            public Task<bool> ExistsMainAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(Existing is not null && Existing.Id == MainServer.Id);

            public Task<bool> IsNameTakenAsync(
                string name,
                Guid? exceptServer,
                CancellationToken cancellationToken = default)
                => Task.FromResult(NameTaken);

            public Task<Guid> AddAsync(
                ServerWrite server,
                CancellationToken cancellationToken = default)
            {
                Added = server;

                return Task.FromResult(Guid.NewGuid());
            }

            public Task<bool> UpdateAsync(
                Guid id,
                ServerWrite server,
                CancellationToken cancellationToken = default)
            {
                WrittenId = id;
                Written = server;

                return Task.FromResult(Existing is not null && Existing.Id == id);
            }

            public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
            {
                Removed = Existing is not null && Existing.Id == id;

                return Task.FromResult(Removed);
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
