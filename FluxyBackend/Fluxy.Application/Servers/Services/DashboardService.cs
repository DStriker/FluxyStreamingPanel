using Fluxy.Core.Servers.Interfaces;
using Fluxy.Core.Servers.Models;
using Fluxy.Core.Servers.ReadModels;
using Fluxy.Core.Servers.Repositories;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Repositories;
using Fluxy.Core.Subscriptions.Repositories;
using Microsoft.Extensions.Logging;

namespace Fluxy.Application.Servers.Services
{
    /// <summary>
    /// Builds the admin dashboard: installation KPIs plus one row per server, with the live
    /// telemetry probed rather than stored.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Five reads and one probe per server, never an N+1.</b> The servers and the four grouped
    /// aggregates are five queries against the database; the telemetry is one HTTP probe per
    /// server, run with a bounded degree of concurrency because a table of twenty servers must not
    /// open twenty sockets at once, and a screen that refreshes must not wait twenty times two
    /// seconds.
    /// </para>
    /// <para>
    /// <b>A server that did not answer is still a row.</b> A null or thrown probe leaves the row in
    /// place with <see cref="DashboardServer.Available"/> false and zeros in every numeric field.
    /// The KPI network totals sum the available servers only, and
    /// <see cref="DashboardSnapshot.ServersUnavailable"/> carries how many did not answer, so a
    /// partial total can be labelled as partial.
    /// </para>
    /// <para>
    /// <b>Attribution is by the real <c>server_id</c> only.</b> Unattributed live connections count
    /// in <see cref="DashboardKpis.ActiveConnections"/> and in
    /// <see cref="DashboardKpis.UsersWithActiveConnections"/>, and contribute to no server row.
    /// </para>
    /// </remarks>
    public sealed class DashboardService : IDashboardService
    {
        /// <summary>How many server probes may be in flight at once.</summary>
        private const int ProbeConcurrency = 4;

        private readonly IServerRepository _servers;
        private readonly IConnectionLogRepository _connections;
        private readonly ISubscriptionRepository _subscriptions;
        private readonly IVideoStreamRepository _streams;
        private readonly IServerStatsProbe _probe;
        private readonly IServerStatsCache _cache;
        private readonly TimeProvider _time;
        private readonly ILogger<DashboardService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DashboardService"/> class.
        /// </summary>
        /// <param name="servers">Repository every server row is read from.</param>
        /// <param name="connections">Repository the live connection groups are read from.</param>
        /// <param name="subscriptions">Repository the preferred-server groups are read from.</param>
        /// <param name="streams">Repository the stream status groups are read from.</param>
        /// <param name="probe">Probe that reads each server's machine.</param>
        /// <param name="cache">Cache the built snapshot is published through.</param>
        /// <param name="time">Clock the snapshot's generation instant is taken from.</param>
        /// <param name="logger">Logger the unusable servers are reported to.</param>
        public DashboardService(
            IServerRepository servers,
            IConnectionLogRepository connections,
            ISubscriptionRepository subscriptions,
            IVideoStreamRepository streams,
            IServerStatsProbe probe,
            IServerStatsCache cache,
            TimeProvider time,
            ILogger<DashboardService> logger)
        {
            _servers = servers;
            _connections = connections;
            _subscriptions = subscriptions;
            _streams = streams;
            _probe = probe;
            _cache = cache;
            _time = time;
            _logger = logger;
        }

        /// <inheritdoc />
        public Task<DashboardSnapshot> GetAsync(CancellationToken cancellationToken = default)
            // The cache decides whether this call builds; the single-flight gate inside it is what
            // keeps two simultaneous misses from building twice.
            => _cache.GetOrAddAsync(BuildAsync, cancellationToken);

