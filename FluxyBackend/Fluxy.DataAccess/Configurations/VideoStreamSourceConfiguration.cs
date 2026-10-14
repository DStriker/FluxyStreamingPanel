using Fluxy.Core.Abstractions;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="VideoStreamSourceEntity"/> to the <c>video_stream_sources</c> table. It
    /// is discovered by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class VideoStreamSourceConfiguration
        : IEntityTypeConfiguration<VideoStreamSourceEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name, columns
        /// use snake_case, and both live in the default schema of the database.</summary>
        public const string TableName = "video_stream_sources";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<VideoStreamSourceEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(source => source.Id);
            builder.Property(source => source.Id)
                .HasColumnName("id");

            builder.Property(source => source.StreamId)
                .HasColumnName("stream_id")
                .IsRequired();

            builder.Property(source => source.Position)
                .HasColumnName("position")
                .IsRequired();

            builder.Property(source => source.Url)
                .HasColumnName("url")
                .HasMaxLength(VideoStreamConfiguration.SourceUrlMaxLength)
                .IsRequired();

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // A source belongs to exactly one stream and dies with it. Restrict would be a
            // refusal nothing needs to answer: the stream is the whole subject of these rows,
            // and a source whose stream is gone is a source for nothing.
            //
            // The navigation is named here for the reason
            // <see cref="VideoStreamConfiguration"/> gives: without it the `Stream` property
            // becomes a second relationship with a shadow key column of its own.
            builder.HasOne(source => source.Stream)
                .WithMany(stream => stream.StreamSources)
                .HasForeignKey(source => source.StreamId)
                .OnDelete(DeleteBehavior.Cascade);

            // One slot per stream. The list is replaced wholesale on every save, so the
            // positions are written by the service as 0..N-1 and this index is what makes
            // "the order is what the rows say" true without the application having to
            // remember not to write the same slot twice.
            builder.HasIndex(source => new { source.StreamId, source.Position })
                .IsUnique();
        }
    }
}
