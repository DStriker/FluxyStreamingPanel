using Fluxy.Core.Persistence.Base;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="VideoStreamVodItemEntity"/> to the <c>video_stream_vod_items</c> table.
    /// It is discovered by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class VideoStreamVodItemConfiguration
        : IEntityTypeConfiguration<VideoStreamVodItemEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name, columns
        /// use snake_case, and both live in the default schema of the database.</summary>
        public const string TableName = "video_stream_vod_items";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<VideoStreamVodItemEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(entry => entry.Id);
            builder.Property(entry => entry.Id)
                .HasColumnName("id");

            builder.Property(entry => entry.StreamId)
                .HasColumnName("stream_id")
                .IsRequired();

            builder.Property(entry => entry.VodItemId)
                .HasColumnName("vod_item_id")
                .IsRequired();

            builder.Property(entry => entry.Position)
                .HasColumnName("position")
                .IsRequired();

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // An entry belongs to exactly one stream and dies with it, for the reason
            // VideoStreamSourceConfiguration gives. The navigation is named for the same
            // reason: without it the `Stream` property would become a second relationship with
            // a shadow key column of its own.
            builder.HasOne(entry => entry.Stream)
                .WithMany(stream => stream.VodItems)
                .HasForeignKey(entry => entry.StreamId)
                .OnDelete(DeleteBehavior.Cascade);

            // The video named is a row of this database, so the key is real: a playlist cannot
            // hold a video that does not exist, and deleting a video empties its place in every
            // playlist rather than leaving a step that resolves to nothing.
            builder.HasOne<VodItemEntity>()
                .WithMany()
                .HasForeignKey(entry => entry.VodItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // One slot per stream; see the sibling configuration for why the index is unique
            // rather than merely present.
            builder.HasIndex(entry => new { entry.StreamId, entry.Position })
                .IsUnique();

            // The read that runs when a video is deleted: the foreign key needs an index on the
            // referencing column, and the composite above leads with the stream.
            builder.HasIndex(entry => entry.VodItemId);
        }
    }
}