        /// <summary>
        /// Reads the five aggregates, probes every server and composes the snapshot.
        /// </summary>
        /// <remarks>
        /// The database reads are sequential on purpose: they are five statements on one scoped
        /// context, and a context is not safe for concurrent commands. Only the probes leave the
        /// process, so only the probes run concurrently.
        /// </remarks>
        private async Task<DashboardSnapshot> BuildAsync(CancellationToken cancellationToken)
        {
            var servers = await _servers.GetAllAsync(cancellationToken);
            var serverCounts = await _connections.ReadLiveServerCountsAsync(cancellationToken);
            var liveUsers = await _connections.CountLiveUsersAsync(cancellationToken);
            var preferredUsers = await _subscriptions.ReadPreferredServerUserCountsAsync(cancellationToken);
            var statusCounts = await _streams.ReadStatusCountsAsync(cancellationToken);

            var samples = await ProbeAllAsync(servers, cancellationToken);

            var connectionCounts = serverCounts
                .Where(row => row.ServerId is not null)
                .ToDictionary(row => row.ServerId!.Value, row => row.ConnectionCount);

            var perServerUsers = serverCounts
                .Where(row => row.ServerId is not null)
                .ToDictionary(row => row.ServerId!.Value, row => row.UserCount);

            var preferredCounts = preferredUsers
                .ToDictionary(row => row.ServerId, row => row.UserCount);

            var rows = new List<DashboardServer>(servers.Count);

            foreach (var server in servers)
            {
                var sample = samples[server.Id];

                rows.Add(new DashboardServer
                {
                    Id = server.Id,
                    Name = server.Name,
                    Status = server.Status,
                    Available = sample is not null,
                    ActiveConnections = connectionCounts.GetValueOrDefault(server.Id),
                    PreferredServerUsers = preferredCounts.GetValueOrDefault(server.Id),
                    UsersWithActiveConnections = perServerUsers.GetValueOrDefault(server.Id),
                    UptimeSeconds = sample?.UptimeSeconds ?? 0,
                    CpuPercent = sample?.CpuPercent ?? 0,
                    RamPercent = sample?.RamPercent ?? 0,
                    DiskPercent = sample?.DiskPercent ?? 0,
                    NetworkInMbps = sample?.NetworkInMbps ?? 0,
                    NetworkOutMbps = sample?.NetworkOutMbps ?? 0
                });
            }

            var available = rows.Where(row => row.Available).ToList();

            return new DashboardSnapshot
            {
                Kpis = new DashboardKpis
                {
                    // Every live connection, including the unattributed group: the installation
                    // figure describes the log, not the servers that happen to be attributed.
                    ActiveConnections = serverCounts.Sum(row => row.ConnectionCount),
                    UsersWithActiveConnections = liveUsers,
                    StreamsRunning = statusCounts
                        .Where(row => row.Status == VideoStreamStatus.Running)
                        .Sum(row => row.Count),
                    StreamsStopped = statusCounts
                        .Where(row => row.Status == VideoStreamStatus.Stopped)
                        .Sum(row => row.Count),

                    // Available servers only - the unavailable count tells the client the total
                    // is partial.
                    NetworkInMbps = available.Sum(row => row.NetworkInMbps),
                    NetworkOutMbps = available.Sum(row => row.NetworkOutMbps)
                },
                Servers = rows,
                ServersUnavailable = rows.Count(row => !row.Available),
                GeneratedAt = _time.GetUtcNow()
            };
        }

        /// <summary>
        /// Probes every server with at most <see cref="ProbeConcurrency"/> requests in flight.
        /// </summary>
        /// <param name="servers">Servers to probe.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>One sample per server identifier; null for a server that did not answer.</returns>
        /// <remarks>
        /// A probe that throws is caught here rather than allowed to fail the snapshot: one
        /// unreachable machine must not take the whole dashboard with it. The exception is logged
        /// with the server's identity so the operator can find the machine that stopped reporting.
        /// </remarks>
        private async Task<IReadOnlyDictionary<Guid, ServerStatsSample?>> ProbeAllAsync(
            IReadOnlyList<Server> servers,
            CancellationToken cancellationToken)
        {
            var results = new Dictionary<Guid, ServerStatsSample?>(servers.Count);

            if (servers.Count == 0)
            {
                return results;
            }

            using var gate = new SemaphoreSlim(ProbeConcurrency);

            var tasks = servers.Select(async server =>
            {
                await gate.WaitAsync(cancellationToken);

                try
                {
                    return (server.Id, Sample: await SafeProbeAsync(server, cancellationToken));
                }
                finally
                {
                    gate.Release();
                }
            });

            foreach (var (id, sample) in await Task.WhenAll(tasks))
            {
                results[id] = sample;
            }

            return results;
        }

        /// <summary>Probes one server, turning any failure into "unavailable".</summary>
        private async Task<ServerStatsSample?> SafeProbeAsync(
            Server server,
            CancellationToken cancellationToken)
        {
            try
            {
                return await _probe.ProbeAsync(server, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "The statistics probe of server {ServerId} ('{ServerName}') threw, so the " +
                    "server is reported as unavailable.",
                    server.Id,
                    server.Name);

                return null;
            }
        }
    }
}
