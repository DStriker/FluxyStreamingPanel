using Fluxy.Core.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Fluxy.DataAccess.Infrastructure.Redis
{
    /// <summary>
    /// Keeps withdrawn access token identifiers in redis, so a sign-out takes effect at once
    /// rather than when the token would have expired.
    /// </summary>
    /// <remarks>
    /// Only identifiers are stored, and only for as long as the token they name would still have
    /// been accepted. That is what keeps this cheap and self cleaning: an entry expires with the
    /// token it refers to, so the store empties itself and there is no cleanup to forget. A
    /// token older than the longest access lifetime is gone from the store because it is already
    /// gone from circulation.
    ///
    /// An unreachable redis makes every check answer "not withdrawn" and is logged rather than
    /// refused. That is the deliberate direction to fail in. Refusing would mean no signed-in
    /// user could do anything at all while redis was down, which is a self-inflicted outage
    /// traded for a guarantee about a window that is at most a few minutes wide either way -
    /// and a sign-out during that outage is recoverable, while an outage that locks everyone out
    /// is not.
    /// </remarks>
    internal sealed class RedisTokenRevocationStore : ITokenRevocationStore
    {
        /// <summary>Prefix of every key this store owns.</summary>
        internal const string KeyPrefix = "fluxy:revoked:jti:";

        private readonly RedisConnection _redis;
        private readonly ILogger<RedisTokenRevocationStore> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisTokenRevocationStore"/> class.
        /// </summary>
        /// <param name="redis">
        /// Shared redis connection. It may be unconfigured, in which case the store never leaves
        /// this process and reports every token as live.
        /// </param>
        /// <param name="logger">Logger outages and refusals are reported to.</param>
        public RedisTokenRevocationStore(
            RedisConnection redis,
            ILogger<RedisTokenRevocationStore> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task BlockAsync(
            string? tokenId,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(tokenId) || lifetime <= TimeSpan.Zero)
            {
                // Nothing worth remembering: either the token cannot be named or it has already
                // expired, and in both cases the store would hold an entry that means nothing.
                return;
            }

            if (!_redis.TryGetDatabase(out var database))
            {
                return;
            }

            try
            {
                // The entry outlives the token by a moment, not by a margin. Keeping it longer
                // would leave identifiers of tokens that nobody can present any more, and
                // keeping it shorter would open a gap in which a token that is still valid is
                // accepted after a sign-out.
                var key = KeyPrefix + tokenId;

                await database.StringSetAsync(
                    key,
                    "1",
                    lifetime,
                    When.Always,
                    CommandFlags.FireAndForget);
            }
            catch (RedisException exception)
            {
                // Logged at warning and otherwise ignored, for the reason in the remarks: the
                // access token's own lifetime is the backstop, and a store that cannot be
                // written is not a reason to fail a sign-out that has already been recorded in
                // the database.
                _logger.LogWarning(
                    exception,
                    "Redis could not be written for a withdrawn access token, so the withdrawal " +
                    "will only take effect when the token expires on its own.");
            }
        }

        /// <inheritdoc />
        public async Task<bool> IsBlockedAsync(
            string? tokenId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(tokenId))
            {
                return false;
            }

            if (!_redis.TryGetDatabase(out var database))
            {
                return false;
            }

            try
            {
                return await database.KeyExistsAsync(KeyPrefix + tokenId);
            }
            catch (RedisException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Redis could not be read for a withdrawn access token, so the request is " +
                    "allowed through. A sign-out during a redis outage takes effect only when the " +
                    "access token expires on its own.");

                return false;
            }
        }
    }
}
