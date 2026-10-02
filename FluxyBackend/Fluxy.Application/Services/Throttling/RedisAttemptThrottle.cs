using System.Collections.Concurrent;
using System.Globalization;
using Fluxy.Core.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Fluxy.Application.Services.Throttling
{
    /// <summary>
    /// Counts attempts in redis so that several instances of the application agree on them.
    /// </summary>
    /// <remarks>
    /// The counter is a plain string key whose value is a number. The window is opened by the
    /// first attempt, not by a shared clock, so a burst cannot straddle a boundary and get two
    /// budgets. <c>INCR</c> and the expiry are one Lua script because doing them as two round
    /// trips would let two concurrent first attempts both create the key, and the one that lost
    /// the race would leave a key with no expiry behind.
    ///
    /// Redis holds nothing the application cannot rebuild, so every operation falls back to
    /// process memory when the server is unreachable. A registration must not stop working
    /// because a side counter is down; the cost of the fallback is that the limit then applies
    /// per instance, which is why that is logged rather than hidden.
    /// </remarks>
    public sealed class RedisAttemptThrottle : IAttemptThrottle
    {
        /// <summary>Prefix of every key this throttle owns.</summary>
        public const string KeyPrefix = "fluxy:throttle:";

        /// <summary>
        /// Increments the counter and gives it an expiry the first time a window starts.
        /// Returns the count including the increment.
        /// </summary>
        private const string IncrementScript = """
            local current = redis.call('INCR', KEYS[1])
            if current == 1 then
                redis.call('PEXPIRE', KEYS[1], ARGV[1])
            end
            return current
            """;

        private readonly IConnectionMultiplexer? _multiplexer;
        private readonly ILogger<RedisAttemptThrottle> _logger;
        private readonly ConcurrentDictionary<string, LocalWindow> _fallback = new(StringComparer.Ordinal);

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisAttemptThrottle"/> class.
        /// </summary>
        /// <param name="multiplexer">
        /// Shared redis connection, or null when the installation has no connection string
        /// configured. A null is not an error: the throttle simply never leaves this process.
        /// </param>
        /// <param name="logger">Logger outages and fallbacks are reported to.</param>
        public RedisAttemptThrottle(
            IConnectionMultiplexer? multiplexer,
            ILogger<RedisAttemptThrottle> logger)
        {
            _multiplexer = multiplexer;
            _logger = logger;
        }

        /// <inheritdoc />
        public int GetAttempts(string key, AttemptPolicy policy)
        {
            var redisKey = KeyPrefix + key;

            if (TryGetDatabase(redisKey, out var database, out var window))
            {
                return window
                    ? ReadLocalCount(redisKey, policy)
                    : ReadRemoteCount(database, redisKey, policy);
            }

            return ReadLocalCount(redisKey, policy);
        }

        /// <inheritdoc />
        public int RecordAttempt(string key, AttemptPolicy policy)
        {
            var redisKey = KeyPrefix + key;

            if (TryGetDatabase(redisKey, out var database, out var window))
            {
                return window
                    ? RecordLocalAttempt(redisKey, policy)
                    : RecordRemoteAttempt(database, redisKey, policy);
            }

            return RecordLocalAttempt(redisKey, policy);
        }

        /// <inheritdoc />
        public void ResetAttempts(string key)
        {
            var redisKey = KeyPrefix + key;

            if (TryGetDatabase(redisKey, out var database, out var window))
            {
                // Removed before the local entry rather than instead of it: an instance that has
                // fallen back to memory must forget too, or the same process would keep counting
                // what redis has just forgotten.
                _fallback.TryRemove(redisKey, out _);

                if (window)
                {
                    return;
                }

                try
                {
                    // FireAndForget because the caller has already decided the window is over and
                    // gains nothing from the answer. A key that is not there is the expected
                    // state, not a failure.
                    database.KeyDeleteAsync(redisKey, CommandFlags.FireAndForget);
                }
                catch (RedisException exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Redis could not be cleared for {Key}, so the recorded attempts stay " +
                        "counted until the window closes on its own.",
                        redisKey);
                }

                return;
            }

            _fallback.TryRemove(redisKey, out _);
        }

        /// <summary>
        /// Whether redis can serve this key right now. Reports the reason once per call when it
        /// cannot, which is enough to make a misconfigured installation visible without turning
        /// every request into a log entry.
        /// </summary>
        private bool TryGetDatabase(string redisKey, out IDatabase database, out bool unreachable)
        {
            database = null!;
            unreachable = false;

            if (_multiplexer is null)
            {
                unreachable = true;

                return false;
            }

            if (!_multiplexer.IsConnected)
            {
                unreachable = true;
                _logger.LogDebug(
                    "Redis is not connected, so the attempt counter for {Key} is kept in memory.",
                    redisKey);

                return false;
            }

            database = _multiplexer.GetDatabase();
            return true;
        }

        private int ReadRemoteCount(IDatabase database, string redisKey, AttemptPolicy policy)
        {
            try
            {
                var value = database.StringGet(redisKey);

                if (value.IsNull)
                {
                    return 0;
                }

                // A value that is not a number means something else wrote over the key. Treating
                // it as zero is the only reading that does not lock the caller out permanently.
                return value.TryParse(out int count) ? count : 0;
            }
            catch (RedisException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Redis could not be read for {Key}, so the attempt counter is kept in memory " +
                    "for now.",
                    redisKey);

                return ReadLocalCount(redisKey, policy);
            }
        }

        private int RecordRemoteAttempt(IDatabase database, string redisKey, AttemptPolicy policy)
        {
            try
            {
                var milliseconds = (long)policy.Window.TotalMilliseconds;
                var result = database.ScriptEvaluate(
                    IncrementScript,
                    [redisKey],
                    [milliseconds.ToString(CultureInfo.InvariantCulture)]);

                return (int)result;
            }
            catch (RedisException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Redis could not be written for {Key}, so the attempt counter is kept in " +
                    "memory for now.",
                    redisKey);

                return RecordLocalAttempt(redisKey, policy);
            }
        }

        private int ReadLocalCount(string key, AttemptPolicy policy)
        {
            if (!_fallback.TryGetValue(key, out var window))
            {
                return 0;
            }

            if (IsExpired(window, policy))
            {
                // Removing is best effort: another thread may have replaced the entry in the
                // meantime, and replacing it again would restart the window on purpose.
                _fallback.TryRemove(key, out _);

                return 0;
            }

            return window.Count;
        }

        private int RecordLocalAttempt(string key, AttemptPolicy policy)
        {
            while (true)
            {
                if (!_fallback.TryGetValue(key, out var existing))
                {
                    var first = new LocalWindow(DateTimeOffset.UtcNow, 1);

                    if (_fallback.TryAdd(key, first))
                    {
                        return first.Count;
                    }

                    // Another thread added the entry between the two calls. Look at theirs.
                    continue;
                }

                // TryUpdate compares against the value that was read, so a thread that got there
                // first wins and this one retries instead of overwriting a newer window.
                var updated = IsExpired(existing, policy)
                    ? new LocalWindow(DateTimeOffset.UtcNow, 1)
                    : existing with { Count = existing.Count + 1 };

                if (_fallback.TryUpdate(key, updated, existing))
                {
                    return updated.Count;
                }
            }
        }

        private static bool IsExpired(LocalWindow window, AttemptPolicy policy)
            => DateTimeOffset.UtcNow - window.StartedAt >= policy.Window;

        /// <summary>
        /// A counting window held in this process only. It is a record so that an update can be
        /// written back atomically with <see cref="ConcurrentDictionary{TKey,TValue}.TryUpdate"/>.
        /// </summary>
        private readonly record struct LocalWindow(DateTimeOffset StartedAt, int Count);
    }
}
