using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Subscriptions.Utils;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="SubscriptionEntity"/> to the <c>subscriptions</c> table. It is discovered
    /// by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Three partial unique indexes.</b> A playlist id and either MAC are unique across
    /// subscriptions <i>when they are present</i>, which is exactly what a PostgreSQL partial
    /// index expresses: <c>... WHERE playlist_id IS NOT NULL</c>. A plain unique index would
    /// refuse the second <c>null</c> in PostgreSQL's model anyway (nulls are distinct), but
    /// naming the indexes here keeps their names in the migration and in the service's exact-match
    /// probes - the convention every other unique index in this project follows.
    /// </para>
    /// <para>
    /// <b>The foreign keys are the design's three decisions.</b> The owner is <c>CASCADE</c>
    /// (a subscription without its owner is meaningless), the issuer is <c>SET NULL</c>
    /// (provenance, not identity, and a departed issuer must not block account deletion) and the
    /// tariff plan is <c>RESTRICT</c> (deleting a plan would silently re-mean the parameters the
    /// subscription derives, so the plan's own service refuses first).
    /// </para>
    /// </remarks>
    public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<SubscriptionEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name.</summary>
        public const string TableName = "subscriptions";

        /// <summary>Partial unique index making a playlist id unique across subscriptions.</summary>
        public const string PlaylistIdIndexName = "IX_subscriptions_playlist_id";

        /// <summary>Partial unique index making a MAG MAC unique across subscriptions.</summary>
        public const string MagMacIndexName = "IX_subscriptions_mag_mac";

        /// <summary>Partial unique index making an Enigma2 MAC unique across subscriptions.</summary>
        public const string Enigma2MacIndexName = "IX_subscriptions_enigma2_mac";

        /// <summary>Check that keeps a connection ceiling inside its range.</summary>
        public const string ConnectionsCheckName = "ck_subscriptions_max_connections";

        /// <summary>Check that keeps at least one output format on the row.</summary>
        public const string OutputFormatsCheckName = "ck_subscriptions_output_formats";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<SubscriptionEntity> builder)
        {
            builder.ToTable(TableName, table =>
            {
                table.HasCheckConstraint(
                    ConnectionsCheckName,
                    "max_connections IS NULL OR max_connections BETWEEN " +
                        SubscriptionLimits.ConnectionsMin + " AND " +
                        SubscriptionLimits.ConnectionsMax);

                // A subscription with no output format could not be delivered at all. The service
                // refuses it before the write; the database is the backstop that turns a bug in
                // that check into a failed write rather than a stored nonsense.
                table.HasCheckConstraint(OutputFormatsCheckName, "output_formats <> 0");
            });

            builder.HasKey(subscription => subscription.Id);
            builder.Property(subscription => subscription.Id)
                .HasColumnName("id");

            builder.Property(subscription => subscription.UserId)
                .HasColumnName("user_id")
                .IsRequired();

            builder.Property(subscription => subscription.PlaylistId)
                .HasColumnName("playlist_id")
                .HasMaxLength(SubscriptionLimits.PlaylistIdLength);

            builder.Property(subscription => subscription.ExpireAt)
                .HasColumnName("expire_at");

            builder.Property(subscription => subscription.TariffPlanId)
                .HasColumnName("tariff_plan_id");

            builder.Property(subscription => subscription.MaxConnections)
                .HasColumnName("max_connections");

            builder.Property(subscription => subscription.IsRestreamer)
                .HasColumnName("is_restreamer")
                .IsRequired();

            // Two lists stored as lists, for the reason the on-demand catalogue's cast and
            // genres are: read with the row, never queried by position.
            builder.Property(subscription => subscription.IpWhitelist)
                .HasColumnName("ip_whitelist")
                .HasColumnType("text[]");

            builder.Property(subscription => subscription.UserAgentWhitelist)
                .HasColumnName("user_agent_whitelist")
                .HasColumnType("text[]");

            builder.Property(subscription => subscription.IsIspLock)
                .HasColumnName("is_isp_lock")
                .IsRequired();

            builder.Property(subscription => subscription.IsCountryLock)
                .HasColumnName("is_country_lock")
                .IsRequired();

            builder.Property(subscription => subscription.IsTrial)
                .HasColumnName("is_trial")
                .IsRequired();

            builder.Property(subscription => subscription.ResellerId)
                .HasColumnName("reseller_id");

            // A [Flags] enum stored as smallint, the conversion every other enum here uses.
            builder.Property(subscription => subscription.OutputFormats)
                .HasColumnName("output_formats")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(subscription => subscription.MagMac)
                .HasColumnName("mag_mac")
                .HasMaxLength(SubscriptionLimits.MacLength);

            builder.Property(subscription => subscription.IsStbLock)
                .HasColumnName("is_stb_lock")
                .IsRequired();

            builder.Property(subscription => subscription.Enigma2Mac)
                .HasColumnName("enigma2_mac")
                .HasMaxLength(SubscriptionLimits.MacLength);

            builder.Property(subscription => subscription.PlayToken)
                .HasColumnName("play_token")
                .HasMaxLength(255);

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // A subscription without its owner is meaningless, so the row goes with the account -
            // the same decision the tokens and the guard rules already carry.
            builder.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(subscription => subscription.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Provenance rather than identity: a deleted issuer must not block the account's own
            // deletion, nor take the subscriptions it once issued with it.
            builder.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(subscription => subscription.ResellerId)
                .OnDelete(DeleteBehavior.SetNull);

            // Deleting a plan would silently re-mean the parameters the subscription derives from
            // it, because they were never stored while the plan was attached. The plan's own
            // service refuses first with a code and a count; this is the backstop.
            builder.HasOne(subscription => subscription.TariffPlan)
                .WithMany()
                .HasForeignKey(subscription => subscription.TariffPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            // The partial unique indexes cannot be declared here: EF Core's model index has no
            // filter, so a plain unique index would occupy the name and refuse every second null
            // the model never even reaches. They are created by the migration under these exact
            // names, which is what the service matches a violation against.
            builder.HasIndex(subscription => subscription.UserId);
            builder.HasIndex(subscription => subscription.ResellerId);
            builder.HasIndex(subscription => subscription.TariffPlanId);

            // The two orders the table offers. Creation time is the default and the expire date
            // is the other; both are read with the row, so neither is a computed value.
            builder.HasIndex(subscription => subscription.CreatedAt);
            builder.HasIndex(subscription => subscription.ExpireAt);
        }
    }
}
