namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What the GeoIP database knows about the network the caller arrived from.
    /// </summary>
    /// <remarks>
    /// Exists so a profile page can offer "allow my current network" without asking the
    /// visitor to look up their own address, country and provider. Best effort: every field
    /// but the address may be null when the database does not know it, and the page has to
    /// render that rather than treat it as "no restriction".
    /// </remarks>
    public sealed record GeoLookupResponse
    {
        /// <summary>Address the request came from, as this server saw it.</summary>
        public string? Ip { get; init; }

        /// <summary>Country of the address, ISO 3166-1 alpha-2, or null when unknown.</summary>
        public string? CountryCode { get; init; }

        /// <summary>Autonomous system number of the address, or null when unknown.</summary>
        public int? AutonomousSystemNumber { get; init; }

        /// <summary>Organization the autonomous system is registered to, or null when unknown.</summary>
        public string? Organization { get; init; }
    }
}
