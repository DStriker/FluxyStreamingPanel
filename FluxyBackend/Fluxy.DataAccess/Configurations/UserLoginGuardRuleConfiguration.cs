using Fluxy.Core.Abstractions;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="UserLoginGuardRuleEntity"/> to the <c>user_login_guard_rules</c> table.
    /// It is discovered by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class UserLoginGuardRuleConfiguration : IEntityTypeConfiguration<UserLoginGuardRuleEntity>
    {
        /// <summary>
        /// Name of the table. Tables use the plural form of the entity name, columns use
        /// snake_case, and both live in the default schema of the database.
        /// </summary>
        public const string TableName = "user_login_guard_rules";

        /// <summary>
        /// Longest canonical rule value. An IPv6 CIDR in full form stays well under this; the
        /// room above is for a form nobody has thought of yet rather than for a longer network.
        /// </summary>
        public const int ValueMaxLength = 64;

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<UserLoginGuardRuleEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(rule => rule.Id);
            builder.Property(rule => rule.Id)
                .HasColumnName("id");

            builder.Property(rule => rule.UserId)
                .HasColumnName("user_id")
                .IsRequired();

            // Stored as smallint, like every other enum here.
            builder.Property(rule => rule.Kind)
                .HasColumnName("kind")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(rule => rule.Value)
                .HasColumnName("value")
                .HasMaxLength(ValueMaxLength)
                .IsRequired();

            // Audit columns inherited from AuditableEntity, listed explicitly like every other
            // column for the same reason they are on the users table.
            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // A rule belongs to exactly one account and dies with it. Declared as a real
            // foreign key rather than a bare indexed column, so deleting an account cannot
            // leave rules that allow networks for nobody.
            builder.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(rule => rule.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // No HasDefaultValue anywhere on purpose, for the reason it holds on every other
            // table: a value the database invents when the application forgot a column hides the
            // mistake instead of surfacing it.
            builder.HasIndex(rule => rule.UserId);
            builder.HasIndex(rule => new { rule.UserId, rule.Kind, rule.Value })
                .IsUnique();
        }
    }
}
