using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Servers.Enums;
using Fluxy.Core.Servers.Models;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of one server. It is mutable, has no behaviour beyond mapping to
    /// <see cref="Server"/>, and knows nothing about HTTP.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three numeric columns carry a sentinel instead of null: <see cref="ClientCount"/> <c>0</c>
    /// is unlimited, <see cref="GuaranteedSpeedKbps"/> <c>0</c> is unknown and
    /// <see cref="Latency"/> <c>0</c> is not measured. Each is a real state of the row rather
    /// than a missing value, which is why the columns are non-nullable and declare no default.
    /// </para>
    /// <para>
    /// <see cref="CountryCodes"/> is a PostgreSQL <c>text[]</c> column for the reason the
    /// on-demand catalogue's <c>cast</c> and <c>genres</c> are: the list is read with the row and
    /// never queried by position, so a child table would buy a join for an order the array
    /// already keeps.
    /// </para>
    /// </remarks>
    public sealed class ServerEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServerEntity"/> class for a server that
        /// is about to be inserted. The identifier and both audit timestamps are generated.
        /// </summary>
        public ServerEntity()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerEntity"/> class for the row whose
        /// identifier <see cref="Fluxy.Core.Servers.Utils.MainServer"/> fixes, rather than for a
        /// row about to be given a fresh one.
        /// </summary>
        /// <remarks>
        /// The two timestamps are placeholders: both are overwritten for an inserted row by
        /// <c>FluxyDbContext.ApplyAudit</c>, which is the only place audit values are ever
        /// written. The base class asks for them because its other constructor exists to
        /// rehydrate a row that is already in the database.
        /// </remarks>
        /// <param name="id">The identifier the row must carry.</param>
        public ServerEntity(Guid id)
            : base(id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        {
        }

        /// <summary>Display name, unique across the installation.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Address the server is reached at.</summary>
        public string Ip { get; set; } = string.Empty;

        /// <summary>Port the server listens on.</summary>
        public int Port { get; set; }

        /// <summary>Client ceiling, or <c>0</c> for unlimited.</summary>
        public int ClientCount { get; set; }

        /// <summary>Name of the operating system the machine runs.</summary>
        public string OsName { get; set; } = string.Empty;

        /// <summary>Name of the network interface the server is reached through.</summary>
        public string NetworkInterface { get; set; } = string.Empty;

        /// <summary>Measured round-trip latency, or <c>0</c> when not measured.</summary>
        public double Latency { get; set; }

        /// <summary>Whether the server is active or disabled.</summary>
        public ServerStatus Status { get; set; } = ServerStatus.Disabled;

        /// <summary>Guaranteed throughput in kbps, or <c>0</c> when unknown.</summary>
        public int GuaranteedSpeedKbps { get; set; }

        /// <summary>Whether the server only serves timeshifted content.</summary>
        public bool TimeshiftOnly { get; set; }

        /// <summary>Host name of the machine, or null when it has none.</summary>
        public string? Hostname { get; set; }

        /// <summary>Address of the VPN the server is reached through, or null when there is none.</summary>
        public string? VpnIp { get; set; }

        /// <summary>Country codes the server serves; empty for none.</summary>
        public string[] CountryCodes { get; set; } = [];

        /// <summary>
        /// Whether this is the main server. Written by the startup seeder alone and never by an
        /// operator.
        /// </summary>
        public bool IsMain { get; set; }

        /// <summary>Builds the business model out of this row.</summary>
        public Server ToModel() => new()
        {
            Id = Id,
            Name = Name,
            Ip = Ip,
            Port = Port,
            ClientCount = ClientCount,
            OsName = OsName,
            NetworkInterface = NetworkInterface,
            Latency = Latency,
            Status = Status,
            GuaranteedSpeedKbps = GuaranteedSpeedKbps,
            TimeshiftOnly = TimeshiftOnly,
            Hostname = Hostname,
            VpnIp = VpnIp,
            CountryCodes = CountryCodes,
            IsMain = IsMain,
            CreatedAt = CreatedAt
        };

        /// <summary>Copies the editable fields of the business model onto this row.</summary>
        /// <remarks>
        /// The identifier, the audit stamps and <see cref="IsMain"/> are deliberately not copied:
        /// the first two are the row's own identity, the third is the seeder's answer. The
        /// country list is copied into a fresh array so a later change to the model cannot reach
        /// into a tracked row behind the repository's back.
        /// </remarks>
        /// <param name="model">Model whose values the row must carry.</param>
        public void FromModel(Server model)
        {
            Name = model.Name;
            Ip = model.Ip;
            Port = model.Port;
            ClientCount = model.ClientCount;
            OsName = model.OsName;
            NetworkInterface = model.NetworkInterface;
            Latency = model.Latency;
            Status = model.Status;
            GuaranteedSpeedKbps = model.GuaranteedSpeedKbps;
            TimeshiftOnly = model.TimeshiftOnly;
            Hostname = model.Hostname;
            VpnIp = model.VpnIp;
            CountryCodes = model.CountryCodes.ToArray();
        }
    }
}
