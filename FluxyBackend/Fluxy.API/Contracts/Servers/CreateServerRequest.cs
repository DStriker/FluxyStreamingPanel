namespace Fluxy.API.Contracts.Servers
{
    /// <summary>
    /// What the server form sends to add a server.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No data annotations on purpose: framework validation runs before the action, so a body
    /// that failed one would never reach the antiforgery check. Every field is checked in the
    /// service after the CSRF pair has been accepted, and the five the row cannot do without
    /// are spellable as null here only because a missing one is the same refusal as a blank one.
    /// </para>
    /// <para>
    /// <c>status</c> travels as the name of the enum member in lower or upper case
    /// (<c>active</c>, <c>Active</c>), the convention the streams use, and an absent value means
    /// <c>active</c>. The three numeric sentinels are stated rather than hidden: an absent
    /// <c>clientCount</c> becomes <c>0</c> (unlimited), an absent <c>guaranteedSpeedKbps</c>
    /// becomes <c>0</c> (unknown), and latency is not a create field at all - a new row starts
    /// at <c>0</c>, meaning not measured.
    /// </para>
    /// <para>
    /// <c>countryCodes</c> is a JSON array of ISO 3166-1 alpha-2 codes, the shape the stored
    /// <c>text[]</c> column keeps and the frontend multi-select sends.
    /// </para>
    /// </remarks>
    public sealed record CreateServerRequest
    {
        /// <summary>Display name. Trimmed before it is stored.</summary>
        public string? Name { get; init; }

        /// <summary>Address the server is reached at: an IPv4 or IPv6 literal.</summary>
        public string? Ip { get; init; }

        /// <summary>Port the server listens on.</summary>
        public int? Port { get; init; }

        /// <summary>Client ceiling, or null for unlimited.</summary>
        public int? ClientCount { get; init; }

        /// <summary>Name of the operating system the machine runs.</summary>
        public string? OsName { get; init; }

        /// <summary>Name of the network interface the server is reached through.</summary>
        public string? NetworkInterface { get; init; }

        /// <summary>State the server starts in, as the name of the enum member, or null for active.</summary>
        public string? Status { get; init; }

        /// <summary>Guaranteed throughput in kbps, or null for unknown.</summary>
        public int? GuaranteedSpeedKbps { get; init; }

        /// <summary>Whether the server only serves timeshifted content.</summary>
        public bool? TimeshiftOnly { get; init; }

        /// <summary>Host name of the machine, or null when it has none.</summary>
        public string? Hostname { get; init; }

        /// <summary>Address of the VPN the server is reached through, or null when there is none.</summary>
        public string? VpnIp { get; init; }

        /// <summary>ISO 3166-1 alpha-2 country codes the server serves, or null for none.</summary>
        public IReadOnlyList<string>? CountryCodes { get; init; }
    }
}
