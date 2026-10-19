using Fluxy.Core.Models.Authentication;
using Fluxy.Core.Users.Enums;
using System.Security.Claims;

namespace Fluxy.API.Controllers.Support
{
    /// <summary>
    /// Reads the identities a Fluxy access token carries out of the principal it arrived in.
    /// </summary>
    /// <remarks>
    /// Every reader lives here rather than on each controller because the claim names are the
    /// contract of <see cref="TokenClaimTypes"/>: a second copy of "find <c>sub</c> and parse it
    /// as a guid" is a second place that has to be found the day the claim is spelled
    /// differently. The methods answer <c>null</c> rather than throwing for a claim the token
    /// does not carry - the authorization layer has already established that the request is
    /// authenticated, so a missing subject is a token this application would not have minted,
    /// which is a fact for the caller to answer about, not a reason to fail.
    /// </remarks>
    internal static class ClaimsExtensions
    {
        /// <summary>The account the token was minted for, from <c>sub</c>.</summary>
        public static Guid? UserId(this ClaimsPrincipal principal)
            => Guid.TryParse(principal.FindFirst(TokenClaimTypes.Subject)?.Value, out var userId)
                ? userId
                : null;

        /// <summary>The session the token belongs to, from <c>sid</c>.</summary>
        public static Guid? SessionId(this ClaimsPrincipal principal)
            => Guid.TryParse(principal.FindFirst(TokenClaimTypes.Session)?.Value, out var session)
                ? session
                : null;

        /// <summary>
        /// The subject claim as the token spells it, for a caller that reports rather than
        /// looks up - the identifier in a log line or in <c>session</c>'s own answer.
        /// </summary>
        public static string? Subject(this ClaimsPrincipal principal)
            => principal.FindFirst(TokenClaimTypes.Subject)?.Value;

        /// <summary>The name the token was minted with, falling back to the identity's own.</summary>
        public static string? Name(this ClaimsPrincipal principal)
            => principal.FindFirst(TokenClaimTypes.Name)?.Value ?? principal.Identity?.Name;

        /// <summary>
        /// The role the token was minted with. A token that carries none defaults to the most
        /// limited role rather than to none at all: every caller of this method already holds a
        /// session, and the role is used to describe that session, not to grant anything with.
        /// </summary>
        public static UserRole Role(this ClaimsPrincipal principal)
        {
            var value = principal.FindFirst(TokenClaimTypes.Role)?.Value;

            return int.TryParse(value, out var role) && Enum.IsDefined(typeof(UserRole), role)
                ? (UserRole)role
                : UserRole.Client;
        }
    }
}
