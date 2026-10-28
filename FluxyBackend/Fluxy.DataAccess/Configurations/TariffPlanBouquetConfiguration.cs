using Fluxy.Core.Persistence.Base;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="TariffPlanBouquetEntity"/> to the <c>tariff_plan_bouquets</c> table. It is
    /// discovered by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class TariffPlanBouquetConfiguration
        : IEntityTypeConfiguration<TariffPlanBouquetEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name.</summary>
        public const string TableName = "tariff_plan_bouquets";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<TariffPlanBouquetEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(row => row.Id);
            builder.Property(row => row.Id)
                .HasColumnName("id");

            builder.Property(row => row.TariffPlanId)
                .HasColumnName("tariff_plan_id")
                .IsRequired();

            builder.Property(row => row.BouquetId)
                .HasColumnName("bouquet_id")
                .IsRequired();

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            builder.HasOne(row => row.TariffPlan)
                .WithMany()
                .HasForeignKey(row => row.TariffPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            // A deleted bouquet stops being delivered by every plan that carried it - the same
            // cleanup the content membership tables get, and the reason this is a foreign key
            // rather than a bare identifier in a list column.
            builder.HasOne<BouquetEntity>()
                .WithMany()
                .HasForeignKey(row => row.BouquetId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(row => new { row.TariffPlanId, row.BouquetId })
                .IsUnique();

            // Which plans deliver a bouquet - the reverse question, which the composite index
            // above cannot answer because it leads with the plan.
            builder.HasIndex(row => row.BouquetId);
        }
    }
}
