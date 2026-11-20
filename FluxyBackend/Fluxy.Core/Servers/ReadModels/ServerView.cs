using Fluxy.Core.Servers.Enums;

namespace Fluxy.Core.Servers.ReadModels
{
    /// <summary>
    /// One server as the API answers with it: every column the table draws and the form opens
    /// on.
    /// </summary>
    /// <remarks>
    /// The list and the detail answer the same shape, because a server has no relations to leave
    /// out of a row - the shape <c>TranscodingProfileView</c> already uses. <see cref="IsMain"/>
    /// travels because the table draws the main row differently and the delete button is
    /// disabled on it; the transport never has to re-derive it from the identifier's value.
    /// </remarks>
    public sealed record ServerView
    {
        /// <summary>Identifier of the server.</summary>
        public required Guid Id { get; init; }

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

        /// <summary>Whether the server is active or disabled.</summary>
        public required ServerStatus Status { get; init; }

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

        /// <summary>When the row was written. No endpoint changes it; it is a sort column.</summary>
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
