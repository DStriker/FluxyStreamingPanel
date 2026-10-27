using Fluxy.Core.Persistence.Base;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="BouquetSeriesEntity"/> to the <c>bouquet_series</c> table. It is
    /// discovered by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class BouquetSeriesConfiguration
        : IEntityTypeConfiguration<BouquetSeriesEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name, columns
        /// use snake_case, and both live in the default schema of the database.</summary>
        public const string TableName = "bouquet_series";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<BouquetSeriesEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(row => row.Id);
            builder.Property(row => row.Id)
                .HasColumnName("id");

            builder.Property(row => row.BouquetId)
                .HasColumnName("bouquet_id")
                .IsRequired();

            builder.Property(row => row.SeriesId)
                .HasColumnName("series_id")
                .IsRequired();

            builder.Property(row => row.SortOrder)
                .HasColumnName("sort_order")
                .IsRequired();

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // A bouquet is deletable only while it holds nothing, and the service refuses a
            // non-empty one with a count before it reaches the database. Restrict is the
            // backstop that makes a bug in that check a failed request rather than a silent
            // unlink of content the operator never touched.
            //
            // The navigation is named here for the reason VideoStreamConfiguration gives:
            // without it the `Bouquet` property becomes a second relationship with a shadow key
            // column of its own.
            builder.HasOne(row => row.Bouquet)
                .WithMany()
                .HasForeignKey(row => row.BouquetId)
                .OnDelete(DeleteBehavior.Restrict);

            // A series that is deleted leaves no membership behind, in any bouquet.
            builder.HasOne<SeriesEntity>()
                .WithMany()
                .HasForeignKey(row => row.SeriesId)
                .OnDelete(DeleteBehavior.Cascade);

            // One series appears at most once in one bouquet; the same series in another bouquet
            // is a different row.
            builder.HasIndex(row => new { row.BouquetId, row.SeriesId })
                .IsUnique();

            // The read the content manager performs for one tab: every row of one bouquet,
            // lowest position first.
            builder.HasIndex(row => new { row.BouquetId, row.SortOrder });

            // The read the four edit forms perform: which bouquets hold this series.
            builder.HasIndex(row => row.SeriesId);
        }
    }
}
