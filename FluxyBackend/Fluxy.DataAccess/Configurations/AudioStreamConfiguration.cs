using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Streams.Utils;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="AudioStreamEntity"/> to the <c>audio_streams</c> table. It is discovered
    /// by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class AudioStreamConfiguration : IEntityTypeConfiguration<AudioStreamEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name, columns
        /// use snake_case, and both live in the default schema of the database.</summary>
        public const string TableName = "audio_streams";

        /// <summary>Longest display name.</summary>
        public const int NameMaxLength = AudioStreamLimits.NameMaxLength;

        /// <summary>Longest icon address.</summary>
        public const int LogoUrlMaxLength = AudioStreamLimits.LogoUrlMaxLength;

        /// <summary>Longest source the relaying process may report back as the one it is using.</summary>
        public const int ActiveSourceMaxLength = AudioStreamLimits.ActiveSourceMaxLength;

        /// <summary>Longest service reference.</summary>
        public const int Enigma2SidMaxLength = AudioStreamLimits.Enigma2SidMaxLength;

        /// <summary>Longest user agent a relayed request may carry.</summary>
        public const int RestreamUserAgentMaxLength = AudioStreamLimits.RestreamUserAgentMaxLength;

        /// <summary>Longest proxy address a relay may go out through.</summary>
        public const int RestreamProxyMaxLength = AudioStreamLimits.RestreamProxyMaxLength;

        /// <summary>Longest URL a stream relays from.</summary>
        public const int SourceUrlMaxLength = AudioStreamLimits.SourceUrlMaxLength;

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<AudioStreamEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(stream => stream.Id);
            builder.Property(stream => stream.Id)
                .HasColumnName("id");

            builder.Property(stream => stream.Name)
                .HasColumnName("name")
                .HasMaxLength(NameMaxLength)
                .IsRequired();

            // Stored as smallint, like every other enum here.
            builder.Property(stream => stream.Status)
                .HasColumnName("status")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(stream => stream.Type)
                .HasColumnName("stream_type")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(stream => stream.CategoryId)
                .HasColumnName("category_id")
                .IsRequired();

            builder.Property(stream => stream.LogoUrl)
                .HasColumnName("logo_url")
                .HasMaxLength(LogoUrlMaxLength);

            builder.Property(stream => stream.ActiveSource)
                .HasColumnName("active_source")
                .HasMaxLength(ActiveSourceMaxLength);

            builder.Property(stream => stream.Enigma2Sid)
                .HasColumnName("enigma2_sid")
                .HasMaxLength(Enigma2SidMaxLength);

            builder.Property(stream => stream.RestreamUserAgent)
                .HasColumnName("restream_user_agent")
                .HasMaxLength(RestreamUserAgentMaxLength);

            builder.Property(stream => stream.RestreamProxy)
                .HasColumnName("restream_proxy")
                .HasMaxLength(RestreamProxyMaxLength);

            // Free text on purpose, for the reason the video configuration gives: the relaying
            // process decides how to read the headers, and a structure fixed here would be
            // this side having an opinion about a format it never uses.
            builder.Property(stream => stream.RestreamCookies)
                .HasColumnName("restream_cookies")
                .HasColumnType("text");

            builder.Property(stream => stream.RestreamHeaders)
                .HasColumnName("restream_headers")
                .HasColumnType("text");

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // A stream belongs to exactly one category, and deleting the category must not
            // strand the streams filed under it. Restrict rather than cascade: the operator is
            // told which one is standing in the way instead of a category vanishing along with
            // a screenful of rows nobody asked about.
            //
            // The navigation is named here on purpose, for the reason the video configuration
            // gives: without it the `Category` property becomes a second relationship with a
            // shadow key column of its own.
            builder.HasOne(stream => stream.Category)
                .WithMany()
                .HasForeignKey(stream => stream.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // One name per category, compared case sensitively. Scoped to the category rather
            // than installation-wide: two categories are two sections of a list, and the same
            // name filed under each is two streams a viewer sees on different screens.
            builder.HasIndex(stream => new { stream.CategoryId, stream.Name })
                .IsUnique();

            // The two filters the table offers, each narrow on its own.
            builder.HasIndex(stream => stream.Status);
            builder.HasIndex(stream => stream.Type);
        }
    }
}
