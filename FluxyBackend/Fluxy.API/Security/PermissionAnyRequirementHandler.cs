using Fluxy.Core.Users.Utils;
using Microsoft.AspNetCore.Authorization;

namespace Fluxy.API.Security
{
    /// <summary>
    /// Decides a <see cref="PermissionAnyRequirement"/> against the account's current row.
    /// </summary>
    /// <remarks>
    /// The same shape as <see cref="PermissionRequirementHandler"/> on purpose: one read of the
    /// account and its group, no caching, and a refusal that is logged rather than explained to
    /// the caller - what the caller is told is decided in <c>ApiAuthorizationResultHandler</c>.
    ///
    /// It re-checks <c>IsActive</c> as well, even though every permission policy also carries a
    /// <see cref="RoleRequirement"/> that would refuse the same request for the same reason: the
    /// two handlers may run in either order, and a permission of a blocked account must not
    /// succeed on the strength of a check that has not run yet.
    /// </remarks>
    public sealed class PermissionAnyRequirementHandler : AuthorizationHandler<PermissionAnyRequirement>
    {
        private readonly RequestStandingReader _standing;
        private readonly ILogger<PermissionAnyRequirementHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PermissionAnyRequirementHandler"/> class.
        /// </summary>
        /// <param name="standing">Reader of the current state of an account, once per request.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public PermissionAnyRequirementHandler(
            RequestStandingReader standing,
            ILogger<PermissionAnyRequirementHandler> logger)
        {
            _standing = standing;
            _logger = logger;
        }

        /// <inheritdoc />
        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionAnyRequirement requirement)
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
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

            // Any one of the named permissions is enough: the caller may read the conversations
            // of some role, and which role is decided by the service once it has the row.
            if (!requirement.Permissions.Any(standing.Grants))
            {
                _logger.LogInformation(
                    "Refused a request from account {UserId} in role {Role}: its group holds none " +
                    "of {Permissions}.",
                    userId,
                    standing.Role,
                    string.Join(
                        ", ",
                        requirement.Permissions.Select(UserPermissionCatalog.NameOf)));

                return;
            }

            context.Succeed(requirement);
        }
    }
}
