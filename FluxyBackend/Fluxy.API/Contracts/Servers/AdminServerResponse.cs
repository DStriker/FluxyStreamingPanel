namespace Fluxy.API.Contracts.Servers
{
    /// <summary>
    /// One server as the API answers with it, in both the list and the detail.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A server has no relations to leave out of a row, so the two answers share one shape -
    /// the shape <c>AdminTranscodingProfileResponse</c> already uses. <c>isMain</c> travels
    /// because the table draws the main row differently and its delete button is disabled; the
    /// client never has to compare an identifier against a constant of the API.
    /// </para>
    /// <para>
    /// <c>status</c> is the name of the enum member (<c>Disabled</c>, <c>Active</c>), the
    /// spelling the stream responses use. <c>guaranteedSpeedKbps</c> carries its unit in the
    /// name; the API never converts it, and the form is what shows Mbps.
    /// </para>
    /// </remarks>
    public sealed record AdminServerResponse
    {
        /// <summary>Identifier of the server, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Display name, unique across the installation.</summary>
        public required string Name { get; init; }

        /// <summary>Address the server is reached at.</summary>
        public required string Ip { get; init; }

        /// <summary>Port the server listens on.</summary>
        public required int Port { get; init; }

        /// <summary>Client ceiling, or <c>0</c> for unlimited.</summary>
        public required int ClientCount { get; init; }

        /// <summary>Name of the operating system the machine runs.</summary>
        public required string OsName { get; init; }

        /// <summary>Name of the network interface the server is reached through.</summary>
        public required string NetworkInterface { get; init; }

        /// <summary>Measured round-trip latency, or <c>0</c> when not measured.</summary>
        public required double Latency { get; init; }

        /// <summary>State of the server, as the name of the enum member.</summary>
        public required string Status { get; init; }

        /// <summary>Guaranteed throughput in kbps, or <c>0</c> when unknown.</summary>
        public required int GuaranteedSpeedKbps { get; init; }

        /// <summary>Whether the server only serves timeshifted content.</summary>
        public required bool TimeshiftOnly { get; init; }

        /// <summary>Host name of the machine, or null when it has none.</summary>
        public string? Hostname { get; init; }

        /// <summary>Address of the VPN the server is reached through, or null when there is none.</summary>
        public string? VpnIp { get; init; }

        /// <summary>Country codes the server serves, upper case and in order; empty for none.</summary>
        public required IReadOnlyList<string> CountryCodes { get; init; }

        /// <summary>Whether this is the main server, which cannot be deleted.</summary>
        public required bool IsMain { get; init; }

        /// <summary>When the row was written.</summary>
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
