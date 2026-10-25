using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Series.Utils;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="SeriesEntity"/> to the <c>series</c> table. It is discovered by
    /// <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class SeriesConfiguration : IEntityTypeConfiguration<SeriesEntity>
    {
        /// <summary>Name of the table.</summary>
        public const string TableName = "series";

        /// <summary>Longest display name.</summary>
        public const int NameMaxLength = SeriesLimits.NameMaxLength;

        /// <summary>Longest poster address.</summary>
        public const int PosterUrlMaxLength = SeriesLimits.ImageUrlMaxLength;

        /// <summary>Longest backdrop address.</summary>
        public const int BackdropUrlMaxLength = SeriesLimits.ImageUrlMaxLength;

        /// <summary>Longest director name.</summary>
        public const int DirectorMaxLength = SeriesLimits.DirectorMaxLength;

        /// <summary>Longest IMDb identifier.</summary>
        public const int ImdbIdMaxLength = SeriesLimits.ImdbIdMaxLength;

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<SeriesEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(series => series.Id);
            builder.Property(series => series.Id)
                .HasColumnName("id");

            builder.Property(series => series.Name)
                .HasColumnName("name")
                .HasMaxLength(NameMaxLength)
                .IsRequired();

            // Stored as smallint, like every other enum here.
            builder.Property(series => series.Status)
                .HasColumnName("status")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(series => series.CategoryId)
                .HasColumnName("category_id")
                .IsRequired();

            builder.Property(series => series.ImdbId)
                .HasColumnName("imdb_id")
                .HasMaxLength(ImdbIdMaxLength);

            builder.Property(series => series.PosterUrl)
                .HasColumnName("poster_url")
                .HasMaxLength(PosterUrlMaxLength);

            builder.Property(series => series.BackdropUrl)
                .HasColumnName("backdrop_url")
                .HasMaxLength(BackdropUrlMaxLength);

            builder.Property(series => series.Plot)
                .HasColumnName("plot")
                .HasColumnType("text");

            // Two lists stored as lists. PostgreSQL's text[] keeps the order the operator
            // arranged, and Npgsql maps string[] to it without a conversion.
            builder.Property(series => series.Cast)
                .HasColumnName("cast")
                .HasColumnType("text[]");

            builder.Property(series => series.Director)
                .HasColumnName("director")
                .HasMaxLength(DirectorMaxLength);

            builder.Property(series => series.Genres)
                .HasColumnName("genres")
                .HasColumnType("text[]");

            // A calendar date rather than a timestamp: a release date has no time of day.
            builder.Property(series => series.ReleaseDate)
                .HasColumnName("release_date")
                .HasColumnType("date");

            builder.Property(series => series.Rating)
                .HasColumnName("rating")
                .HasColumnType("numeric(3,1)");

            builder.Property(nameof(AuditableEntity.CreatedAt)).HasColumnName("created_at");
            builder.Property(nameof(AuditableEntity.UpdatedAt)).HasColumnName("updated_at");

            // A series belongs to exactly one category, and deleting the category must not
            // strand the series filed under it. Restrict for the same reason the streams use
            // it: the operator is told which one is standing in the way.
            builder.HasOne(series => series.Category)
                .WithMany()
                .HasForeignKey(series => series.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // The episodes are the series' own children, so deleting the series deletes them.
            builder.HasMany(series => series.Episodes)
                .WithOne(episode => episode.Series)
                .HasForeignKey(episode => episode.SeriesId)
                .OnDelete(DeleteBehavior.Cascade);

            // One name per category. Scoped to the category for the same reason the on-demand
            // catalogue scopes its own: two categories are two sections of a catalogue.
            builder.HasIndex(series => new { series.CategoryId, series.Name })
                .IsUnique();

            // The filters and sorts the table offers.
            builder.HasIndex(series => series.Status);
            builder.HasIndex(series => series.ReleaseDate);
            builder.HasIndex(series => series.Rating);
        }
    }
}
