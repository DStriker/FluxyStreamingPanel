using System.Security.Claims;
using Fluxy.Core.Users.Enums;
using Fluxy.API.Security;

namespace Fluxy.API.Controllers.Support
{
    /// <summary>
    /// Reads the permissions the account behind a request holds, for the per-target check a
    /// service makes after the coarse policy let the request in.
    /// </summary>
    /// <remarks>
    /// It is one method on one type rather than a private helper on each controller because
    /// two controllers ask the same question of the same object, and the answer has to be
    /// one answer: the policies on their way in already read the account through the very
    /// same <see cref="RequestStandingReader"/>, which memoizes it per request, so asking
    /// again costs no second query. An account whose standing could not be established is
    /// granted nothing rather than everything - the safe reading of "we do not know who
    /// this is", and one the action policies would already have refused before an action
    /// was entered.
    /// </remarks>
    public static class StandingExtensions
    {
        private static readonly IReadOnlySet<UserPermission> NoGrants = new HashSet<UserPermission>();

        /// <summary>
        /// The permissions the account behind <paramref name="user"/> holds, or an empty set
        /// when no standing could be established.
        /// </summary>
        /// <param name="standing">Reader shared with the authorization handlers.</param>
        /// <param name="user">The claims of the request being served.</param>
        public static async Task<IReadOnlySet<UserPermission>> ReadGrantsAsync(
            this RequestStandingReader standing,
            ClaimsPrincipal user)
        {
            if (await standing.ReadAsync(user) is { } found)
            {
                return found.Standing.Permissions;
            }

            return NoGrants;
        }
    }
}

