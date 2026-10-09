using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Authentication;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fluxy.Application.Services.Authentication
{
    /// <summary>
    /// Checks submitted credentials against a stored account and issues a session.
    /// </summary>
    /// <remarks>
    /// Every refusal is the same refusal, and that is the design rather than a simplification.
    /// A caller learns one thing from this service: whether these exact credentials work at this
    /// exact form. Whether the name is unknown, the password wrong, the account unconfirmed,
    /// blocked, or simply not the audience this form is for - none of that is distinguishable
    /// from the answer, so the form cannot be used to find out who has an account here or to
    /// confirm that a given name exists.
    ///
    /// That means the role check is not free to be reported separately even though it is
    /// genuinely a different situation. It is applied, and its result is folded into the same
    /// silence.
    ///
    /// The password comparison is also given a cost whether or not there is a row to compare
    /// against. Verifying a BCrypt hash costs about as much as producing one, so skipping it
    /// for an unknown name would make "no such account" answer in a millisecond and "wrong
    /// password" answer in a hundred - which measures out an account list one request at a time.
    /// A throwaway hash is verified instead, so both take the same time.
    /// </remarks>
    public sealed class AuthenticationService : IAuthenticationService
    {
        /// <summary>
        /// A real BCrypt hash of a value nobody knows, used to spend the same time on a request
        /// for an account that does not exist. Computed once per process because hashing it on
        /// every unknown-name request would defeat the purpose of doing it at all.
        /// </summary>
        private static readonly Lazy<string> DecoyHash = new(
            static () => BCrypt.Net.BCrypt.HashPassword(
                Guid.NewGuid().ToString("N"),
                BCrypt.Net.BCrypt.GenerateSalt()),
            LazyThreadSafetyMode.ExecutionAndPublication);

        private readonly FluxyDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly IGeoIpResolver _geoIp;
        private readonly ILoginGuardService _guard;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<AuthenticationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationService"/> class.
        /// </summary>
        /// <param name="context">Context holding the accounts.</param>
        /// <param name="tokenService">Minter of the session tokens.</param>
        /// <param name="geoIp">Resolver that places the client address on the map.</param>
        /// <param name="guard">Enforcer of the account's allow lists.</param>
        /// <param name="environment">Host environment, for the Development bypass of local networks.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public AuthenticationService(
            FluxyDbContext context,
            ITokenService tokenService,
            IGeoIpResolver geoIp,
            ILoginGuardService guard,
            IHostEnvironment environment,
            ILogger<AuthenticationService> logger)
        {
            _context = context;
            _tokenService = tokenService;
            _geoIp = geoIp;
            _guard = guard;
            _environment = environment;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<AuthenticationOutcome> AuthenticateAsync(
            NewCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            // Trimmed but not folded: the column is compared case sensitively, so `Admin` and
            // `admin` are two different names and a person who mistypes the case gets the same
            // refusal as one who mistypes the password.
            var username = credentials.Username?.Trim() ?? string.Empty;
            var password = credentials.Password ?? string.Empty;

            if (username.Length == 0)
            {
                // Spend the same time as a normal comparison so an empty name cannot be
                // distinguished by timing from any other refusal.
                _ = BCrypt.Net.BCrypt.Verify(password, DecoyHash.Value);
                return Refused();
            }

            var user = await _context.Users
                .AsNoTracking()
                .Include(entry => entry.Group)
                .FirstOrDefaultAsync(entry => entry.Username == username, cancellationToken);

            if (user is null)
            {
                // The account does not exist. The password comparison still runs against a
                // throwaway hash to keep the response time constant regardless of whether the
                // name exists.
                _ = BCrypt.Net.BCrypt.Verify(password, DecoyHash.Value);
                return Refused();
            }

            // Compare against the real hash. A blocked or unconfirmed account takes the same
            // time to compare as a live one; the decision is made after the comparison.
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                // One refusal for a name that is unknown and for a password that is wrong. Not
                // logged individually: someone trying names is the expected case, and a log full
                // of them is both noise and an account list.
                return Refused();
            }

            // The level and the state are both facts about the account's group as much as
            // about the row, so both are read from the model, where the two halves have
            // already been combined - a blocked group blocks its members here for exactly the
            // same reason a blocked row does, and there is one rule rather than two.
            var account = user.ToModel();

            if (account.Status is not UserStatus.Registered)
            {
                _logger.LogInformation(
                    "Refused a sign-in for {Username}: the account is {Status}.",
                    username,
                    account.Status);

                return Refused();
            }

            // Exact equality, not `>=`. The role enum is ordered so that a higher level includes
            // the rights of a lower one, and that ordering is the right rule for deciding what a
            // token may do - but the wrong one here. Each form is an entrance, and an operator
            // signing in at the reseller form would be a cross-level sign-in, which is not
            // something this installation offers.
            if (account.Role != credentials.RequiredRole)
            {
                _logger.LogInformation(
                    "Refused a sign-in for {Username}: the account is {Role} and that form accepts " +
                    "{RequiredRole}.",
                    username,
                    account.Role,
                    credentials.RequiredRole);

                return Refused();
            }

            // Runs after the password, the status and the role, and that order is the point. A
            // guard verdict before the password would let a caller without the password learn
            // whether a network is allowed, and a verdict folded into anything but the one
            // refusal would tell a wrong password from a wrong network.
            if (user.GeoProtectionEnabled && !BypassesGuard(credentials.ClientAddress))
            {
                var geo = await _geoIp.ResolveAsync(credentials.ClientAddress, cancellationToken);

                if (!await _guard.IsAllowedAsync(user.Id, geo, cancellationToken))
                {
                    _logger.LogInformation(
                        "Refused a sign-in for {Username}: the network it came from is not on " +
                        "the account's allow lists.",
                        username);

                    return Refused();
                }
            }

            var tokens = await _tokenService.IssueAsync(
                account,
                credentials.ClientAddress,
                credentials.UserAgent,
                cancellationToken);

            return new AuthenticationOutcome
            {
                Status = AuthenticationStatus.Authenticated,
                UserId = user.Id,
                Username = user.Username,
                Role = account.Role,
                Tokens = tokens
            };
        }

        /// <summary>
        /// The one answer every failure produces.
        /// </summary>
        private static AuthenticationOutcome Refused()
            => new() { Status = AuthenticationStatus.InvalidCredentials };

        /// <summary>
        /// Whether the guard is skipped for this address. Loopback and private networks carry
        /// no GeoIP data by definition, so in Development - where every client is one - the
        /// guard would refuse its own developer. Outside Development there is no bypass: a
        /// local address on a public host is not the developer, it is a misconfiguration.
        /// </summary>
        private bool BypassesGuard(string? clientAddress)
        {
            if (!_environment.IsDevelopment()
                || !GeoIp.LoginGuardPolicy.IsLocalAddress(clientAddress))
            {
                return false;
            }

            _logger.LogWarning(
                "The login guard was bypassed for local address {ClientAddress} because this " +
                "host runs in Development.",
                clientAddress);

            return true;
        }
    }
}
