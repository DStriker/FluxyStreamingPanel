using Fluxy.Core.Authentication.Interfaces;
using Fluxy.Core.Authentication.ReadModels;
using Fluxy.Core.Authentication.Utils;

namespace Fluxy.API.Security
{
    /// <summary>
    /// The account behind the request, read at most once however many requirements its policy
    /// carries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A permission policy holds two requirements and both of them need the same answer, so the
    /// first handler to ask reads it and the second one reads what the first was told. Without
    /// this the two handlers would issue two identical queries per authorized request, and the
    /// guarantee the whole mechanism rests on - one indexed read, never cached beyond the
    /// request - would quietly become two.
    /// </para>
    /// <para>
    /// Scoped rather than static, because two independent requests must never share an answer:
    /// the lifetime of this object is the lifetime of one request, which is exactly as long as
    /// an answer about one account may be trusted. It is also the boundary the three consumers
    /// share - the role handler, the permission handler and the result handler, which asks the
    /// same question again only to word the refusal correctly and gets the first read back.
    /// </para>
    /// <para>
    /// The answer is remembered even when it is "there is no such account". A refusal this
    /// handlers reached independently should be logged once, not twice, and a caller must not
    /// be able to tell the two cases apart by counting queries.
    /// </para>
    /// </remarks>
    public sealed class RequestStandingReader
    {
        private readonly IAccessChecker _accessChecker;
        private readonly ILogger<RequestStandingReader> _logger;

        private bool _resolved;
        private Guid _userId = Guid.Empty;
        private AccountStanding? _standing;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestStandingReader"/> class.
        /// </summary>
        /// <param name="accessChecker">Reader of the current state of an account.</param>
        /// <param name="logger">Where a refusal worth remembering is reported to.</param>
        public RequestStandingReader(
            IAccessChecker accessChecker,
            ILogger<RequestStandingReader> logger)
        {
            _accessChecker = accessChecker;
            _logger = logger;
        }

        /// <summary>
        /// Reads the account behind <paramref name="user"/>, from this request's own slot when
        /// something already read it and from the database when nothing did.
        /// </summary>
        /// <param name="user">The claims of the request being authorized.</param>
        /// <returns>
        /// The account and its standing, or null when neither could be established - a token
        /// that named no usable account, or an account that is not there.
        /// </returns>
        public async Task<(Guid UserId, AccountStanding Standing)?> ReadAsync(
            System.Security.Claims.ClaimsPrincipal user)
        {
            if (_resolved)
            {
                return _standing is { } known ? (_userId, known) : null;
            }

            var subject = user.FindFirst(TokenClaimTypes.Subject)?.Value
                ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(subject, out var userId))
            {
                _logger.LogWarning(
                    "A token carried no usable account identifier, so the request was refused.");

                Remember(Guid.Empty, null);

                return null;
            }

            var standing = await _accessChecker.GetStandingAsync(userId);

            Remember(userId, standing);

            return standing.Status is null ? null : (userId, standing);
        }

        /// <summary>Writes the answer down, including the ones that are an absence.</summary>
        private void Remember(Guid userId, AccountStanding? standing)
        {
            _resolved = true;
            _userId = userId;
            _standing = standing;
        }
    }
}
