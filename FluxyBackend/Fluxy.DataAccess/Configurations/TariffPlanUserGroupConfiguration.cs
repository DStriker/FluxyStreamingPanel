using Fluxy.Core.Persistence.Base;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="TariffPlanUserGroupEntity"/> to the <c>tariff_plan_user_groups</c> table.
    /// It is discovered by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class TariffPlanUserGroupConfiguration
        : IEntityTypeConfiguration<TariffPlanUserGroupEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name.</summary>
        public const string TableName = "tariff_plan_user_groups";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<TariffPlanUserGroupEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(row => row.Id);
            builder.Property(row => row.Id)
                .HasColumnName("id");

            builder.Property(row => row.TariffPlanId)
                .HasColumnName("tariff_plan_id")
                .IsRequired();

            builder.Property(row => row.UserGroupId)
                .HasColumnName("user_group_id")
                .IsRequired();

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // A deleted plan leaves no membership behind. The navigation is named here for the
            // reason BouquetConfiguration gives: without it the `TariffPlan` property becomes a
            // second relationship with a shadow key column of its own.
            builder.HasOne(row => row.TariffPlan)
                .WithMany()
                .HasForeignKey(row => row.TariffPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            // A group that is deleted stops being offered every plan it was in. This is the same
            // reasoning the account rows follow: a link that outlived its subject is a fact about
            // nobody.
            builder.HasOne<UserGroupEntity>()
                .WithMany()
                .HasForeignKey(row => row.UserGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            // One group appears at most once per plan; the same group in another plan is a
            // different row, which is what makes a catalogue of plans a catalogue rather than an
            // exclusive assignment.
            builder.HasIndex(row => new { row.TariffPlanId, row.UserGroupId })
                .IsUnique();

            // The probe behind "may this group buy anything at all", and the read the form uses to
            // open its picker on what the plan has.
            builder.HasIndex(row => row.UserGroupId);
        }
    }
}
