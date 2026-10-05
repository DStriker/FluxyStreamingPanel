namespace Fluxy.Core.Models.GeoIp
{
    /// <summary>
    /// What the GeoIP database knows about one client address.
    /// </summary>
    /// <remarks>
    /// The resolver never throws for a lookup: an address it cannot place - a private one, a
    /// loopback, one missing from the database, or a database that is missing itself - comes
    /// back as <see cref="IsUnknown"/>, and the caller decides what an unknown network means.
    /// Throwing out of a sign-in path would turn a stale database file into an outage.
    /// </remarks>
    public sealed record GeoIpInfo
    {
        /// <summary>Address that was looked up, as the caller reported it.</summary>
        public string? Ip { get; init; }

        /// <summary>Country of the address, ISO 3166-1 alpha-2 upper case, or null when unknown.</summary>
        public string? CountryCode { get; init; }

        /// <summary>Autonomous system number of the address, or null when unknown.</summary>
        public int? AutonomousSystemNumber { get; init; }

        /// <summary>Organization the autonomous system is registered to, or null when unknown.</summary>
        public string? Organization { get; init; }

        /// <summary>Whether the lookup produced nothing usable. Never matched against a list.</summary>
        public bool IsUnknown { get; init; }

        /// <summary>The one answer every failed lookup produces.</summary>
        public static GeoIpInfo Unknown(string? ip) => new() { Ip = ip, IsUnknown = true };
    }
}
