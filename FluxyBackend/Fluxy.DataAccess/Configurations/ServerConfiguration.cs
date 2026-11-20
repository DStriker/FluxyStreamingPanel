using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Servers.Utils;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="ServerEntity"/> to the <c>servers</c> table. It is discovered by
    /// <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class ServerConfiguration : IEntityTypeConfiguration<ServerEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name.</summary>
        public const string TableName = "servers";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<ServerEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(server => server.Id);
            builder.Property(server => server.Id).HasColumnName("id");

            builder.Property(server => server.Name)
                .HasColumnName("name")
                .HasMaxLength(ServerLimits.NameMaxLength)
                .IsRequired();

            builder.Property(server => server.Ip)
                .HasColumnName("ip")
                .HasMaxLength(ServerLimits.IpMaxLength)
                .IsRequired();

            builder.Property(server => server.Port)
                .HasColumnName("port")
                .IsRequired();

            // A ceiling of zero means unlimited, which is a real value rather than a missing one.
            builder.Property(server => server.ClientCount)
                .HasColumnName("client_count")
                .IsRequired();

            builder.Property(server => server.OsName)
                .HasColumnName("os_name")
                .HasMaxLength(ServerLimits.OsNameMaxLength)
                .IsRequired();

            builder.Property(server => server.NetworkInterface)
                .HasColumnName("network_interface")
                .HasMaxLength(ServerLimits.NetworkInterfaceMaxLength)
                .IsRequired();

            // The first floating-point column in this database. A latency is a measurement with
            // a fractional part, and the provider stores a C# double as double precision without
            // a conversion. Zero means "not measured", so the column is non-nullable.
            builder.Property(server => server.Latency)
                .HasColumnName("latency")
                .IsRequired();

            // Stored as smallint, like every other enum here: the members are states of one
            // server, not a scale, so the numeric order means nothing.
            builder.Property(server => server.Status)
                .HasColumnName("status")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(server => server.GuaranteedSpeedKbps)
                .HasColumnName("guaranteed_speed_kbps")
                .IsRequired();

            builder.Property(server => server.TimeshiftOnly)
                .HasColumnName("timeshift_only")
                .IsRequired();

            builder.Property(server => server.Hostname)
                .HasColumnName("hostname")
                .HasMaxLength(ServerLimits.HostnameMaxLength);

            builder.Property(server => server.VpnIp)
                .HasColumnName("vpn_ip")
                .HasMaxLength(ServerLimits.IpMaxLength);

            // A list stored as a list. PostgreSQL's text[] keeps the order the operator arranged
            // and Npgsql maps string[] to it without a conversion, the shape the on-demand
            // catalogue and the subscription whitelists already use.
            builder.Property(server => server.CountryCodes)
                .HasColumnName("country_codes")
                .HasColumnType("text[]")
                .IsRequired();

            // The seeder's flag, never nullable: every row answers whether it is the main one.
            builder.Property(server => server.IsMain)
                .HasColumnName("is_main")
                .IsRequired();

            builder.Property(nameof(AuditableEntity.CreatedAt)).HasColumnName("created_at");
            builder.Property(nameof(AuditableEntity.UpdatedAt)).HasColumnName("updated_at");

            // One name per installation, compared case sensitively for the same reason the guide
            // sources and profiles are: PostgreSQL decides what "equal" means for a varchar from
            // the collation of the column, and the default one compares the bytes. The index is
            // named by hand because the service matches a refused write against this exact name.
            builder.HasIndex(server => server.Name)
                .IsUnique()
                .HasDatabaseName(ServerLimits.NameIndexName);
        }
    }
}
