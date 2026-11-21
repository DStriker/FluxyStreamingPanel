using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Settings.Utils;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="MagSettingsEntity"/> to the <c>mag_settings</c> table. It is discovered by
    /// <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    /// <remarks>
    /// One row per installation; every column is non-nullable and declares no database default.
    /// Both model lists are <c>text[]</c> columns, the shape the server country codes use: read
    /// with the row, never queried by position.
    /// </remarks>
    public sealed class MagSettingsConfiguration : IEntityTypeConfiguration<MagSettingsEntity>
    {
        /// <summary>Name of the table.</summary>
        public const string TableName = "mag_settings";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<MagSettingsEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(settings => settings.Id);
            builder.Property(settings => settings.Id).HasColumnName("id");

            builder.Property(settings => settings.ShowAllCategories)
                .HasColumnName("show_all_categories")
                .IsRequired();

            builder.Property(settings => settings.StrictSecurity)
                .HasColumnName("strict_security")
                .IsRequired();

            builder.Property(settings => settings.ShowChannelLogos)
                .HasColumnName("show_channel_logos")
                .IsRequired();

            builder.Property(settings => settings.ShowPreviewLogos)
                .HasColumnName("show_preview_logos")
                .IsRequired();

            builder.Property(settings => settings.DefaultContainer)
                .HasColumnName("default_container")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(settings => settings.DefaultTheme)
                .HasColumnName("default_theme")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(settings => settings.RecordingMaxLengthMinutes)
                .HasColumnName("recording_max_length_minutes")
                .IsRequired();

            builder.Property(settings => settings.MaxLocalRecordings)
                .HasColumnName("max_local_recordings")
                .IsRequired();

            builder.Property(settings => settings.PlaybackLimit)
                .HasColumnName("playback_limit")
                .IsRequired();

            builder.Property(settings => settings.AllowedStbTypes)
                .HasColumnName("allowed_stb_types")
                .HasColumnType("text[]")
                .IsRequired();

            builder.Property(settings => settings.AllowedRecordingStbTypes)
                .HasColumnName("allowed_recording_stb_types")
                .HasColumnType("text[]")
                .IsRequired();

            builder.Property(nameof(AuditableEntity.CreatedAt)).HasColumnName("created_at");
            builder.Property(nameof(AuditableEntity.UpdatedAt)).HasColumnName("updated_at");
        }
    }
}
