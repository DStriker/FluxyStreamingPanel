namespace Fluxy.Core.Servers.Utils
{
    /// <summary>
    /// Bounds of the values a server row stores.
    /// </summary>
    /// <remarks>
    /// The limits live in this layer rather than beside the table configuration so a service can
    /// judge a request without referencing the data access assembly. The configuration uses the
    /// same constants, so a column width and the rule that refuses an oversized value cannot
    /// drift apart.
    /// </remarks>
    public static class ServerLimits
    {
        /// <summary>Fewest characters a server name may carry.</summary>
        public const int NameMinLength = 1;

        /// <summary>Longest display name a server may carry.</summary>
        public const int NameMaxLength = 200;

        /// <summary>
        /// Longest textual address a server may carry. Enough for the longest IPv6 literal in
        /// its fully expanded form.
        /// </summary>
        public const int IpMaxLength = 45;

        /// <summary>Lowest port a server may listen on.</summary>
        public const int PortMin = 1;

        /// <summary>Highest port a server may listen on.</summary>
        public const int PortMax = 65535;

        /// <summary>Lowest client ceiling a server may state; <c>0</c> means unlimited.</summary>
        public const int ClientCountMin = 0;

        /// <summary>Highest client ceiling a server may state.</summary>
        public const int ClientCountMax = 10000;

        /// <summary>Longest operating system name a server may carry.</summary>
        public const int OsNameMaxLength = 100;

        /// <summary>Longest network interface name a server may carry.</summary>
        public const int NetworkInterfaceMaxLength = 50;

        /// <summary>Lowest latency a server may report; <c>0</c> means not measured.</summary>
        public const double LatencyMin = 0;

        /// <summary>Lowest guaranteed throughput a server may state; <c>0</c> means unknown.</summary>
        public const int GuaranteedSpeedMinKbps = 0;

        /// <summary>Highest guaranteed throughput a server may state, in kbps.</summary>
        public const int GuaranteedSpeedMaxKbps = 1_000_000;

        /// <summary>Longest host name a server may carry.</summary>
        public const int HostnameMaxLength = 253;

        /// <summary>How many characters one ISO 3166-1 alpha-2 country code carries.</summary>
        public const int CountryCodeLength = 2;

        /// <summary>
        /// Most country codes one server may serve. The current ISO 3166-1 list holds 249 codes,
        /// so a row naming more than this names something that is not a country.
        /// </summary>
        public const int CountryCodeCountMax = 249;

        /// <summary>
        /// The index making a server name unique for the installation. Matched by exact name
        /// when the database refuses the write, because the service's own pre-check is only the
        /// friendly path - the index is the guarantee.
        /// </summary>
        public const string NameIndexName = "IX_servers_name";
    }
}
