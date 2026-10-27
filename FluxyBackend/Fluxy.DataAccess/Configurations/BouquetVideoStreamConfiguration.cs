using Fluxy.Core.Persistence.Base;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="BouquetVideoStreamEntity"/> to the <c>bouquet_video_streams</c> table. It
    /// is discovered by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class BouquetVideoStreamConfiguration
        : IEntityTypeConfiguration<BouquetVideoStreamEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name, columns
        /// use snake_case, and both live in the default schema of the database.</summary>
        public const string TableName = "bouquet_video_streams";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<BouquetVideoStreamEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(row => row.Id);
            builder.Property(row => row.Id)
                .HasColumnName("id");

            builder.Property(row => row.BouquetId)
                .HasColumnName("bouquet_id")
                .IsRequired();

            builder.Property(row => row.VideoStreamId)
                .HasColumnName("video_stream_id")
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
            // unlink of content the operator never touched - the same shape the catalogue's
            // ON DELETE RESTRICT has.
            //
            // The navigation is named here for the reason VideoStreamConfiguration gives:
            // without it the `Bouquet` property becomes a second relationship with a shadow key
            // column of its own.
            builder.HasOne(row => row.Bouquet)
                .WithMany()
                .HasForeignKey(row => row.BouquetId)
                .OnDelete(DeleteBehavior.Restrict);

            // A stream that is deleted leaves no membership behind, in any bouquet. This is the
            // whole reason each content kind has a table of its own instead of sharing one
            // polymorphic table with a bare identifier: the database does the cleanup on every
            // delete path that exists today and every one added later.
            builder.HasOne<VideoStreamEntity>()
                .WithMany()
                .HasForeignKey(row => row.VideoStreamId)
                .OnDelete(DeleteBehavior.Cascade);

            // One stream appears at most once in one bouquet; the same stream in another
            // bouquet is a different row, which is what makes a package a package rather than
            // an exclusive filing.
            builder.HasIndex(row => new { row.BouquetId, row.VideoStreamId })
                .IsUnique();

            // The read the content manager performs for one tab: every row of one bouquet,
            // lowest position first.
            builder.HasIndex(row => new { row.BouquetId, row.SortOrder });

            // The read the four edit forms perform: which bouquets hold this stream. The
            // composite index above cannot answer it, because it leads with the bouquet.
            builder.HasIndex(row => row.VideoStreamId);
        }
    }
}
