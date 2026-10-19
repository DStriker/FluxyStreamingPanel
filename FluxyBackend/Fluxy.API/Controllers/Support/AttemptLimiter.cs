using System.Globalization;
using Fluxy.API.Contracts.Common;
using Fluxy.API.Options;
using Fluxy.Core.Security.Interfaces;
using Fluxy.Core.Security.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Fluxy.API.Controllers.Support
{
    /// <summary>
    /// The attempt window of the guest-facing endpoints: the live limits, the counter behind
    /// them, and the refusal a spent window gets.
    /// </summary>
    /// <remarks>
    /// Extracted from <c>AuthControllerBase</c> when the controllers were grouped by area. The
    /// captcha and the attempt window guard an anonymous caller and an expensive key
    /// derivation, and only registration, authentication, password reset and profile run
    /// either; the session history inherited the same base and had no window of its own.
    /// Inheriting the helpers made that controller name a throttle it never spent, so
    /// composition replaced inheritance for the guard while the base kept what every area
    /// needs.
    ///
    /// The two counter calls stay separate because the endpoints do not agree on when an
    /// attempt is spent. A registration is counted only once a row exists, so a mistyped form
    /// costs nothing; a sign-in is counted before the password comparison, so the expensive
    /// step is unreachable once the window is full. Folding one into the other would change
    /// that behaviour, which is the contract.
    /// </remarks>
    public sealed class AttemptLimiter
    {
        private readonly IAttemptThrottle _throttle;
        private readonly IOptionsMonitor<RateLimitOptions> _rateLimits;

        /// <summary>
        /// Initializes a new instance of the <see cref="AttemptLimiter"/> class.
        /// </summary>
        /// <param name="throttle">Counter that limits how often one client may try.</param>
        /// <param name="rateLimits">Live limits, so a configuration reload applies without a restart.</param>
        public AttemptLimiter(
            IAttemptThrottle throttle,
            IOptionsMonitor<RateLimitOptions> rateLimits)
        {
            _throttle = throttle;
            _rateLimits = rateLimits;
        }

        /// <summary>
        /// The limits in force right now, so a configuration reload is visible to the next
        /// request without a restart.
        /// </summary>
        public RateLimitOptions Limits => _rateLimits.CurrentValue;

        /// <summary>
        /// Number of attempts already recorded for <paramref name="key"/>, without changing it.
        /// </summary>
        /// <param name="key">Identifier of the counted party.</param>
        /// <param name="policy">Limit and window to reason with.</param>
        /// <returns>The current count, or zero when nothing has been recorded yet.</returns>
        public int Attempts(string key, AttemptPolicy policy)
            => _throttle.GetAttempts(key, policy);

        /// <summary>
        /// Records one more attempt for <paramref name="key"/>.
        /// </summary>
        /// <param name="key">Identifier of the counted party.</param>
        /// <param name="policy">Limit and window the count is kept in.</param>
        /// <returns>The count including the attempt just recorded.</returns>
        public int Record(string key, AttemptPolicy policy)
            => _throttle.RecordAttempt(key, policy);

        /// <summary>
        /// Forgets everything recorded for <paramref name="key"/>.
        /// </summary>
        /// <param name="key">Identifier of the counted party.</param>
        public void Reset(string key)
            => _throttle.ResetAttempts(key);

        /// <summary>
        /// The refusal for a client that has spent its window, with the wait it should expect.
        /// </summary>
        /// <param name="response">Response whose <c>Retry-After</c> header is written.</param>
        /// <param name="code">Code the client branches on.</param>
        /// <param name="window">Length of the window that has to pass.</param>
        /// <returns>A 429 answer carrying the shared error envelope.</returns>
        public IActionResult Throttled(HttpResponse response, string code, TimeSpan window)
        {
            response.Headers.RetryAfter = ((int)window.TotalSeconds)
                .ToString(CultureInfo.InvariantCulture);

            return new ObjectResult(
                new MessageResponse
                {
                    Code = code,
                    Message = $"Too many attempts. Please try again in {Describe(window)}."
                })
            {
                StatusCode = StatusCodes.Status429TooManyRequests
            };
        }

        /// <summary>
        /// A window in words, rounded to whole minutes so that a fifteen minute wait does not
        /// come out as "14.99 minutes".
        /// </summary>
        private static string Describe(TimeSpan window)
        {
            var minutes = (int)Math.Round(window.TotalMinutes, MidpointRounding.AwayFromZero);

            return minutes == 1 ? "1 minute" : $"{minutes} minutes";
        }
    }
}
