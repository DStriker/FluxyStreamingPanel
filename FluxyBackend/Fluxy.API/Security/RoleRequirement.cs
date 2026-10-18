using Microsoft.AspNetCore.Authorization;

namespace Fluxy.API.Security
{
    /// <summary>
    /// Demands that the account behind a token holds one exact role.
    /// </summary>
    /// <remarks>
    /// Exact, not <c>AtLeast</c>, and the reason is that this is an entrance rather than a
    /// permission. The role enum is ordered precisely so that a permission check can be
    /// <c>role &gt;= required</c> - a reseller panel may accept an operator, because an operator
    /// holds every right a reseller has. But each of the three sign-in forms is a door to one
    /// area, and an operator arriving through the reseller door is not a lesser version of the
    /// same visitor: they are a different audience arriving at the wrong address, and the form
    /// that let them in is a form for someone else. Each form has its own endpoint and its own
    /// policy, and this requirement is where the two meet.
    /// </remarks>
    public sealed class RoleRequirement : IAuthorizationRequirement
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RoleRequirement"/> class.
        /// </summary>
        /// <param name="role">Role the endpoint accepts, and only that role.</param>
        public RoleRequirement(Fluxy.Core.Models.Users.UserRole role)
        {
            Role = role;
        }

        /// <summary>Role the endpoint accepts.</summary>
        public Fluxy.Core.Models.Users.UserRole Role { get; }
    }
}
