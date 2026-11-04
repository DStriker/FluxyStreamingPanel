using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Subscriptions.Utils;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="ConnectionLogEntity"/> to the <c>connection_logs</c> table. It is discovered
    /// by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Country and provider are real columns here, unlike the visit history.</b> The history
    /// resolves GeoIP at read time and therefore cannot filter on it: the value does not exist
    /// until the page is being built. This table is filtered and counted by both, so both are
    /// captured with the row and kept as they were when the session opened. The read path never
    /// consults <c>IGeoIpResolver</c>.
    /// </para>
    /// <para>
    /// <b>The subscription's foreign key cascades.</b> Deleting a subscription deletes the logs of
    /// the connections it carried - the log is evidence about that subscription, and removing the
    /// subject is what the operator asked for. The table is never orphaned.
    /// </para>
    /// <para>
    /// <b>Six indexes plus one hand-written partial index.</b> Five answer the filters the screen
    /// offers and one answers the default order; the partial index over
    /// <c>(started_at DESC, id DESC) WHERE ended_at IS NULL</c> exists because "live" is a small
    /// tail of a table that grows forever, and a filtered index over that tail is what keeps the
    /// live view from scanning the finished ones. EF Core's model index has no filter, so it is
    /// created by the migration and recorded here only as remarks - the same treatment the
    /// subscriptions' partial unique indexes get.
    /// </para>
    /// </remarks>
    public sealed class ConnectionLogConfiguration : IEntityTypeConfiguration<ConnectionLogEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name.</summary>
        public const string TableName = "connection_logs";

        /// <summary>Name of the hand-written partial index over the live rows.</summary>
        public const string LiveIndexName = "IX_connection_logs_live_started_at";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<ConnectionLogEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(connection => connection.Id);
            builder.Property(connection => connection.Id)
                .HasColumnName("id");

            builder.Property(connection => connection.SubscriptionId)
                .HasColumnName("subscription_id")
                .IsRequired();

            // smallint, like every other enum in this solution. The two values are schema: they
            // are already written into rows and must not be renumbered.
            builder.Property(connection => connection.StreamKind)
                .HasColumnName("stream_kind")
                .HasConversion<short>()
                .IsRequired();

            // Opaque on purpose: no foreign key. The relaying process may report a stream this
            // installation never recorded, the same way VideoStream.SeriesId may name a series
            // that was never recorded.
            builder.Property(connection => connection.StreamId)
                .HasColumnName("stream_id")
                .IsRequired();

            // 45 characters is the longest textual IPv6 form, so the column can hold every
            // address the socket layer can hand over.
            builder.Property(connection => connection.Ip)
                .HasColumnName("ip")
                .HasMaxLength(ConnectionLogListLimits.IpMaxLength)
                .IsRequired();

            builder.Property(connection => connection.UserAgent)
                .HasColumnName("user_agent")
                .HasMaxLength(ConnectionLogListLimits.UserAgentMaxLength);

            // Stored upper case, two characters. The filter upper-cases its input so the same
            // country is one value however the caller typed it.
            builder.Property(connection => connection.CountryCode)
                .HasColumnName("country_code")
                .HasMaxLength(ConnectionLogListLimits.CountryCodeLength);

            builder.Property(connection => connection.Isp)
                .HasColumnName("isp")
                .HasMaxLength(ConnectionLogListLimits.IspMaxLength);

            builder.Property(connection => connection.StartedAt)
                .HasColumnName("started_at")
                .IsRequired();

            // Null while the session is live. "Live" is exactly this column being null, and the
            // partial index over the other two columns is built on that predicate.
            builder.Property(connection => connection.EndedAt)
                .HasColumnName("ended_at");

            builder.Property(connection => connection.BroadcastPid)
                .HasColumnName("broadcast_pid");

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // A connection without the subscription it was made under is a row nobody can be told
            // about, so the row goes with it. The consequence is recorded here and in the entity's
            // remarks: deleting a subscription removes its logs.
            builder.HasOne<SubscriptionEntity>()
                .WithMany()
                .HasForeignKey(connection => connection.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            // The default order, and the tie-break every page turn depends on: two sessions can
            // start in the same instant, and without the identifier the database is free to answer
            // a different order on the next read.
            builder.HasIndex(connection => new { connection.StartedAt, connection.Id })
                .IsDescending(true, true);

            // One subscription's connections, oldest first - the drill-down from a subscription.
            builder.HasIndex(connection => new { connection.SubscriptionId, connection.StartedAt });

            // "Every connection of this stream", across both media.
            builder.HasIndex(connection => new { connection.StreamKind, connection.StreamId });

            // The four exact filters the screen offers. `user_agent` is nullable, and PostgreSQL's
            // b-tree indexes nulls like any other value, so a filter that names a client still
            // costs one index read rather than a scan of a table that grows forever.
            builder.HasIndex(connection => connection.Ip);
            builder.HasIndex(connection => connection.CountryCode);
            builder.HasIndex(connection => connection.Isp);
            builder.HasIndex(connection => connection.UserAgent);
        }
    }
}
