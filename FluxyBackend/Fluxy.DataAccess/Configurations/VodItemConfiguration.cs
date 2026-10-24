using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Vod.Utils;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="VodItemEntity"/> to the <c>vod_items</c> table. It is discovered by
    /// <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class VodItemConfiguration : IEntityTypeConfiguration<VodItemEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name.</summary>
        public const string TableName = "vod_items";

        /// <summary>
        /// The columns of the row. Every width is read from <see cref="VodLimits"/>, so the
        /// column and the rule that refuses an oversized value cannot drift apart.
        /// </summary>
        public void Configure(EntityTypeBuilder<VodItemEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id");

            builder.Property(item => item.Name)
                .HasColumnName("name")
                .HasMaxLength(VodLimits.NameMaxLength)
                .IsRequired();

            // Stored as smallint, like every other enum here. Both members are states or kinds
            // rather than levels, so the numeric order means nothing to anything that reads.
            builder.Property(item => item.Status)
                .HasColumnName("status")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(item => item.Type)
                .HasColumnName("vod_type")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(item => item.CategoryId)
                .HasColumnName("category_id")
                .IsRequired();

            builder.Property(item => item.Source)
                .HasColumnName("source")
                .HasMaxLength(VodLimits.SourceMaxLength)
                .IsRequired();

            builder.Property(item => item.SubtitleUrl)
                .HasColumnName("subtitle_url")
                .HasMaxLength(VodLimits.SubtitleUrlMaxLength);

            builder.Property(item => item.ImdbId)
                .HasColumnName("imdb_id")
                .HasMaxLength(VodLimits.ImdbIdMaxLength);

            builder.Property(item => item.PosterUrl)
                .HasColumnName("poster_url")
                .HasMaxLength(VodLimits.ImageUrlMaxLength);

            builder.Property(item => item.BackdropUrl)
                .HasColumnName("backdrop_url")
                .HasMaxLength(VodLimits.ImageUrlMaxLength);

            builder.Property(item => item.Plot)
                .HasColumnName("plot")
                .HasColumnType("text");

            // Two lists that are stored as lists. PostgreSQL's text[] keeps the order the
            // operator arranged, and Npgsql maps string[] to it without a conversion.
            builder.Property(item => item.Cast)
                .HasColumnName("cast")
                .HasColumnType("text[]");

            builder.Property(item => item.Director)
                .HasColumnName("director")
                .HasMaxLength(VodLimits.DirectorMaxLength);

            builder.Property(item => item.Genres)
                .HasColumnName("genres")
                .HasColumnType("text[]");

            // A calendar date rather than a timestamp: a release date has no time of day, and
            // storing one would invite a time zone question with no correct answer.
            builder.Property(item => item.ReleaseDate)
                .HasColumnName("release_date")
                .HasColumnType("date");

            builder.Property(item => item.Rating)
                .HasColumnName("rating")
                .HasColumnType("numeric(3,1)");

            builder.Property(item => item.CountryCode)
                .HasColumnName("country_code")
                .HasMaxLength(VodLimits.CountryCodeLength);

            builder.Property(nameof(AuditableEntity.CreatedAt)).HasColumnName("created_at");
            builder.Property(nameof(AuditableEntity.UpdatedAt)).HasColumnName("updated_at");

            // An item belongs to exactly one category, and deleting the category must not
            // strand the videos filed under it. Restrict for the same reason the streams use
            // it: the operator is told which one is standing in the way instead of a category
            // vanishing along with a screenful of rows nobody asked about.
            builder.HasOne(item => item.Category)
                .WithMany()
                .HasForeignKey(item => item.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // One name per category, compared case sensitively for the same reason username
            // and email are. Scoped to the category because two categories are two sections of
            // a catalogue, and "Dune" filed under two of them is two unrelated rows.
            builder.HasIndex(item => new { item.CategoryId, item.Name })
                .IsUnique();

            // The filters and sorts the table offers.
            builder.HasIndex(item => item.Status);
            builder.HasIndex(item => item.Type);
            builder.HasIndex(item => item.ReleaseDate);
            builder.HasIndex(item => item.Rating);
        }
    }
}
