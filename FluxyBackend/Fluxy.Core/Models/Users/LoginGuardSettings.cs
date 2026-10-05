namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// The login guard of one account: the two switches and the three allow lists.
    /// </summary>
    /// <remarks>
    /// A projection of <see cref="User"/> plus its rules, and deliberately a separate shape:
    /// the profile endpoint renders this, the sign-in path reads the rules straight from the
    /// store, and neither hands the other its storage details.
    /// </remarks>
    public sealed record LoginGuardSettings
    {
        /// <summary>
        /// Whether a sign-in from a network that is not allowed is refused with the same
        /// answer a wrong password gets.
        /// </summary>
        public bool GeoProtectionEnabled { get; init; }

        /// <summary>
        /// Whether a refresh from an address other than the session's own ends the session.
        /// </summary>
        public bool BindSessionToIp { get; init; }

        /// <summary>Allowed addresses, exact or CIDR. Empty means this list does not restrict.</summary>
        public string[] AllowedIps { get; init; } = [];

        /// <summary>Allowed country, ISO 3166-1 alpha-2, or null when not restricted.</summary>
        public string? AllowedCountry { get; init; }

        /// <summary>Allowed provider by autonomous system number, or null when not restricted.</summary>
        public int? AllowedAutonomousSystemNumber { get; init; }
    }
}
