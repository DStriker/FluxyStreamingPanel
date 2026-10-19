using Fluxy.Core.Authentication.Utils;
using Microsoft.AspNetCore.Authorization;

namespace Fluxy.API.Security
{
    /// <summary>
    /// Decides a <see cref="RoleRequirement"/> against the account's current row, not against
    /// the token.
    /// </summary>
    /// <remarks>
    /// Two things have to agree before a request proceeds: the role the token was signed with,
    /// and the role the account holds now. A mismatch means the account changed after the token
    /// was issued, and there is no safe reading of that - the token could belong to a session
    /// opened before a demotion, or to a session that should have ended at a block. Either way
    /// the token is stale and the answer is no.
    ///
    /// The requirement is also checked against the row even when the claims match, so a role
    /// that was changed in the database cannot be honoured on the strength of a token that
    /// predates the change. There is no caching here: a cached answer has a lifetime, and a
    /// stale one is exactly the failure being refused.
    /// </remarks>
    public sealed class RoleRequirementHandler : AuthorizationHandler<RoleRequirement>
    {
        private readonly RequestStandingReader _standing;
        private readonly ILogger<RoleRequirementHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RoleRequirementHandler"/> class.
        /// </summary>
        /// <param name="standing">Reader of the current state of an account, once per request.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public RoleRequirementHandler(
            RequestStandingReader standing,
            ILogger<RoleRequirementHandler> logger)
        {
            _standing = standing;
            _logger = logger;
        }

        /// <inheritdoc />
        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            RoleRequirement requirement)
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                // Left to the authentication handler, which answers a different status and a
                // different code: there being no token at all is not the same problem as
                // holding a token that is no longer good.
                return;
            }

            if (await _standing.ReadAsync(context.User) is not { } found)
            {
                return;
            }

            var (userId, standing) = found;

            if (!standing.IsActive)
            {
                _logger.LogInformation(
                    "Refused a request from account {UserId}: it is {Status}.",
                    userId,
                    standing.Status?.ToString() ?? "missing");

                return;
            }

            var claimedRole = ReadRole(context.User);

            if (claimedRole is not { } roleFromToken)
            {
                _logger.LogWarning(
                    "A token for account {UserId} carried no usable role claim, so the request " +
                    "was refused.",
                    userId);

                return;
            }

            if (roleFromToken != standing.Role)
            {
                _logger.LogInformation(
                    "Refused a request from account {UserId}: its token claims {TokenRole} while " +
                    "the account holds {ActualRole}.",
                    userId,
                    roleFromToken,
                    standing.Role);

                return;
            }

            if (standing.Role != requirement.Role)
            {
                _logger.LogInformation(
                    "Refused a request from account {UserId} in role {Role}: that endpoint accepts " +
                    "{RequiredRole} only.",
                    userId,
                    standing.Role,
                    requirement.Role);

                return;
            }

            context.Succeed(requirement);
        }

        /// <summary>
        /// The role the token carries, or null when it carries none or carries something that is
        /// not a role.
        /// </summary>
        private static Fluxy.Core.Users.Enums.UserRole? ReadRole(System.Security.Claims.ClaimsPrincipal user)
        {
            var value = user.FindFirst(TokenClaimTypes.Role)?.Value
                ?? user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            // A claim naming a value that is not a role is treated as no role at all, rather
            // than as the nearest member. Casting an undefined number into the enum would make
            // a token signed with a nonsense role pass a check it has no business passing.
            return int.TryParse(value, out var role)
                && Enum.IsDefined(typeof(Fluxy.Core.Users.Enums.UserRole), role)
                ? (Fluxy.Core.Users.Enums.UserRole)role
                : null;
        }
    }
}
