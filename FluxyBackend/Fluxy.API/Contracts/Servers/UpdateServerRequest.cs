namespace Fluxy.API.Contracts.Servers
{
    /// <summary>
    /// What the server form sends to change a server.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is a full write, not a partial patch.</b> The form holds one server, so the body
    /// carries the whole row - the convention the tariff plan, the streams and the transcoding
    /// profile already follow. Every property is nullable only because a record cannot tell a
    /// member the client omitted from one it never wrote: an absent text is refused as the blank
    /// it becomes, an absent <c>clientCount</c> is the <c>0</c> that means unlimited, and an
    /// absent <c>timeshiftOnly</c> is <c>false</c>.
    /// </para>
    /// <para>
    /// <c>isMain</c> is deliberately absent: it is the startup seeder's answer and no request
    /// may promote a row or demote the main one. The main row otherwise accepts every field this
    /// record carries; only <c>DELETE</c> refuses it.
    /// </para>
    /// <para>
    /// No data annotations, for the reason the create request gives: the antiforgery check has
    /// to run before anything judges the body.
    /// </para>
    /// </remarks>
    public sealed record UpdateServerRequest
    {
        /// <summary>Display name. Trimmed before it is stored.</summary>
        public string? Name { get; init; }

        /// <summary>Address the server is reached at: an IPv4 or IPv6 literal.</summary>
        public string? Ip { get; init; }

        /// <summary>Port the server listens on.</summary>
        public int? Port { get; init; }

        /// <summary>Client ceiling. <c>0</c> means unlimited.</summary>
        public int? ClientCount { get; init; }

        /// <summary>Name of the operating system the machine runs.</summary>
        public string? OsName { get; init; }

        /// <summary>Name of the network interface the server is reached through.</summary>
        public string? NetworkInterface { get; init; }

        /// <summary>Measured round-trip latency. <c>0</c> means not measured.</summary>
        public double? Latency { get; init; }

        /// <summary>State of the server, as the name of the enum member.</summary>
        public string? Status { get; init; }

        /// <summary>Guaranteed throughput in kbps. <c>0</c> means unknown.</summary>
        public int? GuaranteedSpeedKbps { get; init; }

        /// <summary>Whether the server only serves timeshifted content.</summary>
        public bool? TimeshiftOnly { get; init; }

        /// <summary>Host name of the machine, or null when it has none.</summary>
        public string? Hostname { get; init; }

        /// <summary>Address of the VPN the server is reached through, or null when there is none.</summary>
        public string? VpnIp { get; init; }

        /// <summary>ISO 3166-1 alpha-2 country codes the server serves; empty for none.</summary>
        public IReadOnlyList<string>? CountryCodes { get; init; }
    }
}
