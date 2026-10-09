using Fluxy.Core.Abstractions;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="UserGroupPermissionEntity"/> to the <c>user_group_permissions</c>
    /// table. It is discovered by <c>FluxyDbContext.OnModelCreating</c>, so no registration is
    /// needed.
    /// </summary>
    public sealed class UserGroupPermissionConfiguration
        : IEntityTypeConfiguration<UserGroupPermissionEntity>
    {
        /// <summary>
        /// Name of the table. Tables use the plural form of the entity name, columns use
        /// snake_case, and both live in the default schema of the database.
        /// </summary>
        public const string TableName = "user_group_permissions";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<UserGroupPermissionEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(grant => grant.Id);
            builder.Property(grant => grant.Id)
                .HasColumnName("id");

            builder.Property(grant => grant.UserGroupId)
                .HasColumnName("user_group_id")
                .IsRequired();

            // Stored as smallint, like every other enum here: the flags are powers of two
            // from a fixed catalog, and a short holds every value the catalog can grow to.
            builder.Property(grant => grant.Permission)
                .HasColumnName("permission")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // A grant belongs to exactly one group and dies with it. Declared as a real
            // foreign key rather than a bare indexed column, so deleting a group cannot leave
            // permissions granting things for a group that is not there.
            builder.HasOne<UserGroupEntity>()
                .WithMany(group => group.Permissions)
                .HasForeignKey(grant => grant.UserGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            // One row per grant of one group: the unique index is what makes "the set is what
            // the rows say" true without the application having to remember not to write the
            // same permission twice while saving a form that posts it twice.
            builder.HasIndex(grant => new { grant.UserGroupId, grant.Permission })
                .IsUnique();
        }
    }
}
