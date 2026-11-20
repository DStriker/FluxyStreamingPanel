using Fluxy.Core.Servers.Enums;

namespace Fluxy.Core.Servers.Commands
{
    /// <summary>
    /// The values a create or an update writes to a server row.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Separate from <see cref="Models.Server"/> because a write does not name what is the row's
    /// identity - its identifier, the audit stamps and the main-server flag - and because the
    /// service has already trimmed, defaulted and validated every value by the time a row is
    /// written. The repository therefore writes exactly what it is given instead of repeating
    /// the rules.
    /// </para>
    /// <para>
    /// <see cref="Id"/> is settable for one caller only: the startup seeder, which has to create
    /// the main server under the fixed identifier of <see cref="Utils.MainServer"/>. Every other
    /// caller leaves it null and the store generates one.
    /// </para>
    /// </remarks>
    public sealed record ServerWrite
    {
        /// <summary>
        /// Identifier the row must carry, or null to let the store generate one. Only the
        /// startup seeder sets it.
        /// </summary>
        public Guid? Id { get; init; }

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
    }
}
