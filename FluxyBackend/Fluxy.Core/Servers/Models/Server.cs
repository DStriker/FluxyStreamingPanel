using Fluxy.Core.Servers.Enums;

namespace Fluxy.Core.Servers.Models
{
    /// <summary>
    /// Immutable business representation of one server: an address the installation may relay
    /// through, together with what the machine behind it is and how it is doing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only the main server is fixed.</b> <see cref="IsMain"/> is written by the startup
    /// seeder and by nothing else - see <see cref="Utils.MainServer"/> - so every row is either
    /// the single main server or an ordinary one an operator owns end to end.
    /// </para>
    /// <para>
    /// Three numeric columns carry a sentinel rather than null, because each has a real state
    /// that zero describes: <see cref="ClientCount"/> <c>0</c> is "unlimited",
    /// <see cref="GuaranteedSpeedKbps"/> <c>0</c> is "unknown", and <see cref="Latency"/> <c>0</c>
    /// is "not measured". None of them is a missing value the database invented.
    /// </para>
    /// <para>
    /// <see cref="CountryCodes"/> is an ordered list stored as a PostgreSQL <c>text[]</c> column,
    /// the shape the on-demand catalogue and the subscription whitelists already use: the codes
    /// are read with the row and never queried by position, so a child table would buy a join
    /// for an order the array already keeps. An empty array means "no country"; the list is
    /// never null.
    /// </para>
    /// </remarks>
    public sealed record Server
    {
        /// <summary>
        /// Identifier of the row. The main server carries the fixed identifier of
        /// <see cref="Utils.MainServer.Id"/>; every other row carries a fresh one.
        /// </summary>
        public required Guid Id { get; init; }

        /// <summary>
        /// Display name, between 1 and <see cref="Utils.ServerLimits.NameMaxLength"/>
        /// characters, trimmed and unique across the installation.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Address the server is reached at, at most <see cref="Utils.ServerLimits.IpMaxLength"/>
        /// characters. Either an IPv4 or an IPv6 literal; the service refuses anything
        /// <see cref="System.Net.IPAddress.TryParse(string, out System.Net.IPAddress?)"/>
        /// cannot read.
        /// </summary>
        public required string Ip { get; init; }

        /// <summary>
        /// Port the server listens on, between <see cref="Utils.ServerLimits.PortMin"/> and
        /// <see cref="Utils.ServerLimits.PortMax"/>.
        /// </summary>
        public required int Port { get; init; }

        /// <summary>
        /// How many clients the server may serve, at most
        /// <see cref="Utils.ServerLimits.ClientCountMax"/>. <c>0</c> means unlimited.
        /// </summary>
        public required int ClientCount { get; init; }

        /// <summary>
        /// Name of the operating system the machine runs, at most
        /// <see cref="Utils.ServerLimits.OsNameMaxLength"/> characters and trimmed.
        /// </summary>
        public required string OsName { get; init; }

        /// <summary>
        /// Name of the network interface the server is reached through, such as <c>eth0</c>, at
        /// most <see cref="Utils.ServerLimits.NetworkInterfaceMaxLength"/> characters.
        /// </summary>
        public required string NetworkInterface { get; init; }

        /// <summary>
        /// Measured round-trip latency. Never negative; <c>0</c> means it has not been measured.
        /// </summary>
        public required double Latency { get; init; }

        /// <summary>Whether the server is active or disabled.</summary>
        public required ServerStatus Status { get; init; }

        /// <summary>
        /// Guaranteed throughput in kbps, at most
        /// <see cref="Utils.ServerLimits.GuaranteedSpeedMaxKbps"/>. <c>0</c> means unknown. The
        /// unit matches the bitrate columns of a transcoding profile.
        /// </summary>
        public required int GuaranteedSpeedKbps { get; init; }

        /// <summary>Whether the server only serves timeshifted content.</summary>
        public required bool TimeshiftOnly { get; init; }

        /// <summary>
        /// Host name of the machine, at most
        /// <see cref="Utils.ServerLimits.HostnameMaxLength"/> characters, or null when it has
        /// none. A blank value is stored as null.
        /// </summary>
        public string? Hostname { get; init; }

        /// <summary>
        /// Address of the VPN the server is reached through, at most
        /// <see cref="Utils.ServerLimits.IpMaxLength"/> characters, or null when there is none.
        /// When present it is a literal the same rule as <see cref="Ip"/> accepts; a blank value
        /// is stored as null.
        /// </summary>
        public string? VpnIp { get; init; }

        /// <summary>
        /// ISO 3166-1 alpha-2 country codes the server serves, upper case and in the order the
        /// operator arranged. Empty when it serves no particular country; never null.
        /// </summary>
        public required string[] CountryCodes { get; init; }

        /// <summary>
        /// Whether this is the main server. Written by the startup seeder alone - an operator
        /// can rename and reconfigure the row, but cannot make another row the main one or turn
        /// this one into an ordinary server.
        /// </summary>
        public required bool IsMain { get; init; }

        /// <summary>
        /// When the row was written. The row's audit stamp: the context stamps it on insert and
        /// nothing else writes it, so no operator command carries or changes it. It travels with
        /// the model because the table sorts by it and the detail answer draws it.
        /// </summary>
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
