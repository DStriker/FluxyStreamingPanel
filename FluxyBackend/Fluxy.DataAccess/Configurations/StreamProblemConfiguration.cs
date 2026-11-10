using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Streams.Utils;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="StreamProblemEntity"/> to the <c>stream_problems</c> table. It is discovered
    /// by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The stream's foreign key cascades.</b> A problem is evidence about one configured
    /// stream: deleting the stream deletes the problems it produced, because a journal line whose
    /// subject no longer exists is what the operator asked to remove. The table is never orphaned,
    /// and no stream name is duplicated onto the row - the name is resolved by a join on read.
    /// </para>
    /// <para>
    /// <b>Two indexes are the two questions the screen asks.</b> <c>(created_at, id)</c> descending
    /// is the default order and the tie-break every page turn depends on; <c>(stream_id,
    /// created_at)</c> is "this stream's problems, newest first", which is the drill-down from a
    /// stream and the shape the <c>streamId</c> filter runs on.
    /// </para>
    /// <para>
    /// <b>The message width comes from <see cref="StreamProblemListLimits"/>.</b> The write
    /// boundary cuts a message to the same constant, so a column width and the rule that shortens
    /// a value to fit cannot drift apart.
    /// </para>
    /// <para>
    /// No column carries <c>HasDefaultValue</c>: a value the database invented for a column the
    /// application forgot would hide the mistake instead of surfacing it - the rule every table of
    /// this solution follows.
    /// </para>
    /// </remarks>
    public sealed class StreamProblemConfiguration : IEntityTypeConfiguration<StreamProblemEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name.</summary>
        public const string TableName = "stream_problems";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<StreamProblemEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(problem => problem.Id);
            builder.Property(problem => problem.Id)
                .HasColumnName("id");

            // A real reference, unlike the refusal log's opaque content identifier: a problem is
            // about one stream of this installation, so the key must name an existing row and the
            // cascade below removes the journal with it.
            builder.Property(problem => problem.StreamId)
                .HasColumnName("stream_id")
                .IsRequired();

            // The sentence a support call reads. Its width is the column's, and the service cuts
            // every message to the same constant.
            builder.Property(problem => problem.ErrorMessage)
                .HasColumnName("error_message")
                .HasMaxLength(StreamProblemListLimits.ErrorMessageMaxLength)
                .IsRequired();

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // A problem without the stream it was reported for is a row nobody can be told about,
            // so the row goes with it. The consequence is recorded here and in the entity's
            // remarks: deleting a stream removes its problems.
            builder.HasOne<VideoStreamEntity>()
                .WithMany()
                .HasForeignKey(problem => problem.StreamId)
                .OnDelete(DeleteBehavior.Cascade);

            // The default order, and the tie-break every page turn depends on: two problems can be
            // reported in the same instant, and without the identifier the database is free to
            // answer a different order on the next read.
            builder.HasIndex(problem => new { problem.CreatedAt, problem.Id })
                .IsDescending(true, true);

            // One stream's problems, oldest first - the drill-down from a stream, and the index the
            // `streamId` filter runs on.
            builder.HasIndex(problem => new { problem.StreamId, problem.CreatedAt });
        }
    }
}
