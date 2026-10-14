using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Streams;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="VideoStreamEntity"/> to the <c>video_streams</c> table. It is discovered
    /// by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class VideoStreamConfiguration : IEntityTypeConfiguration<VideoStreamEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name, columns
        /// use snake_case, and both live in the default schema of the database.</summary>
        public const string TableName = "video_streams";

        /// <summary>
        /// Longest display name. A stream is one line of a table beside a column of category
        /// names, so the room above this length is for a name nobody has thought of rather
        /// than for one this length would already hold.
        /// </summary>
        public const int NameMaxLength = 200;

        /// <summary>Longest icon address. A URL, and one that a browser has to be able to fetch.</summary>
        public const int LogoUrlMaxLength = 500;

        /// <summary>Longest source the relaying process may report back as the one it is using.</summary>
        public const int ActiveSourceMaxLength = 1000;

        /// <summary>
        /// Longest service reference. An Enigma2 reference is a handful of colon separated
        /// numbers (<c>1:0:19:1234:0:0:0:0:0:0:</c>), so this is room rather than a limit.
        /// </summary>
        public const int Enigma2SidMaxLength = 200;

        /// <summary>Longest channel key inside a programme guide.</summary>
        public const int EpgChannelKeyMaxLength = 200;

        /// <summary>Longest user agent a relayed request may carry.</summary>
        public const int RestreamUserAgentMaxLength = 500;

        /// <summary>Longest proxy address a relay may go out through.</summary>
        public const int RestreamProxyMaxLength = 500;

        /// <summary>Longest URL a stream relays from.</summary>
        public const int SourceUrlMaxLength = 2000;

        /// <summary>Longest path of a file a stream plays.</summary>
        public const int FilePathMaxLength = 1000;

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<VideoStreamEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(stream => stream.Id);
            builder.Property(stream => stream.Id)
                .HasColumnName("id");

            builder.Property(stream => stream.Name)
                .HasColumnName("name")
                .HasMaxLength(NameMaxLength)
                .IsRequired();

            // Stored as smallint, like every other enum here. Both members are states or kinds
            // rather than levels, so the numeric order means nothing to anything that reads.
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

            builder.Property(stream => stream.DelayMinutes)
                .HasColumnName("delay_minutes")
                .IsRequired();

            // Three references to rows of domains that do not exist in this database yet, so
            // they are plain uuids rather than foreign keys. There is nothing for a constraint
            // to point at, and inventing a table to point at would be inventing a domain that
            // nobody asked for. The relaying process resolves them.
            builder.Property(stream => stream.SeriesId)
                .HasColumnName("series_id");

            builder.Property(stream => stream.VodId)
                .HasColumnName("vod_id");

            builder.Property(stream => stream.EpgSourceId)
                .HasColumnName("epg_source_id");

            builder.Property(stream => stream.EpgChannelKey)
                .HasColumnName("epg_channel_key")
                .HasMaxLength(EpgChannelKeyMaxLength);

            builder.Property(stream => stream.ArchiveDays)
                .HasColumnName("archive_days")
                .IsRequired();

            builder.Property(stream => stream.SortOrder)
                .HasColumnName("sort_order")
                .IsRequired();

            builder.Property(stream => stream.RestreamUserAgent)
                .HasColumnName("restream_user_agent")
                .HasMaxLength(RestreamUserAgentMaxLength);

            builder.Property(stream => stream.RestreamProxy)
                .HasColumnName("restream_proxy")
                .HasMaxLength(RestreamProxyMaxLength);

            // The last two are free text on purpose. The relaying process decides how to read
            // the headers, and a structure fixed here - a table, a JSON object, a list of
            // name-value rows - would be this side having an opinion about a format it never
            // uses.
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
            // strand the streams filed under it. Restrict rather than cascade for the same
            // reason the accounts' group is restrict: the operator is told which one is
            // standing in the way instead of a category vanishing along with a screenful of
            // rows nobody asked about. The refusal is answered with a code by the category
            // service, which counts the rows this key protects.
            //
            // The navigation is named here on purpose. Writing `HasOne<ContentCategoryEntity>()`
            // without it would leave the `Category` property as a *second* relationship of its
            // own, and EF would give it a shadow key column beside the real one - which is
            // exactly what the first scaffold of this migration produced: two foreign keys
            // pointing at the same table, one of them a column nothing ever writes.
            builder.HasOne(stream => stream.Category)
                .WithMany()
                .HasForeignKey(stream => stream.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // One name per category, compared case sensitively for the same reason username
            // and email are: PostgreSQL decides what "equal" means for a varchar from the
            // collation of the column, and the default one compares the bytes.
            //
            // Scoped to the category rather than installation-wide on purpose. Two categories
            // are two sections of a channel list, and "News" filed under general and "News"
            // filed under sports are two channels that a viewer sees on different screens -
            // forbidding the second because the first exists would be a rule about nothing.
            builder.HasIndex(stream => new { stream.CategoryId, stream.Name })
                .IsUnique();

            // The read this table exists for: one category's streams, in the order the table
            // shows them. The category leads so the index answers it as a range scan.
            builder.HasIndex(stream => new { stream.CategoryId, stream.SortOrder });

            // The two filters the table offers, each narrow on its own.
            builder.HasIndex(stream => stream.Status);
            builder.HasIndex(stream => stream.Type);
        }
    }
}
