using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Series.Utils;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="EpisodeEntity"/> to the <c>series_episodes</c> table. It is discovered
    /// by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class EpisodeConfiguration : IEntityTypeConfiguration<EpisodeEntity>
    {
        /// <summary>Name of the table.</summary>
        public const string TableName = "series_episodes";

        /// <summary>Longest display name.</summary>
        public const int NameMaxLength = SeriesLimits.EpisodeNameMaxLength;

        /// <summary>Longest source address.</summary>
        public const int SourceMaxLength = SeriesLimits.SourceMaxLength;

        /// <summary>Longest image address.</summary>
        public const int ImageUrlMaxLength = SeriesLimits.ImageUrlMaxLength;

        /// <summary>Longest IMDb identifier.</summary>
        public const int ImdbIdMaxLength = SeriesLimits.ImdbIdMaxLength;

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<EpisodeEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(episode => episode.Id);
            builder.Property(episode => episode.Id)
                .HasColumnName("id");

            builder.Property(episode => episode.SeriesId)
                .HasColumnName("series_id")
                .IsRequired();

            builder.Property(episode => episode.SeasonNumber)
                .HasColumnName("season_number")
                .IsRequired();

            builder.Property(episode => episode.EpisodeNumber)
                .HasColumnName("episode_number")
                .IsRequired();

            builder.Property(episode => episode.Name)
                .HasColumnName("name")
                .HasMaxLength(NameMaxLength);

            builder.Property(episode => episode.Source)
                .HasColumnName("source")
                .HasMaxLength(SourceMaxLength)
                .IsRequired();

            builder.Property(episode => episode.Status)
                .HasColumnName("status")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(episode => episode.Type)
                .HasColumnName("episode_type")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(episode => episode.ImdbId)
                .HasColumnName("imdb_id")
                .HasMaxLength(ImdbIdMaxLength);

            builder.Property(episode => episode.ImageUrl)
                .HasColumnName("image_url")
                .HasMaxLength(ImageUrlMaxLength);

            builder.Property(episode => episode.Plot)
                .HasColumnName("plot")
                .HasColumnType("text");

            builder.Property(episode => episode.ReleaseDate)
                .HasColumnName("release_date")
                .HasColumnType("date");

            builder.Property(episode => episode.Rating)
                .HasColumnName("rating")
                .HasColumnType("numeric(3,1)");

            builder.Property(nameof(AuditableEntity.CreatedAt)).HasColumnName("created_at");
            builder.Property(nameof(AuditableEntity.UpdatedAt)).HasColumnName("updated_at");

            // One episode per season-and-number pair inside a series. This is what makes the
            // add-season form's numbering safe against a concurrent create: the second writer
            // loses the race and is answered as a conflict rather than creating a duplicate.
            builder.HasIndex(episode => new
                {
                    episode.SeriesId,
                    episode.SeasonNumber,
                    episode.EpisodeNumber
                })
                .IsUnique();

            // The filters and sorts the episode table offers, each preceded by the series the
            // query is always scoped to.
            builder.HasIndex(episode => new { episode.SeriesId, episode.Status });
            builder.HasIndex(episode => new { episode.SeriesId, episode.Type });
            builder.HasIndex(episode => new { episode.SeriesId, episode.ReleaseDate });
            builder.HasIndex(episode => new { episode.SeriesId, episode.Rating });
        }
    }
}
