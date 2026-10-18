using Microsoft.AspNetCore.Authorization;

namespace Fluxy.API.Security
{
    /// <summary>
    /// Demands that the account behind a token holds one or more permission keys in its group.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The second, finer gate - the role decides which area of the application a caller is in,
    /// and a permission decides what they may do once there. The two are deliberately separate
    /// rather than one folded into the other: a role is an audience and is checked on every
    /// request of that area, while a permission is an operation and grows over time without
    /// needing a new role or a new policy for every addition.
    /// </para>
    /// <para>
    /// Several permissions may be named, in which case <b>any one of them</b> satisfies the
    /// requirement. That is what the two account policies use: one endpoint serves clients,
    /// resellers and administrators alike, so which of the three <c>view*</c> keys it needs
    /// depends on the account the request is about - a fact the policy cannot know before the
    /// request is being handled. Such a policy is therefore a coarse gate only, and the
    /// decision about a specific account is taken inside the action.
    /// </para>
    /// <para>
    /// It is checked against the account's current row for exactly the reason
    /// <see cref="RoleRequirement"/> is: the permission a token was minted under is not a claim
    /// the token even carries, and a grant withdrawn a second ago has to be gone now rather
    /// than in five minutes.
    /// </para>
    /// </remarks>
    public sealed class PermissionRequirement : IAuthorizationRequirement
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PermissionRequirement"/> class.
        /// </summary>
        /// <param name="permissions">
        /// Permissions the endpoint demands, any one of which is enough. At least one must be
        /// named - a requirement with nothing to ask for would succeed for everybody, which is
        /// the one answer it exists to prevent.
        /// </param>
        public PermissionRequirement(params Fluxy.Core.Models.Users.UserPermission[] permissions)
        {
            if (permissions is null || permissions.Length is 0)
            {
                throw new ArgumentException(
                    "A permission requirement has to demand at least one permission.",
                    nameof(permissions));
            }

            Permissions = permissions;
        }

        /// <summary>Permissions the endpoint demands; any one of them satisfies the requirement.</summary>
        public IReadOnlyList<Fluxy.Core.Models.Users.UserPermission> Permissions { get; }
    }
}
