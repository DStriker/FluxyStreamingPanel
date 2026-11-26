using System.Text.Json;
using Fluxy.Core.Servers.Interfaces;
using Fluxy.Core.Servers.ReadModels;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Fluxy.DataAccess.Infrastructure.Redis
{
    /// <summary>
    /// Keeps the most recent dashboard snapshot in redis for a few seconds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The cache is not a dependency.</b> An installation with no redis connection string, or
    /// one whose redis is unreachable, degrades to building the snapshot on every request: the
    /// store answers "I could not help" and the caller proceeds as if it had missed. A dashboard
    /// that stopped answering because its cache did would be a self-inflicted outage, and this is
    /// the direction the other redis stores here fail in too.
    /// </para>
    /// <para>
    /// <b>Two misses must not build twice.</b> The gate is a single semaphore for this singleton:
    /// the first caller that misses takes it, re-reads the key and builds; a second caller that
    /// arrived in the meantime waits, then finds what the first one stored. The gate is per
    /// process, which is the right scope for a ten-second cache - the alternative is a redis lock
    /// whose failure modes are worse than two builds.
    /// </para>
    /// <para>
    /// <b>Exceptions from the builder propagate.</b> They are database failures, not cache
    /// failures, and swallowing them would turn a broken read into an empty dashboard. The gate is
    /// released in a <c>finally</c> either way, so one failed build does not wedge the process.
    /// </para>
    /// </remarks>
    internal sealed class RedisServerStatsCache : IServerStatsCache
    {
        /// <summary>Key the snapshot is stored under.</summary>
        internal const string Key = "fluxy:dashboard:v1";

        /// <summary>How long a stored snapshot is served before it is rebuilt.</summary>
        internal static readonly TimeSpan Ttl = TimeSpan.FromSeconds(10);

        private static readonly JsonSerializerOptions SerializerOptions =
            new(JsonSerializerDefaults.Web);

        private readonly RedisConnection _redis;
        private readonly ILogger<RedisServerStatsCache> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisServerStatsCache"/> class.
        /// </summary>
        /// <param name="redis">
        /// Shared redis connection. It may be unconfigured, in which case every call builds
        /// directly.
        /// </param>
        /// <param name="logger">Logger cache failures are reported to.</param>
        public RedisServerStatsCache(
            RedisConnection redis,
            ILogger<RedisServerStatsCache> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<DashboardSnapshot> GetOrAddAsync(
            Func<CancellationToken, Task<DashboardSnapshot>> build,
            CancellationToken cancellationToken = default)
        {
            if (!_redis.TryGetDatabase(out var database))
            {
                // No cache configured or no connection: the answer is still the real one, built
                // now. Nothing is logged here because RedisConnection already said why once.
                return await build(cancellationToken);
            }

            if (await TryReadAsync(database, cancellationToken) is { } cached)
            {
                return cached;
            }

            await _gate.WaitAsync(cancellationToken);

            try
            {
                // Re-read inside the gate: another request may have built while this one waited,
                // and building again would be the stampede the gate exists to prevent.
                if (await TryReadAsync(database, cancellationToken) is { } fresh)
                {
                    return fresh;
                }

                var snapshot = await build(cancellationToken);

                await TryWriteAsync(database, snapshot, cancellationToken);

                return snapshot;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>Reads and deserializes the stored snapshot, or null when there is none.</summary>
        private async Task<DashboardSnapshot?> TryReadAsync(
            IDatabase database,
            CancellationToken cancellationToken)
        {
            try
            {
                var value = await database.StringGetAsync(Key);

                if (value.IsNullOrEmpty)
                {
                    return null;
                }

                return JsonSerializer.Deserialize<DashboardSnapshot>(
                    value.ToString(),
                    SerializerOptions);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller gave up: that is the request ending, not the cache failing, and it
                // keeps the cancellation contract of the surrounding call.
                throw;
            }
            catch (Exception exception)
            {
                // An unreachable store and a value written by an older shape are the same answer
                // here: a miss. The value is left in place so the write below replaces it.
                _logger.LogWarning(
                    exception,
                    "The dashboard snapshot could not be read from redis, so it is rebuilt.");
            }

            return null;
        }

        /// <summary>Stores the snapshot, treating a failed write as a cache miss that never was.</summary>
        private async Task TryWriteAsync(
            IDatabase database,
            DashboardSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            try
            {
                var json = JsonSerializer.Serialize(snapshot, SerializerOptions);

                await database.StringSetAsync(Key, json, Ttl);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // A failed write is a cache miss that never was: the next request builds again,
                // which is exactly what the caller was about to do anyway.
                _logger.LogWarning(
                    exception,
                    "The dashboard snapshot could not be stored in redis, so the next request " +
                    "will build it again.");
            }
        }
    }
}
