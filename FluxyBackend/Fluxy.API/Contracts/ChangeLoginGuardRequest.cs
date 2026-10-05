namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What a signed-in visitor submits to change the login guard of their account.
    /// </summary>
    /// <remarks>
    /// The limits are the contract, not a hint: at most five addresses, one country, one
    /// provider. The service enforces them again, because a body is a suggestion until it is
    /// checked, but a client that renders five address fields never has to guess the sixth
    /// one's fate.
    ///
    /// The JSON spelling is the camelCase of the same names C# uses - one fact with two
    /// spellings is a typo waiting to be read as a missing field.
    /// </remarks>
    public sealed class ChangeLoginGuardRequest
    {
        /// <summary>Password the account signs in with, proving the session is the owner's.</summary>
        public string? CurrentPassword { get; init; }

        /// <summary>
        /// Whether a sign-in from a network that is not allowed is refused with the same
        /// answer a wrong password gets.
        /// </summary>
        public bool GeoProtectionEnabled { get; init; }

        /// <summary>
        /// Whether a refresh from an address other than the session's own ends the session.
        /// </summary>
        public bool BindSessionToIp { get; init; }

        /// <summary>Allowed addresses, exact or CIDR, IPv4 or IPv6. At most five.</summary>
        public string[]? AllowedIps { get; init; }

        /// <summary>Allowed country, ISO 3166-1 alpha-2, or null when not restricted.</summary>
        public string? AllowedCountry { get; init; }

        /// <summary>Allowed provider by autonomous system number, or null when not restricted.</summary>
        public int? AllowedAutonomousSystemNumber { get; init; }
    }
}
