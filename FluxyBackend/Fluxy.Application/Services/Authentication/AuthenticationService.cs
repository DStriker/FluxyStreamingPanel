using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Authentication;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
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
        private readonly ILogger<AuthenticationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationService"/> class.
        /// </summary>
        /// <param name="context">Context holding the accounts.</param>
        /// <param name="tokenService">Minter of the session tokens.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public AuthenticationService(
            FluxyDbContext context,
            ITokenService tokenService,
            ILogger<AuthenticationService> logger)
        {
            _context = context;
            _tokenService = tokenService;
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

            if (user.Status is not UserStatus.Registered)
            {
                _logger.LogInformation(
                    "Refused a sign-in for {Username}: the account is {Status}.",
                    username,
                    user.Status);

                return Refused();
            }

            // Exact equality, not `>=`. The role enum is ordered so that a higher level includes
            // the rights of a lower one, and that ordering is the right rule for deciding what a
            // token may do - but the wrong one here. Each form is an entrance, and an operator
            // signing in at the reseller form would be a cross-level sign-in, which is not
            // something this installation offers.
            if (user.Role != credentials.RequiredRole)
            {
                _logger.LogInformation(
                    "Refused a sign-in for {Username}: the account is {Role} and that form accepts " +
                    "{RequiredRole}.",
                    username,
                    user.Role,
                    credentials.RequiredRole);

                return Refused();
            }

            var tokens = await _tokenService.IssueAsync(
                user.ToModel(),
                credentials.ClientAddress,
                credentials.UserAgent,
                cancellationToken);

            return new AuthenticationOutcome
            {
                Status = AuthenticationStatus.Authenticated,
                UserId = user.Id,
                Username = user.Username,
                Role = user.Role,
                Tokens = tokens
            };
        }

        /// <summary>
        /// The one answer every failure produces.
        /// </summary>
        private static AuthenticationOutcome Refused()
            => new() { Status = AuthenticationStatus.InvalidCredentials };
    }
}
