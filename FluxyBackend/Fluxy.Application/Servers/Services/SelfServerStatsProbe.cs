using Fluxy.Application.Servers.Utils;
using Fluxy.Core.Servers.Interfaces;
using Fluxy.Core.Servers.Models;
using Fluxy.Core.Servers.ReadModels;
using Fluxy.Core.Servers.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fluxy.Application.Servers.Services
{
    /// <summary>
    /// Answers the installation's main server from inside the process, and every other server over
    /// HTTP through <see cref="HttpServerStatsProbe"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The main row describes this process, so measuring it remotely was the wrong question.</b>
    /// Its configured address is an editable starting point for clients rather than a socket the
    /// statistics live behind, and no server on this installation exposes the assumed
    /// <c>/api/stats</c> route yet - so the main row answered "unavailable" on every fresh
    /// installation, including the machine that was serving the request. Straightening that is the
    /// whole reason this decorator exists.
    /// </para>
    /// <para>
    /// <b>The rule is the identifier, never the stored flag.</b>
    /// <see cref="MainServer.IsMain(Guid)"/> is asked, so a row edited by hand cannot change which
    /// server is answered locally, and a row renamed or marked disabled is still the main one.
    /// </para>
    /// <para>
    /// <b>A failed local read falls through to the remote probe, and never fabricates.</b> If the
    /// sampler cannot measure the process - a platform refused a fact, a counter reset, or the
    /// caller cancelled - the decorator asks the delegate. That delegate answers null for a main
    /// row in today's installation, so the row is reported unavailable with zeros: the same shape
    /// as any other unreadable server, not a zero that claims an idle machine. A cold start is not
    /// such a case: the sampler measures its own short window, so the first request after a
    /// restart is already answered.
    /// </para>
    /// <para>
    /// <b>Nothing above this type changes.</b> <see cref="DashboardService"/> still probes every
    /// row through <see cref="IServerStatsProbe"/>, the wire contract still carries the same six
    /// numbers, and the disable flag is still the operator's: a main row may be
    /// <see cref="ServerStatus.Disabled"/> and <c>available: true</c> at the same time, because
    /// those are two different facts.
    /// </para>
    /// </remarks>
    public sealed class SelfServerStatsProbe : IServerStatsProbe
    {
        /// <summary>
        /// The key the remote probe is registered under, so this decorator can ask for it
        /// without naming itself and without a cycle.
        /// </summary>
        /// <remarks>
        /// <see cref="IServerStatsProbe"/> has exactly one ordinary registration - this type - so
        /// injecting <see cref="IServerStatsProbe"/> into it would ask the container to build this
        /// type from itself. The remote lives under this key instead, which is also what lets a
        /// test replace the remote delegate while keeping the decorator in place.
        /// </remarks>
        public const string RemoteKey = "Servers.HttpServerStatsProbe";

        private readonly IServerStatsProbe _remote;
        private readonly SelfServerStatsSampler _sampler;
        private readonly ILogger<SelfServerStatsProbe> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelfServerStatsProbe"/> class.
        /// </summary>
        /// <param name="remote">Probe every non-main server is delegated to.</param>
        /// <param name="sampler">Sampler that reads this process's statistics.</param>
        /// <param name="logger">Logger the fallback is reported to.</param>
        public SelfServerStatsProbe(
            [FromKeyedServices(RemoteKey)] IServerStatsProbe remote,
            SelfServerStatsSampler sampler,
            ILogger<SelfServerStatsProbe> logger)
        {
            _remote = remote;
            _sampler = sampler;
            _logger = logger;
        }

        /// <inheritdoc />
        /// <remarks>
        /// The main row is answered from <see cref="SelfServerStatsSampler.ReadAsync"/>; every other
        /// row is the delegate's answer, unchanged. A sampler that throws is treated like a sampler
        /// that answered null - the delegate is asked, and its answer is what the dashboard draws.
        /// A caller that cancelled is answered null instead: the main row is unavailable for that
        /// request alone, and the cancellation is not restated as a machine failure.
        /// </remarks>
        public async Task<ServerStatsSample?> ProbeAsync(
            Server server,
            CancellationToken cancellationToken = default)
        {
            if (!MainServer.IsMain(server.Id))
            {
                return await _remote.ProbeAsync(server, cancellationToken);
            }

            try
            {
                if (await _sampler.ReadAsync(cancellationToken) is { } sample)
                {
                    return sample;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception exception)
            {
                // Reported rather than propagated: one unreadable fact about this machine must not
                // fail the dashboard build, and the delegate below is the documented fallback.
                _logger.LogWarning(
                    exception,
                    "The local statistics of the main server {ServerId} could not be read, so the " +
                    "remote probe is asked instead.",
                    server.Id);
            }

            return await _remote.ProbeAsync(server, cancellationToken);
        }
    }
}
