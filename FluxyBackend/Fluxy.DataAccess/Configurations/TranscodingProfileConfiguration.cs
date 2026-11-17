using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Transcoding.Utils;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="TranscodingProfileEntity"/> to the <c>transcoding_profiles</c> table. It
    /// is discovered by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class TranscodingProfileConfiguration
        : IEntityTypeConfiguration<TranscodingProfileEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name.</summary>
        public const string TableName = "transcoding_profiles";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<TranscodingProfileEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(profile => profile.Id);
            builder.Property(profile => profile.Id).HasColumnName("id");

            builder.Property(profile => profile.Name)
                .HasColumnName("name")
                .HasMaxLength(TranscodingProfileLimits.NameMaxLength)
                .IsRequired();

            // The four enums are stored as smallint, like every other enum here. The codec and
            // preset lists are a set of unrelated encoders rather than a scale, so the numeric
            // order means nothing and only the member names travel to ffmpeg.
            builder.Property(profile => profile.VideoCodec)
                .HasColumnName("video_codec")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(profile => profile.AudioCodec)
                .HasColumnName("audio_codec")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(profile => profile.Preset)
                .HasColumnName("preset")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(profile => profile.VideoProfile)
                .HasColumnName("video_profile")
                .HasConversion<short?>();

            // Every numeric option is a plain integer with no database default, per the project
            // rule: a value the database invented when the application forgot a column would
            // hide the mistake. Null is the state "no flag", not a missing value.
            builder.Property(profile => profile.VideoBitrateAvg).HasColumnName("video_bitrate_avg");
            builder.Property(profile => profile.AudioBitrateAvg).HasColumnName("audio_bitrate_avg");

            builder.Property(profile => profile.MinBitrateTolerance)
                .HasColumnName("min_bitrate_tolerance");

            builder.Property(profile => profile.MaxBitrateTolerance)
                .HasColumnName("max_bitrate_tolerance");

            builder.Property(profile => profile.BufferSize).HasColumnName("buffer_size");
            builder.Property(profile => profile.Crf).HasColumnName("crf");
            builder.Property(profile => profile.Width).HasColumnName("width");
            builder.Property(profile => profile.Height).HasColumnName("height");

            builder.Property(profile => profile.AspectRatio)
                .HasColumnName("aspect_ratio")
                .HasMaxLength(TranscodingProfileLimits.AspectRatioMaxLength);

            // A decimal option needs a precision the provider can pin: ffmpeg's own frame rates
            // are thousandths of a hertz (29.970), so three decimal places hold every one.
            builder.Property(profile => profile.Framerate)
                .HasColumnName("framerate")
                .HasPrecision(9, 3);

            builder.Property(profile => profile.AudioSampleRate).HasColumnName("audio_sample_rate");
            builder.Property(profile => profile.AudioChannels).HasColumnName("audio_channels");
            builder.Property(profile => profile.Threads).HasColumnName("threads");

            builder.Property(profile => profile.LogoUrl)
                .HasColumnName("logo_url")
                .HasMaxLength(TranscodingProfileLimits.LogoUrlMaxLength);

            builder.Property(profile => profile.RedactionX).HasColumnName("redaction_x");
            builder.Property(profile => profile.RedactionY).HasColumnName("redaction_y");
            builder.Property(profile => profile.RedactionWidth).HasColumnName("redaction_width");
            builder.Property(profile => profile.RedactionHeight).HasColumnName("redaction_height");

            builder.Property(nameof(AuditableEntity.CreatedAt)).HasColumnName("created_at");
            builder.Property(nameof(AuditableEntity.UpdatedAt)).HasColumnName("updated_at");

            // One name per installation, compared case sensitively for the same reason the guide
            // sources are: PostgreSQL decides what "equal" means for a varchar from the
            // collation of the column, and the default one compares the bytes. The index is
            // named by hand because the service matches a refused write against this exact name.
            builder.HasIndex(profile => profile.Name)
                .IsUnique()
                .HasDatabaseName(TranscodingProfileLimits.NameIndexName);
        }
    }
}
