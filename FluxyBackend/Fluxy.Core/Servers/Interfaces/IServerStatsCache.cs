using Fluxy.Core.Servers.ReadModels;

namespace Fluxy.Core.Servers.Interfaces
{
    /// <summary>
    /// Holds the most recent dashboard snapshot for a few seconds, so a screen that refreshes
    /// often does not probe every server on every request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cache is an optimisation and never a dependency: an unconfigured, unreachable or
    /// erroring store degrades to building the snapshot directly. That is the deliberate
    /// direction to fail in - a dashboard that stopped answering because its cache did would be
    /// a self-inflicted outage.
    /// </para>
    /// <para>
    /// The single-flight gate is part of the contract: two requests that miss at the same moment
    /// must not both build. The second waits, then reads what the first stored.
    /// </para>
    /// </remarks>
    public interface IServerStatsCache
    {
        /// <summary>
        /// Returns the cached snapshot, or builds it once under the single-flight gate.
        /// An unconfigured, unreachable or erroring Redis degrades to building directly;
        /// this method never fails because of the cache itself.
        /// </summary>
        /// <param name="build">Builder to run on a miss.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// An exception thrown by <paramref name="build"/> is a database failure and propagates;
        /// the gate is released in a <c>finally</c> either way.
        /// </remarks>
        Task<DashboardSnapshot> GetOrAddAsync(
            Func<CancellationToken, Task<DashboardSnapshot>> build,
            CancellationToken cancellationToken = default);
    }
}
