using Fluxy.Core.Servers.Enums;

namespace Fluxy.Core.Servers.Commands
{
    /// <summary>
    /// Everything an administrator supplies to create a server.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The five values a row cannot do without are required: <see cref="Name"/>, the address it
    /// is reached at, the port it listens on, the operating system and the interface. The rest
    /// are optional because the form offers a starting value, and an absent one is resolved by
    /// the service - this project declares no database defaults, so a value the application
    /// forgot would surface as an error rather than be invented silently.
    /// </para>
    /// <para>
    /// The three numeric sentinels are stated rather than hidden: an absent
    /// <see cref="ClientCount"/> becomes <c>0</c> (unlimited), an absent
    /// <see cref="GuaranteedSpeedKbps"/> becomes <c>0</c> (unknown), and an absent
    /// <see cref="Status"/> becomes <see cref="ServerStatus.Active"/>. A server that has just
    /// been added has not been measured, so <c>Latency</c> is not a create field at all: the
    /// row starts at <c>0</c>, meaning "not measured", and only the measuring path writes a
    /// value.
    /// </para>
    /// </remarks>
    public sealed record NewServer
    {
        /// <summary>Display name. Trimmed, unique across the installation.</summary>
        public required string Name { get; init; }

        /// <summary>Address the server is reached at: an IPv4 or IPv6 literal.</summary>
        public required string Ip { get; init; }

        /// <summary>Port the server listens on.</summary>
        public required int Port { get; init; }

        /// <summary>Name of the operating system the machine runs. Trimmed.</summary>
        public required string OsName { get; init; }

        /// <summary>Name of the network interface the server is reached through. Trimmed.</summary>
        public required string NetworkInterface { get; init; }

        /// <summary>
        /// How many clients the server may serve, or null for unlimited. <c>0</c> is unlimited
        /// rather than "disabled", so it is a real ceiling and not a missing value.
        /// </summary>
        public int? ClientCount { get; init; }

        /// <summary>State the server starts in, or null for
        /// <see cref="ServerStatus.Active"/>.</summary>
        public ServerStatus? Status { get; init; }

        /// <summary>
        /// Guaranteed throughput in kbps, or null for unknown. The unit matches the bitrate
        /// columns of a transcoding profile.
        /// </summary>
        public int? GuaranteedSpeedKbps { get; init; }

        /// <summary>Whether the server only serves timeshifted content. Absent means false.</summary>
        public bool TimeshiftOnly { get; init; }

        /// <summary>Host name of the machine, or null when it has none. Blank is stored as null.</summary>
        public string? Hostname { get; init; }

        /// <summary>
        /// Address of the VPN the server is reached through, or null when there is none. Blank
        /// is stored as null.
        /// </summary>
        public string? VpnIp { get; init; }

        /// <summary>
        /// ISO 3166-1 alpha-2 country codes the server serves, or null for none. The service
        /// normalizes each code to upper case and refuses one that is not a country.
        /// </summary>
        public IReadOnlyList<string>? CountryCodes { get; init; }
    }
}
