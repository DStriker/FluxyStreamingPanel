using Fluxy.Core.Epg.Utils;
using Fluxy.Core.Persistence.Base;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="EpgDataEntity"/> to the <c>epg_data</c> table. It is discovered by
    /// <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class EpgDataConfiguration : IEntityTypeConfiguration<EpgDataEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name.</summary>
        public const string TableName = "epg_data";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<EpgDataEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(programme => programme.Id);
            builder.Property(programme => programme.Id).HasColumnName("id");

            builder.Property(programme => programme.SourceId)
                .HasColumnName("source_id")
                .IsRequired();

            builder.Property(programme => programme.ChannelId)
                .HasColumnName("channel_id")
                .HasMaxLength(EpgDataLimits.ChannelIdMaxLength)
                .IsRequired();

            builder.Property(programme => programme.Title)
                .HasColumnName("title")
                .HasMaxLength(EpgDataLimits.TitleMaxLength)
                .IsRequired();

            builder.Property(programme => programme.Language)
                .HasColumnName("language")
                .HasMaxLength(EpgDataLimits.LanguageMaxLength);

            builder.Property(programme => programme.StartDate)
                .HasColumnName("start_date")
                .IsRequired();

            builder.Property(programme => programme.EndDate)
                .HasColumnName("end_date")
                .IsRequired();

            builder.Property(programme => programme.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            builder.Property(nameof(AuditableEntity.CreatedAt)).HasColumnName("created_at");
            builder.Property(nameof(AuditableEntity.UpdatedAt)).HasColumnName("updated_at");

            // A guide's programmes are its own rows, so they go when it goes: the updater
            // deletes a window of them on every refresh, and deleting the source itself must
            // not leave a schedule behind that nothing can address.
            builder.HasOne<EpgSourceEntity>()
                .WithMany()
                .HasForeignKey(programme => programme.SourceId)
                .OnDelete(DeleteBehavior.Cascade);

            // The five indexes the domain asks for. Each is narrow on its own: a refresh
            // deletes by source, a channel is read by key, a language is filtered, and a
            // window of the schedule is read by its two bounds.
            builder.HasIndex(programme => programme.SourceId);
            builder.HasIndex(programme => programme.ChannelId);
            builder.HasIndex(programme => programme.Language);
            builder.HasIndex(programme => programme.StartDate);
            builder.HasIndex(programme => programme.EndDate);
        }
    }
}
