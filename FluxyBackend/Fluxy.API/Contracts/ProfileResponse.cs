using Fluxy.Core.Models.Users;

namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What a profile page is shown about the account behind the presented token.
    /// </summary>
    /// <remarks>
    /// It exists because <see cref="SessionResponse"/> answers a different question: that one
    /// reports the claims of the token, and the email address is deliberately not one of them -
    /// a claim is a copy that lives for as long as the token does, and an address that can be
    /// changed must not be readable from a copy. This reads the row.
    /// </remarks>
    public sealed record ProfileResponse
    {
        /// <summary>Login name of the account.</summary>
        public required string Username { get; init; }

        /// <summary>Email address of the account.</summary>
        public required string Email { get; init; }

        /// <summary>Access level of the account, as the name of the enum member.</summary>
        public required string Role { get; init; }

        /// <summary>
        /// Everything the account's group allows it to do, each as the name a client sends
        /// back: <c>viewClients</c>, <c>editGroups</c>-style camelCase, in catalog order.
        /// </summary>
        /// <remarks>
        /// An empty array for an account whose group grants nothing, which is every client and
        /// reseller today. This is a hint about which controls to draw rather than an
        /// authority: the server refuses on its own, in the service, so a caller that ignored
        /// this field would see buttons that do not work and no extra access.
        ///
        /// Order comes from <c>UserPermissionCatalog.All</c> rather than from the row order,
        /// because the rows are per-grant and their order is whatever the index produced -
        /// a list a client renders in two places must not rearrange itself between them.
        /// </remarks>
        public required string[] Permissions { get; init; }

        /// <summary>
        /// IANA identifier of the chosen display time zone, or null when the visitor has not
        /// chosen one and it should be read from their browser.
        /// </summary>
        public string? TimeZone { get; init; }

        /// <summary>
        /// Whether a sign-in from a network that is not allowed is refused with the same
        /// answer a wrong password gets.
        /// </summary>
        public required bool GeoProtectionEnabled { get; init; }

        /// <summary>
        /// Whether a refresh from an address other than the session's own ends the session.
        /// </summary>
        public required bool BindSessionToIp { get; init; }

        /// <summary>Allowed addresses, exact or CIDR. Empty means this list does not restrict.</summary>
        public required string[] AllowedIps { get; init; }

        /// <summary>Allowed country, ISO 3166-1 alpha-2, or null when not restricted.</summary>
        public string? AllowedCountry { get; init; }

        /// <summary>Allowed provider by autonomous system number, or null when not restricted.</summary>
        public int? AllowedAutonomousSystemNumber { get; init; }
    }
}
