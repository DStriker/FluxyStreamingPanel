using Fluxy.Core.Servers.Enums;

namespace Fluxy.Core.Servers.Commands
{
    /// <summary>
    /// What an administrator writes to an existing server.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is a full write, not a partial patch.</b> The form holds one server, so what the
    /// operator sees and what the row becomes are the same statement - the convention the tariff
    /// plan, the streams and the transcoding profile already follow. Every editable field is
    /// present, and a field the request leaves out is not "keep the stored value": a record
    /// cannot tell an omitted member from one that was never written, so the service treats the
    /// shape as the whole row and the transport must send all of it.
    /// </para>
    /// <para>
    /// Two things are deliberately absent. <c>IsMain</c> is set by the startup seeder alone and
    /// never by an operator, so no request can promote another row or demote this one. The main
    /// row is otherwise an ordinary row: it accepts every field write this record carries, and
    /// the only operation it refuses is <c>DELETE</c>, which answers
    /// <see cref="ServerAction.ImmutableMain"/> after the existence check, so a missing
    /// identifier still answers <see cref="ServerAction.NotFound"/>. <c>Id</c> and the audit
    /// stamps are the row's own identity and are never part of a write.
    /// </para>
    /// </remarks>
    public sealed record ServerPatch
    {
        /// <summary>Display name. Trimmed, unique across the installation.</summary>
        public required string Name { get; init; }

        /// <summary>Address the server is reached at: an IPv4 or IPv6 literal.</summary>
        public required string Ip { get; init; }

        /// <summary>Port the server listens on.</summary>
        public required int Port { get; init; }

        /// <summary>How many clients the server may serve. <c>0</c> means unlimited.</summary>
        public required int ClientCount { get; init; }

        /// <summary>Name of the operating system the machine runs. Trimmed.</summary>
        public required string OsName { get; init; }

        /// <summary>Name of the network interface the server is reached through. Trimmed.</summary>
        public required string NetworkInterface { get; init; }

        /// <summary>
        /// Measured round-trip latency. <c>0</c> means it has not been measured, which is a
        /// state the operator may restore by clearing the field.
        /// </summary>
        public required double Latency { get; init; }

        /// <summary>Whether the server is active or disabled.</summary>
        public required ServerStatus Status { get; init; }

        /// <summary>
        /// Guaranteed throughput in kbps. <c>0</c> means unknown.
        /// </summary>
        public required int GuaranteedSpeedKbps { get; init; }

        /// <summary>Whether the server only serves timeshifted content.</summary>
        public required bool TimeshiftOnly { get; init; }

        /// <summary>Host name of the machine, or null when it has none. Blank is stored as null.</summary>
        public string? Hostname { get; init; }

        /// <summary>
        /// Address of the VPN the server is reached through, or null when there is none. Blank
        /// is stored as null.
        /// </summary>
        public string? VpnIp { get; init; }

        /// <summary>
        /// ISO 3166-1 alpha-2 country codes the server serves. Empty means none; the whole list
        /// is replaced on every write, in the order it arrives.
        /// </summary>
        public required IReadOnlyList<string> CountryCodes { get; init; }
    }
}
