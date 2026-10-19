using Microsoft.AspNetCore.Authorization;

namespace Fluxy.API.Security
{
    /// <summary>
    /// Decides a <see cref="PermissionRequirement"/> against the account's current row.
    /// </summary>
    /// <remarks>
    /// The same shape as <see cref="RoleRequirementHandler"/> on purpose: one read of the
    /// account and its group, no caching, and a refusal that is logged rather than explained to
    /// the caller - what the caller is told is decided in <c>ApiAuthorizationResultHandler</c>,
    /// which is the one place that knows which of the requirements of a merged policy failed.
    ///
    /// It re-checks <c>IsActive</c> as well, even though every permission policy also carries a
    /// <see cref="RoleRequirement"/> that would refuse the same request for the same reason. The
    /// two handlers may run in either order, and a permission of a blocked account must not
    /// succeed on the strength of a check that has not run yet.
    ///
    /// A requirement naming several permissions succeeds when the group holds any one of them.
    /// That is what makes a coarse "may this caller touch accounts at all" gate expressible
    /// while the account the request is about is still unknown.
    /// </remarks>
    public sealed class PermissionRequirementHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly RequestStandingReader _standing;
        private readonly ILogger<PermissionRequirementHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PermissionRequirementHandler"/> class.
        /// </summary>
        /// <param name="standing">Reader of the current state of an account, once per request.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public PermissionRequirementHandler(
            RequestStandingReader standing,
            ILogger<PermissionRequirementHandler> logger)
        {
            _standing = standing;
            _logger = logger;
        }

        /// <inheritdoc />
        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
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

            // Any one of the named permissions is enough: a requirement naming three <c>view*</c>
            // keys is asking whether this caller may read accounts of *some* role, not whether
            // they may read all three kinds.
            if (!requirement.Permissions.Any(standing.Grants))
            {
                _logger.LogInformation(
                    "Refused a request from account {UserId} in role {Role}: its group holds none " +
                    "of {Permissions}.",
                    userId,
                    standing.Role,
                    string.Join(
                        ", ",
                        requirement.Permissions.Select(
                            Fluxy.Core.Users.Utils.UserPermissionCatalog.NameOf)));

                return;
            }

            context.Succeed(requirement);
        }
    }
}
