using Fluxy.Core.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Fluxy.DataAccess.Infrastructure.Redis
{
    /// <summary>
    /// Keeps the identifiers of withdrawn sessions in redis, so that every access token a
    /// session ever held is refused at once rather than when its own lifetime ends.
    /// </summary>
    /// <remarks>
    /// Only session identifiers are stored, and only for as long as the longest lived access
    /// token of that session could still have been accepted. That is what keeps this cheap and
    /// self cleaning: an entry expires with the last token it refers to, so the store empties
    /// itself and there is no cleanup to forget. One marker covers every token the session
    /// produced, including those minted by earlier rotations.
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
        internal const string KeyPrefix = "fluxy:revoked:sid:";

        private readonly RedisConnection _redis;
        private readonly ILogger<RedisTokenRevocationStore> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisTokenRevocationStore"/> class.
        /// </summary>
        /// <param name="redis">
        /// Shared redis connection. It may be unconfigured, in which case the store never leaves
        /// this process and reports every session as live.
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
        public async Task BlockSessionAsync(
            Guid? sessionId,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default)
        {
            if (sessionId is not { } session || session == Guid.Empty || lifetime <= TimeSpan.Zero)
            {
                // Nothing worth remembering: either the session cannot be named or every
                // token it could have produced has already expired, and in both cases the
                // store would hold an entry that means nothing.
                return;
            }

            if (!_redis.TryGetDatabase(out var database))
            {
                return;
            }

            try
            {
                // The entry outlives the last possible token by a moment, not by a margin.
                // Keeping it longer would leave identifiers of sessions that nobody can
                // present any more, and keeping it shorter would open a gap in which a token
                // that is still valid is accepted after a sign-out.
                var key = KeyPrefix + session;

                await database.StringSetAsync(key, "1", lifetime, When.Always);
            }
            catch (RedisException exception)
            {
                // Logged at warning and otherwise ignored, for the reason in the remarks: the
                // access token's own lifetime is the backstop, and a store that cannot be
                // written is not a reason to fail a sign-out that has already been recorded in
                // the database.
                _logger.LogWarning(
                    exception,
                    "Redis could not be written for a revoked session, so the withdrawal will " +
                    "only take effect when its access tokens expire on their own.");
            }
        }

        /// <inheritdoc />
        public async Task<bool> IsSessionBlockedAsync(
            Guid? sessionId,
            CancellationToken cancellationToken = default)
        {
            if (sessionId is not { } session || session == Guid.Empty)
            {
                return false;
            }

            if (!_redis.TryGetDatabase(out var database))
            {
                return false;
            }

            try
            {
                return await database.KeyExistsAsync(KeyPrefix + session);
            }
            catch (RedisException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Redis could not be read for a revoked session, so the request is allowed " +
                    "through. A sign-out during a redis outage takes effect only when the " +
                    "access tokens expire on their own.");

                return false;
            }
        }
    }
}
