using Fluxy.Core.Users.Enums;

namespace Fluxy.API.Security
{
    /// <summary>
    /// Demands that the account behind a token holds <b>any one</b> of several permission keys in
    /// its group.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A mirror of <see cref="PermissionRequirement"/> with the "any of" rule stated in the type's
    /// own name, used by the endpoints whose audience is wider than one grant: the ticket surface
    /// is opened either by an operator who reads accounts of one role or by an operator who reads
    /// accounts of another, and a caller holding <c>viewClients</c> may read a client's
    /// conversations without holding <c>viewResellers</c>.
    /// </para>
    /// <para>
    /// It exists beside the original rather than replacing it because the two requirements mean
    /// different things to the reading code: <see cref="PermissionRequirement"/> is the historical
    /// name for "this set, any of them", and a new controller declaring
    /// <c>ViewTicketsPolicy</c> should say <i>any</i> in the place a reader looks first. Which
    /// accounts an operator may actually reach is still decided inside the service, against
    /// <c>UserPermissionCatalog.ViewFor</c>/<c>EditFor</c>.
    /// </para>
    /// </remarks>
    public sealed class PermissionAnyRequirement : Microsoft.AspNetCore.Authorization.IAuthorizationRequirement
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PermissionAnyRequirement"/> class.
        /// </summary>
        /// <param name="permissions">
        /// Permissions the endpoint demands, any one of which is enough. At least one must be
        /// named - a requirement with nothing to ask for would succeed for everybody.
        /// </param>
        public PermissionAnyRequirement(params UserPermission[] permissions)
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
        public IReadOnlyList<UserPermission> Permissions { get; }
    }
}
