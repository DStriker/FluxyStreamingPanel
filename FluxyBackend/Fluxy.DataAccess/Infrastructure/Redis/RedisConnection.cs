using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Fluxy.DataAccess.Infrastructure.Redis
{
    /// <summary>
    /// Owns the redis connection shared by the stores in this layer.
    /// </summary>
    /// <remarks>
    /// The multiplexer is nullable because an installation without a redis connection string is
    /// a supported degraded state: attempt counters stay in process memory and withdrawn access
    /// tokens are not tracked. Keeping that state here rather than in every consumer is what
    /// lets the container resolve those consumers in that configuration - a nullable constructor
    /// parameter is not an optional dependency to Microsoft.Extensions.DependencyInjection.
    /// </remarks>
    internal sealed class RedisConnection
    {
        private readonly IConnectionMultiplexer? _multiplexer;
        private readonly ILogger<RedisConnection> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisConnection"/> class.
        /// </summary>
        /// <param name="multiplexer">
        /// Shared redis connection, or null when the installation has no connection string.
        /// </param>
        /// <param name="logger">Logger outages and the missing configuration are reported to.</param>
        public RedisConnection(
            IConnectionMultiplexer? multiplexer,
            ILogger<RedisConnection> logger)
        {
            _multiplexer = multiplexer;
            _logger = logger;

            if (multiplexer is null)
            {
                _logger.LogWarning(
                    "No redis connection is configured, so attempt counters are kept in process " +
                    "memory and withdrawn access tokens are not tracked.");
            }
        }

        /// <summary>
        /// Whether redis can serve a command right now, and the database to send it to.
        /// </summary>
        /// <param name="database">Database to use when this method returns true.</param>
        /// <returns>True when a connected multiplexer is available.</returns>
        public bool TryGetDatabase(out IDatabase database)
        {
            database = null!;

            if (_multiplexer is null)
            {
                return false;
            }

            if (!_multiplexer.IsConnected)
            {
                _logger.LogDebug(
                    "Redis is not connected, so the redis-backed operation is degraded for now.");

                return false;
            }

            database = _multiplexer.GetDatabase();

            return true;
        }
    }
}
