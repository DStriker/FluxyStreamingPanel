using Fluxy.Core.Content.Utils;
using Fluxy.Core.Persistence.Base;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="BouquetEntity"/> to the <c>bouquets</c> table. It is discovered by
    /// <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    /// <remarks>
    /// The unique index on the name is <b>not</b> declared here on purpose: it is an expression
    /// index over <c>lower(name)</c>, which EF Core 10 cannot express as a model index, so
    /// <c>AddBouquets</c> creates it with <c>migrationBuilder.Sql</c> under the fixed name
    /// <see cref="NameLowerIndexName"/>. Declaring a plain unique index on <c>Name</c> instead
    /// would make "Кино" and "кино" two rows - the rule this table is here to state is that they
    /// are one.
    /// </remarks>
    public sealed class BouquetConfiguration : IEntityTypeConfiguration<BouquetEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name, columns
        /// use snake_case, and both live in the default schema of the database.</summary>
        public const string TableName = "bouquets";

        /// <summary>
        /// Longest display name. A bouquet is read as one line of a table beside its counts, so
        /// the room above this length is for a name nobody has thought of rather than for one
        /// this length would already hold.
        /// </summary>
        public const int NameMaxLength = BouquetLimits.NameMaxLength;

        /// <summary>
        /// Name of the unique index over <c>lower(name)</c>. It is created by raw SQL in the
        /// migration, and the service matches a unique violation against this exact name - the
        /// convention every other name index here follows, and the only thing that lets a
        /// duplicate be answered as <c>already_exists</c> instead of as a provider error.
        /// </summary>
        public const string NameLowerIndexName = "IX_bouquets_name_lower";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<BouquetEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(bouquet => bouquet.Id);
            builder.Property(bouquet => bouquet.Id)
                .HasColumnName("id");

            builder.Property(bouquet => bouquet.Name)
                .HasColumnName("name")
                .HasMaxLength(NameMaxLength)
                .IsRequired();

            builder.Property(bouquet => bouquet.SortOrder)
                .HasColumnName("sort_order");

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // The read every screen of this domain performs: the whole list, lowest position
            // first. A bouquet list is a screenful rather than a paged resource, so this index
            // answers it as a range scan and there is nothing to filter by.
            builder.HasIndex(bouquet => bouquet.SortOrder);
        }
    }
}
