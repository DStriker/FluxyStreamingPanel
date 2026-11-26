using System.Collections.Concurrent;
using Fluxy.Core.Servers.Interfaces;
using Fluxy.Core.Servers.Models;
using Fluxy.Core.Servers.ReadModels;

namespace Fluxy.IntegrationTests.Infrastructure.Fakes
{
    /// <summary>
    /// Answers server statistics without contacting a server, and records what the dashboard asked
    /// for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The real probe dials <c>http://{ip}:{port}/api/stats</c>, which cannot be reached from a
    /// Testcontainers host and would make every dashboard test depend on network timing. This fake
    /// is replaced in <see cref="FluxyApiFactory"/> beside the other external dependencies.
    /// </para>
    /// <para>
    /// <b>The counters are per process, not per fact.</b> <see cref="CallCount"/> is what proves a
    /// cache hit performed no probes; <see cref="Current"/>/<see cref="MaxActive"/> are what a
    /// bounded-concurrency test observes when <see cref="Delay"/> makes the probes overlap. The
    /// dictionary is concurrent because the dashboard probes in parallel.
    /// </para>
    /// </remarks>
    public sealed class FakeServerStatsProbe : IServerStatsProbe
    {
        private readonly ConcurrentDictionary<Guid, ServerStatsSample?> _samples = new();
        private int _active;

        /// <summary>Sample every server answers with, unless one was set on its identifier.</summary>
        public ServerStatsSample? Result { get; set; }

        /// <summary>How many probes have been made since the fixture was reset.</summary>
        public int CallCount => _calls;

        /// <summary>Whether the probe throws instead of answering.</summary>
        public bool ThrowInstead { get; set; }

        /// <summary>How many probes are in flight right now.</summary>
        public int Current => _active;

        /// <summary>The highest number of probes that were in flight at once.</summary>
        public int MaxActive => _maxActive;

        /// <summary>Optional delay, so overlapping probes can be observed.</summary>
        public Func<CancellationToken, Task>? Delay { get; set; }

        private int _calls;
        private int _maxActive;

        /// <summary>Sets the sample one specific server answers with; null means unavailable.</summary>
        /// <param name="serverId">Server to answer for.</param>
        /// <param name="sample">Sample to answer with, or null for "did not answer".</param>
        public void SetResult(Guid serverId, ServerStatsSample? sample) => _samples[serverId] = sample;

        /// <summary>Clears the counters and every per-server answer.</summary>
        public void Reset()
        {
            Result = null;
            ThrowInstead = false;
            Delay = null;
            _samples.Clear();
            Interlocked.Exchange(ref _calls, 0);
            Interlocked.Exchange(ref _active, 0);
            Interlocked.Exchange(ref _maxActive, 0);
        }

        /// <inheritdoc />
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
                if (Delay is not null)
                {
                    await Delay(cancellationToken);
                }

                if (ThrowInstead)
                {
                    throw new HttpRequestException("The fake probe was told to fail.");
                }

                return _samples.TryGetValue(server.Id, out var sample) ? sample : Result;
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }
}
