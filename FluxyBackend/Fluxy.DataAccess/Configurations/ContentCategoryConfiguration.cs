using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Content;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="ContentCategoryEntity"/> to the <c>content_categories</c> table. It is
    /// discovered by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class ContentCategoryConfiguration : IEntityTypeConfiguration<ContentCategoryEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name, columns
        /// use snake_case, and both live in the default schema of the database.</summary>
        public const string TableName = "content_categories";

        /// <summary>
        /// Longest display name. A category is a section heading over a content grid, so it
        /// is read as a column of a table beside a couple of actions - the room above this
        /// length is for a name nobody has thought of rather than for one this length would
        /// already hold.
        /// </summary>
        public const int NameMaxLength = ContentCategoryLimits.NameMaxLength;

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<ContentCategoryEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(category => category.Id);
            builder.Property(category => category.Id)
                .HasColumnName("id");

            builder.Property(category => category.Name)
                .HasColumnName("name")
                .HasMaxLength(NameMaxLength)
                .IsRequired();

            // Stored as smallint, like every other enum here. The members are kinds of
            // content, not levels of anything, so the numeric order means nothing.
            builder.Property(category => category.Type)
                .HasColumnName("content_type")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(category => category.SortOrder)
                .HasColumnName("sort_order");

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // One kind of content names each of its categories once. Scoped to the type,
            // because "Sports" over video streams and "Sports" over series are two unrelated
            // sections of two different screens, and forbidding one because the other exists
            // would be a rule about nothing. Case sensitive for the same reason username and
            // email are: PostgreSQL decides what "equal" means for a varchar from the
            // collation of the column, and the default one compares the bytes - which is
            // what makes "Sports" and "sports" two categories, exactly as it makes them two
            // accounts.
            builder.HasIndex(category => new { category.Type, category.Name })
                .IsUnique();

            // The read every page of this domain performs: all rows of one type, lowest
            // position first. The type leads so the index can answer it as a range scan.
            builder.HasIndex(category => new { category.Type, category.SortOrder });
        }
    }
}
