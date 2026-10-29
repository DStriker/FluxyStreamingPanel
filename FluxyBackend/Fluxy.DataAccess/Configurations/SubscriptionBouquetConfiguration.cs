using Fluxy.Core.Persistence.Base;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="SubscriptionBouquetEntity"/> to the <c>subscription_bouquets</c> table. It
    /// is discovered by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    /// <remarks>
    /// The same shape as <c>tariff_plan_bouquets</c> and for the same reasons: an explicit row
    /// per membership (so a deleted bouquet leaves no link behind), a unique index over the pair,
    /// and a reverse index over the bouquet so "which subscriptions carry this package" is an
    /// index scan rather than a sequential one. There is no position column - the order a
    /// subscription lists its bouquets in means nothing.
    /// </remarks>
    public sealed class SubscriptionBouquetConfiguration
        : IEntityTypeConfiguration<SubscriptionBouquetEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name.</summary>
        public const string TableName = "subscription_bouquets";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<SubscriptionBouquetEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(row => row.Id);
            builder.Property(row => row.Id)
                .HasColumnName("id");

            builder.Property(row => row.SubscriptionId)
                .HasColumnName("subscription_id")
                .IsRequired();

            builder.Property(row => row.BouquetId)
                .HasColumnName("bouquet_id")
                .IsRequired();

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            builder.HasOne(row => row.Subscription)
                .WithMany(subscription => subscription.Bouquets)
                .HasForeignKey(row => row.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            // A deleted bouquet stops being carried by every subscription - the same cleanup the
            // plan's junction gets, and the reason this is a real foreign key rather than a bare
            // identifier in a list column.
            builder.HasOne<BouquetEntity>()
                .WithMany()
                .HasForeignKey(row => row.BouquetId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(row => new { row.SubscriptionId, row.BouquetId })
                .IsUnique();

            // Which subscriptions carry a bouquet - the reverse question, which the composite
            // index above cannot answer because it leads with the subscription.
            builder.HasIndex(row => row.BouquetId);
        }
    }
}
