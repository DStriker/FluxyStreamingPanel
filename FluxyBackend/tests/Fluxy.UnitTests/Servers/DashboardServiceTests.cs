using Fluxy.Application.Servers.Services;
using Fluxy.Core.Servers.Commands;
using Fluxy.Core.Servers.Enums;
using Fluxy.Core.Servers.Interfaces;
using Fluxy.Core.Servers.Models;
using Fluxy.Core.Servers.ReadModels;
using Fluxy.Core.Servers.Repositories;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Repositories;
using Fluxy.Core.Streams.Rows;
using Fluxy.Core.Subscriptions.Repositories;
using Fluxy.Core.Subscriptions.Rows;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Fluxy.UnitTests.Servers
{
    /// <summary>
    /// The dashboard's composition: which grouped read becomes which figure, what an unavailable
    /// server looks like, and the two properties the page depends on - the cache is asked first,
    /// and the probes never exceed the bounded concurrency.
    /// </summary>
    /// <remarks>
    /// The repository fakes answer exactly the questions the service asks, the way the other
    /// service tests here do. The probe fake is concurrent on purpose: the bounded-concurrency
    /// test is only meaningful if the fake would otherwise overlap freely.
    /// </remarks>
    public sealed class DashboardServiceTests
    {
        private static readonly DateTimeOffset Now =
            new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public async Task AServerThatDidNotAnswerIsARowOfZerosAndIsExcludedFromTheNetworkTotal()
        {
            var good = ServerAt("Alpha");
            var bad = ServerAt("Beta");

            var probe = new FakeProbe();
            probe.Samples[good.Id] = Sample(cpu: 10, networkIn: 100, networkOut: 50);
            probe.Samples[bad.Id] = null;

            var service = Build(
                servers: [good, bad],
                serverCounts: [],
                probe: probe);

            var snapshot = await service.GetAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, snapshot.Servers.Count);
            Assert.Equal(1, snapshot.ServersUnavailable);

            var answered = snapshot.Servers.Single(row => row.Id == good.Id);
            Assert.True(answered.Available);
            Assert.Equal(10, answered.CpuPercent);

            var missing = snapshot.Servers.Single(row => row.Id == bad.Id);
            Assert.False(missing.Available);
            Assert.Equal(0, missing.CpuPercent);
            Assert.Equal(0, missing.RamPercent);
            Assert.Equal(0, missing.DiskPercent);
            Assert.Equal(0, missing.UptimeSeconds);
            Assert.Equal(0, missing.NetworkInMbps);
            Assert.Equal(0, missing.NetworkOutMbps);

            // The KPI total covers the server that answered only.
            Assert.Equal(100, snapshot.Kpis.NetworkInMbps);
            Assert.Equal(50, snapshot.Kpis.NetworkOutMbps);
        }

        [Fact]
        public async Task AProbeThatThrowsStillProducesANormalSnapshot()
        {
            var server = ServerAt("Alpha");
            var probe = new FakeProbe { ThrowInstead = true };

            var service = Build(servers: [server], serverCounts: [], probe: probe);

            var snapshot = await service.GetAsync(TestContext.Current.CancellationToken);

            var row = Assert.Single(snapshot.Servers);
            Assert.False(row.Available);
            Assert.Equal(1, snapshot.ServersUnavailable);
        }

        [Fact]
        public async Task TheInstallationTotalsIncludeTheUnattributedGroup()
        {
            var alpha = ServerAt("Alpha");

            var service = Build(
                servers: [alpha],
                serverCounts:
                [
                    new ConnectionLogServerCountRow
                    {
                        ServerId = alpha.Id,
                        ConnectionCount = 3,
                        UserCount = 2
                    },
                    new ConnectionLogServerCountRow
                    {
                        ServerId = null,
                        ConnectionCount = 4,
                        UserCount = 4
                    }
                ],
                liveUsers: 5,
                probe: new FakeProbe());

            var snapshot = await service.GetAsync(TestContext.Current.CancellationToken);

            // Seven live connections, five distinct viewers: the unattributed four count in the
            // installation figures and in no server row.
            Assert.Equal(7, snapshot.Kpis.ActiveConnections);
            Assert.Equal(5, snapshot.Kpis.UsersWithActiveConnections);

            var row = Assert.Single(snapshot.Servers);
            Assert.Equal(3, row.ActiveConnections);
            Assert.Equal(2, row.UsersWithActiveConnections);
        }

        [Fact]
        public async Task TheStreamFiguresComeFromTheTwoStatusGroups()
        {
            var service = Build(
                servers: [],
                serverCounts: [],
                statusCounts:
                [
                    new VideoStreamStatusCountRow { Status = VideoStreamStatus.Running, Count = 7 },
                    new VideoStreamStatusCountRow { Status = VideoStreamStatus.Stopped, Count = 2 }
                ],
                probe: new FakeProbe());

            var snapshot = await service.GetAsync(TestContext.Current.CancellationToken);

            Assert.Equal(7, snapshot.Kpis.StreamsRunning);
            Assert.Equal(2, snapshot.Kpis.StreamsStopped);
        }

        [Fact]
        public async Task AStatusNoStreamHoldsReadsAsZero()
        {
            var service = Build(
                servers: [],
                serverCounts: [],
                statusCounts:
                [
                    new VideoStreamStatusCountRow { Status = VideoStreamStatus.Running, Count = 4 }
                ],
                probe: new FakeProbe());

            var snapshot = await service.GetAsync(TestContext.Current.CancellationToken);

            Assert.Equal(4, snapshot.Kpis.StreamsRunning);
            Assert.Equal(0, snapshot.Kpis.StreamsStopped);
        }

        [Fact]
        public async Task ACacheHitNeitherReadsNorProbes()
        {
            var cached = new DashboardSnapshot
            {
                Kpis = EmptyKpis(),
                Servers = [],
                ServersUnavailable = 0,
                GeneratedAt = Now
            };

            var servers = new FakeServers([ServerAt("Alpha")]);
            var probe = new FakeProbe();
            var cache = new FakeCache(cached);

            var service = new DashboardService(
                servers,
                new FakeConnections([], 0),
                new FakeSubscriptions([]),
                new FakeStreams([]),
                probe,
                cache,
                new FakeTimeProvider(Now),
                NullLogger<DashboardService>.Instance);

            var snapshot = await service.GetAsync(TestContext.Current.CancellationToken);

            Assert.Same(cached, snapshot);
            Assert.Equal(0, servers.Reads);
            Assert.Equal(0, probe.Calls);
        }

        [Fact]
        public async Task SixServersNeverProbeMoreThanFourAtOnce()
        {
            var servers = Enumerable.Range(0, 6).Select(index => ServerAt($"Edge {index}")).ToList();

            var probe = new FakeProbe
            {
                Delay = TimeSpan.FromMilliseconds(50)
            };

            var service = Build(servers: servers, serverCounts: [], probe: probe);

            var snapshot = await service.GetAsync(TestContext.Current.CancellationToken);

            Assert.Equal(6, snapshot.Servers.Count);
            Assert.Equal(6, probe.Calls);
            Assert.True(probe.MaxActive <= 4, $"observed {probe.MaxActive} concurrent probes");
        }

        [Fact]
        public async Task EveryRowIsReturnedIncludingTheDisabledOne()
        {
            var active = ServerAt("Alpha", ServerStatus.Active);
            var disabled = ServerAt("Beta", ServerStatus.Disabled);

            var service = Build(
                servers: [active, disabled],
                serverCounts: [],
                probe: new FakeProbe());

            var snapshot = await service.GetAsync(TestContext.Current.CancellationToken);

            Assert.Equal([active.Id, disabled.Id], snapshot.Servers.Select(row => row.Id));
            Assert.Contains(snapshot.Servers, row => row.Status == ServerStatus.Disabled);
        }

        [Fact]
        public async Task TheGenerationInstantIsTheProvidedClock()
        {
            var service = Build(
                servers: [],
                serverCounts: [],
                probe: new FakeProbe(),
                clock: new FakeTimeProvider(Now));

            var snapshot = await service.GetAsync(TestContext.Current.CancellationToken);

            Assert.Equal(Now, snapshot.GeneratedAt);
        }

        private static DashboardService Build(
            IReadOnlyList<Server>? servers = null,
            IReadOnlyList<ConnectionLogServerCountRow>? serverCounts = null,
            int liveUsers = 0,
            IReadOnlyList<PreferredServerUserCountRow>? preferred = null,
            IReadOnlyList<VideoStreamStatusCountRow>? statusCounts = null,
            FakeProbe? probe = null,
            FakeCache? cache = null,
            FakeTimeProvider? clock = null)
            => new(
                new FakeServers(servers ?? []),
                new FakeConnections(
                    serverCounts ?? [],
                    liveUsers),
                new FakeSubscriptions(preferred ?? []),
                new FakeStreams(statusCounts ?? []),
                probe ?? new FakeProbe(),
                cache ?? new FakeCache(),
                clock ?? new FakeTimeProvider(Now),
                NullLogger<DashboardService>.Instance);

        private static Server ServerAt(string name, ServerStatus status = ServerStatus.Active) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Ip = "203.0.113.7",
            Port = 6200,
            ClientCount = 0,
            OsName = "Debian 12",
            NetworkInterface = "eth0",
            Latency = 0,
            Status = status,
            GuaranteedSpeedKbps = 0,
            TimeshiftOnly = false,
            Hostname = null,
            VpnIp = null,
            CountryCodes = [],
            IsMain = false,
            CreatedAt = Now
        };

        private static ServerStatsSample Sample(double cpu, double networkIn, double networkOut) => new()
        {
            CpuPercent = cpu,
            RamPercent = 20,
            DiskPercent = 30,
            UptimeSeconds = 1000,
            NetworkInMbps = networkIn,
            NetworkOutMbps = networkOut
        };

        private static DashboardKpis EmptyKpis() => new()
        {
            ActiveConnections = 0,
            UsersWithActiveConnections = 0,
            StreamsRunning = 0,
            StreamsStopped = 0,
            NetworkInMbps = 0,
            NetworkOutMbps = 0
        };

        /// <summary>Counts the reads, so a cache hit can prove none happened.</summary>
        private sealed class FakeServers : IServerRepository
        {
            private readonly IReadOnlyList<Server> _servers;

            public FakeServers(IReadOnlyList<Server> servers)
            {
                _servers = servers;
            }

            public int Reads { get; private set; }

            public Task<IReadOnlyList<Server>> GetAllAsync(
                CancellationToken cancellationToken = default)
            {
                Reads++;

                return Task.FromResult(_servers);
            }

            public Task<ServerPage> GetPageAsync(
                int page,
                int pageSize,
                string? search,
                ServerSortField sortBy,
                ServerSortOrder sortOrder,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Server?> GetAsync(Guid id, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<ServerReference?> GetReferenceAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IReadOnlyList<ServerOption>> GetOptionsAsync(
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<int> CountReferencesAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> ExistsMainAsync(CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> IsNameTakenAsync(
                string name,
                Guid? exceptServer,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Guid> AddAsync(ServerWrite server, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> UpdateAsync(
                Guid id,
                ServerWrite server,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        private sealed class FakeConnections : IConnectionLogRepository
        {
            private readonly IReadOnlyList<ConnectionLogServerCountRow> _rows;
            private readonly int _liveUsers;

            public FakeConnections(IReadOnlyList<ConnectionLogServerCountRow> rows, int liveUsers)
            {
                _rows = rows;
                _liveUsers = liveUsers;
            }

            public Task<IReadOnlyList<ConnectionLogServerCountRow>> ReadLiveServerCountsAsync(
                CancellationToken cancellationToken = default)
                => Task.FromResult(_rows);

            public Task<int> CountLiveUsersAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(_liveUsers);

            public Task<Fluxy.Core.Subscriptions.ReadModels.ConnectionLogPage> GetPageAsync(
                int page,
                int pageSize,
                string? search,
                Fluxy.Core.Subscriptions.Enums.ConnectionStreamKind streamKind,
                bool liveOnly,
                Guid? subscriptionId,
                Guid? ownerId,
                Guid? streamId,
                Guid? serverId,
                string? ip,
                string? countryCode,
                string? isp,
                string? userAgent,
                DateTimeOffset? startedFrom,
                DateTimeOffset? startedTo,
                Fluxy.Core.Subscriptions.Enums.ConnectionLogSortField sortBy,
                Fluxy.Core.Subscriptions.Enums.ConnectionLogSortOrder sortOrder,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<int> AddRangeAsync(
                IReadOnlyList<Fluxy.Core.Subscriptions.Commands.NewConnectionLog> logs,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<int> CountAsync(CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        private sealed class FakeSubscriptions : ISubscriptionRepository
        {
            private readonly IReadOnlyList<PreferredServerUserCountRow> _rows;

            public FakeSubscriptions(IReadOnlyList<PreferredServerUserCountRow> rows)
            {
                _rows = rows;
            }

            public Task<IReadOnlyList<PreferredServerUserCountRow>> ReadPreferredServerUserCountsAsync(
                CancellationToken cancellationToken = default)
                => Task.FromResult(_rows);

            public Task<Fluxy.Core.Subscriptions.ReadModels.SubscriptionPage> GetPageAsync(
                int page,
                int pageSize,
                string? search,
                Guid? ownerId,
                Guid? creatorId,
                Fluxy.Core.Subscriptions.Enums.SubscriptionStatus? status,
                Fluxy.Core.Subscriptions.Enums.SubscriptionSortField sortBy,
                Fluxy.Core.Subscriptions.Enums.SubscriptionSortOrder sortOrder,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Fluxy.Core.Subscriptions.ReadModels.SubscriptionDetail?> GetAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Fluxy.Core.Subscriptions.Models.Subscription?> FindForUpdateAsync(
                Guid id,
                CancellationToken cancellationToken = default)
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

            public Task<Fluxy.Core.Subscriptions.Rows.SubscriptionOwnerContext?> GetOwnerContextAsync(
                Guid userId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Fluxy.Core.Subscriptions.Rows.SubscriptionPlanContext?> GetPlanContextAsync(
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
                Fluxy.Core.Subscriptions.Commands.NewSubscription subscription,
                IReadOnlyList<Guid> bouquetIds,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> UpdateAsync(
                Guid id,
                Fluxy.Core.Subscriptions.Models.Subscription subscription,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task ReplaceBouquetsAsync(
                Guid subscriptionId,
                IReadOnlyList<Guid> bouquetIds,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<int> CountForPlanAsync(
                Guid planId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        private sealed class FakeStreams : IVideoStreamRepository
        {
            private readonly IReadOnlyList<VideoStreamStatusCountRow> _rows;

            public FakeStreams(IReadOnlyList<VideoStreamStatusCountRow> rows)
            {
                _rows = rows;
            }

            public Task<IReadOnlyList<VideoStreamStatusCountRow>> ReadStatusCountsAsync(
                CancellationToken cancellationToken = default)
                => Task.FromResult(_rows);

            public Task<Fluxy.Core.Streams.ReadModels.VideoStreamPage> GetPageAsync(
                int page,
                int pageSize,
                string? search,
                Guid? categoryId,
                VideoStreamStatus? status,
                VideoStreamType? type,
                Fluxy.Core.Streams.Enums.VideoStreamSortField sortBy,
                Fluxy.Core.Streams.Enums.VideoStreamSortOrder sortOrder,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Fluxy.Core.Streams.Models.VideoStream?> GetAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Fluxy.Core.Streams.Models.VideoStream?> FindForUpdateAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> CategoryIsUsableAsync(
                Guid categoryId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> IsNameTakenAsync(
                string name,
                Guid categoryId,
                Guid? exceptStream,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IReadOnlyList<Fluxy.Core.Streams.Rows.VideoStreamImportRow>> GetImportRowsAsync(
                Guid categoryId,
                IReadOnlyCollection<string> names,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task UpdateRangeAsync(
                IReadOnlyList<Fluxy.Core.Streams.Commands.VideoStreamBatchUpdate> updates,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Guid> AddAsync(
                Fluxy.Core.Streams.Commands.VideoStreamWrite stream,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IReadOnlyList<Guid>> AddRangeAsync(
                IReadOnlyList<Fluxy.Core.Streams.Commands.VideoStreamWrite> streams,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> UpdateAsync(
                Guid id,
                Fluxy.Core.Streams.Commands.VideoStreamWrite stream,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<VideoStreamStatus?> SetStatusAsync(
                Guid id,
                VideoStreamStatus status,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        /// <summary>
        /// Answers a declared sample per server, counts calls and optionally overlaps on purpose.
        /// </summary>
        /// <remarks>
        /// <b>Both counters are updated atomically, because the probes really do overlap.</b> The
        /// dashboard calls this fake from up to four tasks at once, so <c>_calls++</c> would lose
        /// increments and <c>Math.Max</c> over a field would lose the peak - the second failure
        /// being the dangerous one, because a lost peak makes the concurrency cap look respected
        /// when it is not. The CAS loop is the shape
        /// <c>FakeServerStatsProbe</c> in the integration suite already uses.
        /// </remarks>
        private sealed class FakeProbe : IServerStatsProbe
        {
            private int _active;
            private int _calls;
            private int _maxActive;

            public Dictionary<Guid, ServerStatsSample?> Samples { get; } = new();

            public bool ThrowInstead { get; init; }

            public TimeSpan Delay { get; init; }

            public int Calls => _calls;

            public int MaxActive => _maxActive;

            public async Task<ServerStatsSample?> ProbeAsync(
                Server server,
                CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _calls);

                var active = Interlocked.Increment(ref _active);

                int observed;
                do
                {
                    observed = _maxActive;
                }
                while (active > observed
                    && Interlocked.CompareExchange(ref _maxActive, active, observed) != observed);

                try
                {
                    if (Delay > TimeSpan.Zero)
                    {
                        await Task.Delay(Delay, cancellationToken);
                    }

                    if (ThrowInstead)
                    {
                        throw new HttpRequestException("The server did not answer.");
                    }

                    return Samples.TryGetValue(server.Id, out var sample) ? sample : null;
                }
                finally
                {
                    Interlocked.Decrement(ref _active);
                }
            }
        }

        /// <summary>A cache that answers a declared snapshot and builds nothing.</summary>
        private sealed class FakeCache : IServerStatsCache
        {
            private readonly DashboardSnapshot? _cached;

            public FakeCache(DashboardSnapshot? cached = null)
            {
                _cached = cached;
            }

            public async Task<DashboardSnapshot> GetOrAddAsync(
                Func<CancellationToken, Task<DashboardSnapshot>> build,
                CancellationToken cancellationToken = default)
                => _cached ?? await build(cancellationToken);
        }
    }
}
