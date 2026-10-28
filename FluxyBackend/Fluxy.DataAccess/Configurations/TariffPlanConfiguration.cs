using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Subscriptions.Utils;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="TariffPlanEntity"/> to the <c>tariff_plans</c> table. It is discovered by
    /// <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The unique index on the name is <b>not</b> declared here on purpose: it is an expression
    /// index over <c>lower(name)</c>, which EF Core 10 cannot express as a model index, so
    /// <c>AddTariffPlans</c> creates it with <c>migrationBuilder.Sql</c> under the fixed name
    /// <see cref="NameLowerIndexName"/>. Declaring a plain unique index on <c>Name</c> instead
    /// would make "Пробный" and "пробный" two rows - the rule this table exists to state is that
    /// they are one.
    /// </para>
    /// <para>
    /// The three CHECK constraints are the rules that hold whatever writes the row: a negative
    /// price, a period of zero and a connection ceiling outside 1..10000 are not states this
    /// domain has, and the service refuses them before they arrive. The database is the backstop
    /// that turns a bug in that check into a failed write rather than into a stored nonsense.
    /// </para>
    /// </remarks>
    public sealed class TariffPlanConfiguration : IEntityTypeConfiguration<TariffPlanEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name.</summary>
        public const string TableName = "tariff_plans";

        /// <summary>Longest display name, also enforced by the service and the form.</summary>
        public const int NameMaxLength = TariffPlanLimits.NameMaxLength;

        /// <summary>
        /// Name of the unique index over <c>lower(name)</c>. It is created by raw SQL in the
        /// migration, and the service matches a unique violation against this exact name - the
        /// convention every other name index here follows, and the only thing that lets a
        /// duplicate be answered as <c>already_exists</c> instead of as a provider error.
        /// </summary>
        public const string NameLowerIndexName = "IX_tariff_plans_name_lower";

        /// <summary>Check that keeps a price at or above zero.</summary>
        public const string CostCheckName = "ck_tariff_plans_cost";

        /// <summary>Check that keeps a validity period at or above one unit.</summary>
        public const string DurationCheckName = "ck_tariff_plans_duration_value";

        /// <summary>Check that keeps the connection ceiling inside its range.</summary>
        public const string ConnectionsCheckName = "ck_tariff_plans_max_connections";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<TariffPlanEntity> builder)
        {
            builder.ToTable(TableName, table =>
            {
                table.HasCheckConstraint(CostCheckName, "cost >= 0");

                table.HasCheckConstraint(
                    DurationCheckName,
                    "duration_value >= " + TariffPlanLimits.DurationValueMin);

                table.HasCheckConstraint(
                    ConnectionsCheckName,
                    "max_connections BETWEEN " + TariffPlanLimits.ConnectionsMin +
                        " AND " + TariffPlanLimits.ConnectionsMax);
            });

            builder.HasKey(plan => plan.Id);
            builder.Property(plan => plan.Id)
                .HasColumnName("id");

            builder.Property(plan => plan.Name)
                .HasColumnName("name")
                .HasMaxLength(NameMaxLength)
                .IsRequired();

            builder.Property(plan => plan.IsTrial)
                .HasColumnName("is_trial")
                .IsRequired();

            builder.Property(plan => plan.Cost)
                .HasColumnName("cost")
                .IsRequired();

            builder.Property(plan => plan.DurationValue)
                .HasColumnName("duration_value")
                .IsRequired();

            // Stored as smallint, which is what HasConversion<short> maps to in PostgreSQL. The
            // numeric values are part of the schema; see the enum for why they are not renumbered.
            builder.Property(plan => plan.DurationUnit)
                .HasColumnName("duration_unit")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(plan => plan.SupportsM3u)
                .HasColumnName("supports_m3u")
                .IsRequired();

            builder.Property(plan => plan.SupportsMag)
                .HasColumnName("supports_mag")
                .IsRequired();

            builder.Property(plan => plan.SupportsEnigma2)
                .HasColumnName("supports_enigma2")
                .IsRequired();

            builder.Property(plan => plan.OutputMpegTs)
                .HasColumnName("output_mpeg_ts")
                .IsRequired();

            builder.Property(plan => plan.OutputHls)
                .HasColumnName("output_hls")
                .IsRequired();

            builder.Property(plan => plan.StrictStbDeviceId)
                .HasColumnName("strict_stb_device_id")
                .IsRequired();

            builder.Property(plan => plan.AllowRestream)
                .HasColumnName("allow_restream")
                .IsRequired();

            builder.Property(plan => plan.MaxConnections)
                .HasColumnName("max_connections")
                .IsRequired();

            builder.Property(plan => plan.AllowMultipleCountries)
                .HasColumnName("allow_multiple_countries")
                .IsRequired();

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // The default read of the table: every plan in name order. The cost index answers
            // the one other column a caller may order by, and creation time answers the third.
            builder.HasIndex(plan => plan.Name);
            builder.HasIndex(plan => plan.Cost);
            builder.HasIndex(plan => plan.CreatedAt);
        }
    }
}
