using Fluxy.Core.Persistence.Base;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="VideoStreamFileEntity"/> to the <c>video_stream_files</c> table. It is
    /// discovered by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class VideoStreamFileConfiguration : IEntityTypeConfiguration<VideoStreamFileEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name, columns
        /// use snake_case, and both live in the default schema of the database.</summary>
        public const string TableName = "video_stream_files";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<VideoStreamFileEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(file => file.Id);
            builder.Property(file => file.Id)
                .HasColumnName("id");

            builder.Property(file => file.StreamId)
                .HasColumnName("stream_id")
                .IsRequired();

            builder.Property(file => file.Position)
                .HasColumnName("position")
                .IsRequired();

            builder.Property(file => file.Path)
                .HasColumnName("path")
                .HasMaxLength(VideoStreamConfiguration.FilePathMaxLength)
                .IsRequired();

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // A file belongs to exactly one stream and dies with it, for the reason
            // <see cref="VideoStreamSourceConfiguration"/> gives. The navigation is named for
            // the same reason: without it the `Stream` property would become a second
            // relationship carrying a shadow key column of its own.
            builder.HasOne(file => file.Stream)
                .WithMany(stream => stream.StreamFiles)
                .HasForeignKey(file => file.StreamId)
                .OnDelete(DeleteBehavior.Cascade);

            // One slot per stream; see the sibling configuration for why the index is unique
            // rather than merely present.
            builder.HasIndex(file => new { file.StreamId, file.Position })
                .IsUnique();
        }
    }
}
