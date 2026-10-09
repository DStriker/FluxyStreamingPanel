using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="UserGroupEntity"/> to the <c>user_groups</c> table. It is discovered by
    /// <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class UserGroupConfiguration : IEntityTypeConfiguration<UserGroupEntity>
    {
        /// <summary>
        /// Name of the table. Tables use the plural form of the entity name, columns use
        /// snake_case, and both live in the default schema of the database.
        /// </summary>
        public const string TableName = "user_groups";

        /// <summary>
        /// Longest display name. A group is read as a column of a table beside eight others,
        /// and the room above it is for a name nobody has thought of rather than for one this
        /// length would already hold.
        /// </summary>
        public const int NameMaxLength = 100;

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<UserGroupEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(group => group.Id);
            builder.Property(group => group.Id)
                .HasColumnName("id");

            builder.Property(group => group.Name)
                .HasColumnName("name")
                .HasMaxLength(NameMaxLength)
                .IsRequired();

            // Stored as smallint, like every other enum here. Their numeric order is not a
            // permission order - see the enums.
            builder.Property(group => group.Role)
                .HasColumnName("role")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(group => group.Status)
                .HasColumnName("status")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // One group is named once. Case sensitive for the same reason username and email
            // are: PostgreSQL decides what "equal" means for a varchar from the collation of
            // the column, and the default one compares the bytes - which is what makes
            // "Clients" and "clients" two groups, exactly as it makes them two accounts.
            builder.HasIndex(group => group.Name)
                .IsUnique();

            // The role is the single source of what level a member holds, so it is worth an
            // index of its own: "every group at this level" is the read a role filter on the
            // accounts page performs through the join.
            builder.HasIndex(group => group.Role);
        }
    }
}
