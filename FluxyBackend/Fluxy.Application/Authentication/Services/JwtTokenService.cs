using Fluxy.Application.Authentication.Utils;
using Fluxy.Application.Users.Utils;
using Fluxy.Core.Authentication.Enums;
using Fluxy.Core.Authentication.Interfaces;
using Fluxy.Core.Authentication.Models;
using Fluxy.Core.Authentication.Options;
using Fluxy.Core.Authentication.Outcomes;
using Fluxy.Core.Authentication.Repositories;
using Fluxy.Core.Authentication.Utils;
using Fluxy.Core.GeoIp.Interfaces;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Interfaces;
using Fluxy.Core.Users.Models;
using Fluxy.Core.Users.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Fluxy.Application.Authentication.Services
{
    /// <summary>
    /// Issues, rotates and ends the token pairs of a session.
    /// </summary>
    /// <remarks>
    /// The two tokens are built differently on purpose, because the things they are exposed to
    /// are different. The access token is signed and self contained, so nothing about it can be
    /// changed after it is written and any request can be accepted from it without a lookup
    /// beyond the one keyed read that refuses a revoked session - that read is what lets a
    /// sign-in be ended before the token's own time is up, while the short life bounds what an
    /// unreachable store can leave open. The refresh token is an opaque random value with no
    /// claims in it at all, and it exists in the database only as a hash, so it can be revoked
    /// outright, expired, traced and ended in a single step. Nothing is gained by making it a
    /// signed token: it is never read, only exchanged.
    ///
    /// Every refresh spends the token it was given and issues a new one in the same session.
    /// A token that is presented a second time is therefore either a replay or a copy, and
    /// there is no way to tell those apart, so the whole session is destroyed rather than
    /// guessed at. A user whose browser retried a request they already got an answer to pays
    /// one sign-in for it.
    /// </remarks>
    public sealed class JwtTokenService : ITokenService
    {
        /// <summary>
        /// Bytes of entropy in a refresh token. 32 is the output size of SHA-256 and far more
        /// than a token that is looked up by its hash could ever need to be unguessable.
        /// </summary>
        private const int RefreshTokenByteCount = 32;

        /// <summary>
        /// Hash used for the stored form of a refresh token and for the signing key material.
        /// SHA-256 is right here rather than a password hash on purpose: the refresh token is
        /// 256 bits of entropy chosen by this service, not something a person chose and could
        /// be weak, so stretching it would only make every refresh slower for no added safety.
        /// </summary>
        private static readonly HashAlgorithmName HashName = HashAlgorithmName.SHA256;

        private readonly IRefreshTokenRepository _tokens;
        private readonly ITokenRevocationStore _revocations;
        private readonly IUserRepository _users;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOptionsMonitor<AuthenticationOptions> _options;
        private readonly IGeoIpResolver _geoIp;
        private readonly ILoginGuardService _guard;
        private readonly IHostEnvironment _environment;
        private readonly SigningCredentials _credentials;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<JwtTokenService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="JwtTokenService"/> class.
        /// </summary>
        /// <param name="tokens">Repository of the issued refresh tokens.</param>
        /// <param name="revocations">
        /// Store the sessions are recorded in when their access tokens have to stop working
        /// before their own lifetime ends.
        /// </param>
        /// <param name="users">Repository the accounts are read from.</param>
        /// <param name="unitOfWork">Commits one rotation as one save.</param>
        /// <param name="options">Live settings, so a configuration reload applies without a restart.</param>
        /// <param name="geoIp">Resolver that places the client address on the map.</param>
        /// <param name="guard">Enforcer of the account's allow lists.</param>
        /// <param name="environment">Host environment, for the Development bypass of local networks.</param>
        /// <param name="timeProvider">
        /// Clock the lifetimes are measured with. Injected so a window can be exercised without
        /// waiting for it.
        /// </param>
        /// <param name="logger">Logger the security relevant events are reported to.</param>
        /// <exception cref="InvalidOperationException">
        /// The signing key is missing or too short. Failing here means a misconfigured
        /// installation refuses to start rather than minting tokens nobody can verify.
        /// </exception>
        public JwtTokenService(
            IRefreshTokenRepository tokens,
            ITokenRevocationStore revocations,
            IUserRepository users,
            IUnitOfWork unitOfWork,
            IOptionsMonitor<AuthenticationOptions> options,
            IGeoIpResolver geoIp,
            ILoginGuardService guard,
            IHostEnvironment environment,
            TimeProvider timeProvider,
            ILogger<JwtTokenService> logger)
        {
            _tokens = tokens;
            _revocations = revocations;
            _users = users;
            _unitOfWork = unitOfWork;
            _options = options;
            _geoIp = geoIp;
            _guard = guard;
            _environment = environment;
            _timeProvider = timeProvider;
            _logger = logger;

            _credentials = new SigningCredentials(
                new SymmetricSecurityKey(
                    TokenSigningKey.Materialize(options.CurrentValue.SigningKey)),
                SecurityAlgorithms.HmacSha256);
        }

        /// <inheritdoc />
        public async Task<IssuedTokens> IssueAsync(
            User user,
            string? clientAddress = null,
            string? userAgent = null,
            CancellationToken cancellationToken = default)
        {
            var options = _options.CurrentValue;
            var now = _timeProvider.GetUtcNow();
            var sessionId = Guid.NewGuid();

            var refreshToken = GenerateRefreshToken();
            var accessToken = CreateAccessToken(user, sessionId, now, options);

            await _tokens.AddAsync(
                new RefreshToken
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    TokenHash = HashToken(refreshToken),
                    SessionId = sessionId,
                    ExpiresAt = now.Add(options.RefreshLifetime),
                    CreatedAt = now,
                    ClientAddress = Truncate(clientAddress, RefreshTokenConfigurationLimits.ClientAddress),
                    UserAgent = Truncate(userAgent, RefreshTokenConfigurationLimits.UserAgent)
                },
                cancellationToken);

            await CleanupExpiredAsync(now, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Opened a session for {Username} in role {Role}. It expires at {ExpiresAt}.",
                user.Username,
                user.Role,
                now.Add(options.RefreshLifetime));

            return new IssuedTokens
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = now.Add(options.AccessLifetime),
                RefreshTokenExpiresAt = now.Add(options.RefreshLifetime),
                SessionId = sessionId
            };
        }

        /// <inheritdoc />
        public async Task<RefreshOutcome> RefreshAsync(
            string? presentedToken,
            string? clientAddress = null,
            string? userAgent = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(presentedToken))
            {
                return new RefreshOutcome { Status = RefreshStatus.InvalidToken };
            }

            var stored = await _tokens.FindByHashAsync(
                HashToken(presentedToken),
                cancellationToken);

            if (stored is null)
            {
                // An unknown token says nothing worth logging: someone guessing a token is the
                // expected case, and the answer is the same one an expired token gets.
                return new RefreshOutcome { Status = RefreshStatus.InvalidToken };
            }

            var now = _timeProvider.GetUtcNow();

            if (stored.ExpiresAt <= now)
            {
                return new RefreshOutcome { Status = RefreshStatus.InvalidToken };
            }

            if (stored.RevokedAt is not null)
            {
                // Already spent, so this is a second presentation of the same token. A browser
                // retrying a request and a copied token are indistinguishable from here, and
                // the only safe reading of an ambiguity like that is to assume the copy.
                await RevokeSessionAsync(stored.SessionId, cancellationToken);

                _logger.LogWarning(
                    "A refresh token of session {SessionId} was presented after it had been spent, " +
                    "so the whole session was revoked. The account is {UserId}.",
                    stored.SessionId,
                    stored.UserId);

                return new RefreshOutcome { Status = RefreshStatus.SessionRevoked };
            }

            // Read once, and read as the model: what matters here is the account's effective
            // status, which is its own row combined with its group's. A blocked group ends the
            // session for the same reason a blocked account does.
            var account = await _users.FindByIdAsync(stored.UserId, cancellationToken);

            if (account is null || account.Status is not UserStatus.Registered)
            {
                // The account stopped being usable while the session was open - blocked, or
                // gone. The chain is closed rather than left waiting for the account to come
                // back, because the token in the browser should not outlive the right to use it.
                await RevokeSessionAsync(stored.SessionId, cancellationToken);

                _logger.LogInformation(
                    "A refresh for account {UserId} was refused because the account is {Status}, " +
                    "and its session was revoked.",
                    stored.UserId,
                    account?.Status.ToString() ?? "missing");

                return new RefreshOutcome { Status = RefreshStatus.SessionRevoked };
            }

            var options = _options.CurrentValue;
            var refreshToken = GenerateRefreshToken();

            // A session bound to its opening address ends here rather than rotating: the address
            // on the presented token is the last one the session was seen at, so a different one
            // now means the session moved. Compared as addresses rather than as text, because one
            // IPv6 peer has many spellings and a session must not end over formatting.
            // Read from the model rather than from the token's own row: the block above already
            // returned on a missing account, so `account` is the one that has been proved to
            // exist. Going through the model is also the honest reading of what is being asked,
            // since the effective status that decided the block came from the same place.
            if (account.BindSessionToIp && !LoginGuardPolicy.AddressesEqual(stored.ClientAddress, clientAddress))
            {
                await RevokeSessionAsync(stored.SessionId, cancellationToken);

                _logger.LogInformation(
                    "A refresh of session {SessionId} was refused because it arrived from " +
                    "{ClientAddress} while the session is bound to {BoundAddress}, and its " +
                    "session was revoked.",
                    stored.SessionId,
                    clientAddress,
                    stored.ClientAddress);

                return new RefreshOutcome { Status = RefreshStatus.SessionRevoked };
            }

            // The guard is rechecked on every rotation, not just at sign-in: a session outlives
            // the network it was opened from, and a stolen refresh token is worthless if the
            // thief's network is not allowed. Revoked rather than merely refused, like a
            // blocked account - the session must not survive its own network.
            //
            // Through the model again, for the reason the block above gives: it is the one
            // value here that has already been proved to exist.
            if (account.GeoProtectionEnabled
                && !DevelopmentGuardBypass.BypassesEnforcement(clientAddress, _environment, _logger))
            {
                var geo = await _geoIp.ResolveAsync(clientAddress, cancellationToken);

                if (!await _guard.IsAllowedAsync(account.Id, geo, cancellationToken))
                {
                    await RevokeSessionAsync(stored.SessionId, cancellationToken);

                    _logger.LogInformation(
                        "A refresh of session {SessionId} was refused because its network is no " +
                        "longer on the account's allow lists, and its session was revoked.",
                        stored.SessionId);

                    return new RefreshOutcome { Status = RefreshStatus.SessionRevoked };
                }
            }

            // The identifier is chosen here rather than by the database, so the row being spent
            // can name its replacement in the same save. That link is what makes a chain
            // walkable, and it cannot be filled in after the insert returns.
            var replacementId = Guid.NewGuid();

            await _tokens.AddAsync(
                new RefreshToken
                {
                    Id = replacementId,
                    UserId = stored.UserId,
                    TokenHash = HashToken(refreshToken),
                    SessionId = stored.SessionId,
                    ExpiresAt = now.Add(options.RefreshLifetime),
                    CreatedAt = now,
                    ClientAddress = Truncate(clientAddress, RefreshTokenConfigurationLimits.ClientAddress),
                    UserAgent = Truncate(userAgent, RefreshTokenConfigurationLimits.UserAgent)
                },
                cancellationToken);

            await _tokens.SpendAsync(stored.Id, replacementId, now, cancellationToken);

            await CleanupExpiredAsync(now, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new RefreshOutcome
            {
                Status = RefreshStatus.Refreshed,
                UserId = account.Id,
                Role = account.Role,
                Tokens = new IssuedTokens
                {
                    AccessToken = CreateAccessToken(account, stored.SessionId, now, options),
                    RefreshToken = refreshToken,
                    AccessTokenExpiresAt = now.Add(options.AccessLifetime),
                    RefreshTokenExpiresAt = now.Add(options.RefreshLifetime),
                    SessionId = stored.SessionId
                }
            };
        }

        /// <inheritdoc />
        public async Task<bool> RevokeSessionAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            var now = _timeProvider.GetUtcNow();

            // Only the live rows are touched. Rows that are already revoked are left as they are:
            // their timestamps are the record of when they were spent, and overwriting them would
            // destroy the one piece of evidence that distinguishes a spent token from a stolen
            // one. The revoked_at condition is therefore in the query rather than in a filter
            // over everything the session ever had.
            var updated = await _tokens.RevokeSessionAsync(sessionId, now, cancellationToken);

            // Written even when no live refresh row remained: logout can arrive with an access
            // token whose chain was already ended, and that token still has to stop working.
            // The marker covers every access token the session ever held, not just this one.
            await _revocations.BlockSessionAsync(
                sessionId,
                _options.CurrentValue.SessionRevocationLifetime,
                cancellationToken);

            return updated;
        }

        /// <inheritdoc />
        public async Task<bool> RevokeAllSessionsAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var now = _timeProvider.GetUtcNow();

            // Same shape, and for the same reason: only the open rows are touched, because the
            // timestamp on an already revoked row is the record of when it was spent and
            // overwriting it would destroy the one thing that tells a spent token from a stolen
            // one. Every chain of the account goes at once rather than one at a time - a
            // password change has to leave nothing behind that could be exchanged later.
            var sessions = await _tokens.RevokeAllSessionsAsync(userId, now, cancellationToken);

            // The database revocation ends what can be refreshed; these markers end what is
            // already in a browser. Both halves are needed for a password change or a block to
            // take effect at once rather than at the end of the access token's life.
            foreach (var sessionId in sessions)
            {
                await _revocations.BlockSessionAsync(
                    sessionId,
                    _options.CurrentValue.SessionRevocationLifetime,
                    cancellationToken);
            }

            if (sessions.Count > 0)
            {
                _logger.LogInformation(
                    "Ended {Count} live session(s) of account {UserId}.",
                    sessions.Count,
                    userId);
            }

            return sessions.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> RevokeUserSessionAsync(
            Guid userId,
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            var now = _timeProvider.GetUtcNow();

            // The ownership check is in the predicate rather than in a separate read: a row
            // that does not belong to this account simply does not match, so the answer is
            // "nothing was revoked" - the same answer a session that does not exist gets. The
            // id is a guessable value, and a caller must not be able to tell a session that is
            // gone from one that belongs to somebody else by the difference in the refusal.
            //
            // Only the live rows are touched, for the reason every revoke states: the timestamp
            // on an already revoked row is the record of when it was spent, and overwriting it
            // would destroy the one thing that tells a spent token from a stolen one. "Live"
            // is the same word the list uses (see ActiveSessionService): not revoked *and* not
            // expired. A chain whose only row has expired is a session that can no longer be
            // refreshed - already dead - so it answers 404 like every other dead session
            // rather than 200 for an ending that changes nothing, and the refusal vocabulary
            // stays the one the page's id could have come from.
            var updated = await _tokens.RevokeUserSessionAsync(
                userId,
                sessionId,
                now,
                cancellationToken);

            if (updated)
            {
                // Only when a live row was ended: an unknown session, one of another account
                // and one that is already dead all answer false, and none of them should leave
                // a marker behind.
                await _revocations.BlockSessionAsync(
                    sessionId,
                    _options.CurrentValue.SessionRevocationLifetime,
                    cancellationToken);

                _logger.LogInformation(
                    "Ended session {SessionId} of account {UserId}.",
                    sessionId,
                    userId);
            }

            return updated;
        }

        /// <inheritdoc />
        public async Task<int> RevokeAllExceptCurrentAsync(
            Guid userId,
            Guid exceptSessionId,
            CancellationToken cancellationToken = default)
        {
            var now = _timeProvider.GetUtcNow();

            // The sessions are read before the update by the repository, because the caller
            // has to withdraw the access tokens of each one and the update touches tokens. It
            // also requires the rows to still be live, so a chain that quietly expired is not
            // counted as ended.
            var sessions = await _tokens.RevokeAllExceptCurrentAsync(
                userId,
                exceptSessionId,
                now,
                cancellationToken);

            foreach (var sessionId in sessions)
            {
                await _revocations.BlockSessionAsync(
                    sessionId,
                    _options.CurrentValue.SessionRevocationLifetime,
                    cancellationToken);
            }

            if (sessions.Count > 0)
            {
                _logger.LogInformation(
                    "Ended {Count} session(s) of account {UserId} except its current one.",
                    sessions.Count,
                    userId);
            }

            return sessions.Count;
        }

        /// <summary>
        /// Builds the signed part of a session: who the account is, what it may do, and which
        /// session and token this is.
        /// </summary>
        /// <remarks>
        /// The claims are exactly the ones the protocol needs and nothing more. A role in the
        /// token is a statement about the moment it was issued, which is why
        /// <see cref="IAccessChecker"/> exists and reads the row back - the claim is a starting
        /// point, not the answer. The session identifier is what lets a sign-out find the whole
        /// chain and what lets a withdrawal be remembered once for every token the session ever
        /// held; the token's own identifier stays as the standard <c>jti</c>.
        /// </remarks>
        private string CreateAccessToken(
            User user,
            Guid sessionId,
            DateTimeOffset now,
            AuthenticationOptions options)
        {
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(
                [
                    // The account, its role, and the two identifiers that name the session and
                    // the token. Names come from TokenClaimTypes rather than from string
                    // literals so that the half that writes them and the half that reads them
                    // cannot spell one differently - a claim read under the wrong name is not
                    // an error, it is a missing one.
                    new Claim(TokenClaimTypes.Subject, user.Id.ToString()),
                    new Claim(TokenClaimTypes.Name, user.Username),
                    new Claim(TokenClaimTypes.Role, ((int)user.Role).ToString(
                        CultureInfo.InvariantCulture)),
                    new Claim(TokenClaimTypes.TokenId, Guid.NewGuid().ToString()),
                    new Claim(TokenClaimTypes.Session, sessionId.ToString())
                ]),
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = now.Add(options.AccessLifetime).UtcDateTime,
                Issuer = options.Issuer,
                Audience = options.Audience,
                SigningCredentials = _credentials
            };

            return new JwtSecurityTokenHandler().WriteToken(
                new JwtSecurityTokenHandler().CreateToken(descriptor));
        }

        /// <summary>
        /// A refresh token, as entropy rather than as a structure.
        /// </summary>
        /// <remarks>
        /// Base64url of 32 bytes from the cryptographic generator. It carries no claims, so
        /// there is nothing in it to read, forge or partially guess - and since it is looked up
        /// by hash, the only thing that has to be unguessable is the value itself.
        /// </remarks>
        private static string GenerateRefreshToken()
            => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(RefreshTokenByteCount));

        /// <summary>
        /// The stored form of a refresh token. A database that is read in full - a backup, a log,
        /// a replica - then yields nothing that can be presented to this server.
        /// </summary>
        private static string HashToken(string token)
            => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        /// <summary>
        /// Removes stale refresh tokens to prevent the table from growing unbounded.
        /// </summary>
        /// <remarks>
        /// Expired tokens (whose window passed) are removed regardless of whether they were
        /// spent, and revoked tokens that are older than thirty days are removed as well.
        /// The thirty day window preserves a short audit trail without keeping an ever growing
        /// history, and the cleanup is run during issuance and rotation where writes already
        /// happen - cheap enough not to need a separate hosted service.
        /// </remarks>
        private Task CleanupExpiredAsync(DateTimeOffset now, CancellationToken ct)
            => _tokens.CleanupExpiredAsync(now, ct);

        /// <summary>
        /// Cuts a value to what the column holds.
        /// </summary>
        /// <remarks>
        /// These columns record where a session came from and nothing depends on them being
        /// complete, so an oversized value is shortened rather than rejected. Refusing to open
        /// a session because a client sent a very long user agent would be a far worse outcome
        /// than storing the first 255 characters of it.
        /// </remarks>
        private static string? Truncate(string? value, int maxLength)
            => value is null || value.Length <= maxLength
                ? value
                : value[..maxLength];
    }

    /// <summary>
    /// Column widths of the <c>refresh_tokens</c> table, mirrored here so the service that
    /// writes to it does not have to reference the data access layer's configuration types.
    /// </summary>
    /// <remarks>
    /// A duplicate of two constants, and a deliberate one: the alternative is for the token
    /// service to name <c>RefreshTokenConfiguration</c>, which would put a persistence
    /// decision in the middle of the business service. The mapping is the authority and the
    /// values below have to agree with it.
    /// </remarks>
}
