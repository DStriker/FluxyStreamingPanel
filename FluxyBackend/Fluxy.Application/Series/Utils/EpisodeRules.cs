using Fluxy.Application.Content.Utils;
using Fluxy.Core.Series.Commands;
using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Utils;

namespace Fluxy.Application.Series.Utils
{
    /// <summary>
    /// The rules and sentences every write to an episode or a season shares.
    /// </summary>
    public static class EpisodeRules
    {
        /// <summary>The sentence an episode name that is too long gets.</summary>
        public static readonly string[] NameRule =
        [
            "The episode name must be " + SeriesLimits.EpisodeNameMaxLength +
                " characters or fewer."
        ];

        /// <summary>The sentence a source that is blank or too long gets.</summary>
        public static readonly string[] SourceRule =
        [
            "The source must be between 1 and " + SeriesLimits.SourceMaxLength +
                " characters, either a URL or a path to a file."
        ];

        /// <summary>The sentence a status that names no member gets.</summary>
        public static readonly string[] StatusRule =
            ["The status must be either active or inactive."];

        /// <summary>The sentence a delivery kind that names no member gets.</summary>
        public static readonly string[] TypeRule =
            ["The type must be either relay or redirect."];

        /// <summary>The sentence a season number outside the accepted range gets.</summary>
        public static readonly string[] SeasonRule =
        [
            "The season number must be between " + SeriesLimits.SeasonNumberMin +
                " and " + SeriesLimits.SeasonNumberMax + "."
        ];

        /// <summary>The sentence an episode number outside the accepted range gets.</summary>
        public static readonly string[] EpisodeNumberRule =
        [
            "The episode number must be between " + SeriesLimits.EpisodeNumberMin +
                " and " + SeriesLimits.EpisodeNumberMax + "."
        ];

        /// <summary>The sentence an IMDb identifier that is not one gets.</summary>
        public static readonly string[] ImdbRule =
            ["The IMDb identifier must be 'tt' followed by 5 to 10 digits."];

        /// <summary>The sentence an image address that is too long gets.</summary>
        public static readonly string[] ImageRule =
        [
            "The image address must be " + SeriesLimits.ImageUrlMaxLength +
                " characters or fewer."
        ];

        /// <summary>The sentence a synopsis that is too long gets.</summary>
        public static readonly string[] PlotRule =
            ["The synopsis must be " + SeriesLimits.PlotMaxLength + " characters or fewer."];

        /// <summary>The sentence a release date that is not <c>yyyy-MM-dd</c> gets.</summary>
        public static readonly string[] ReleaseDateRule =
            ["The release date must be written as yyyy-MM-dd."];

        /// <summary>The sentence a rating outside 0..10 or with more than one decimal gets.</summary>
        public static readonly string[] RatingRule =
            ["The rating must be between 0 and 10, with at most one decimal place."];

        /// <summary>The sentence a folder path that is blank or too long gets.</summary>
        public static readonly string[] FolderRule =
        [
            "The folder path must be between 1 and " + SeriesLimits.FolderPathMaxLength +
                " characters."
        ];

        /// <summary>The unique index making a season-and-number pair unique inside a series.</summary>
        public const string NumberIndexName =
            "IX_series_episodes_series_id_season_number_episode_number";

        /// <summary>Whether a source is non-blank and short enough to store.</summary>
        /// <param name="source">Source to judge.</param>
        public static bool IsUsableSource(string source)
            => source.Trim().Length is >= 1 and <= SeriesLimits.SourceMaxLength;

        /// <summary>
        /// Checks everything about an episode that can be judged without asking the database.
        /// </summary>
        public static Dictionary<string, string[]> Validate(
            Guid seriesId,
            int seasonNumber,
            int episodeNumber,
            string? name,
            string source,
            EpisodeStatus status,
            EpisodeType type,
            string? imdbId,
            string? imageUrl,
            string? plot,
            string? releaseDate,
            decimal? rating)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (seriesId == Guid.Empty)
            {
                errors[nameof(NewEpisode.SeriesId)] = ["The episode must belong to a series."];
            }

            if (!IsUsableSource(source))
            {
                errors[nameof(NewEpisode.Source)] = SourceRule;
            }

            if (seasonNumber is < SeriesLimits.SeasonNumberMin or > SeriesLimits.SeasonNumberMax)
            {
                errors[nameof(NewEpisode.SeasonNumber)] = SeasonRule;
            }

            if (episodeNumber is < SeriesLimits.EpisodeNumberMin
                or > SeriesLimits.EpisodeNumberMax)
            {
                errors[nameof(NewEpisode.EpisodeNumber)] = EpisodeNumberRule;
            }

            if (!string.IsNullOrWhiteSpace(name)
                && name.Trim().Length > SeriesLimits.EpisodeNameMaxLength)
            {
                errors[nameof(NewEpisode.Name)] = NameRule;
            }

            if (!Enum.IsDefined(status))
            {
                errors[nameof(NewEpisode.Status)] = StatusRule;
            }

            if (!Enum.IsDefined(type))
            {
                errors[nameof(NewEpisode.Type)] = TypeRule;
            }

            if (!string.IsNullOrWhiteSpace(imdbId)
                && (imdbId.Trim().Length > SeriesLimits.ImdbIdMaxLength
                    || !ContentMetadataRules.IsImdbId(imdbId.Trim())))
            {
                errors[nameof(NewEpisode.ImdbId)] = ImdbRule;
            }

            if (!ContentMetadataRules.Fits(imageUrl, SeriesLimits.ImageUrlMaxLength))
            {
                errors[nameof(NewEpisode.ImageUrl)] = ImageRule;
            }

            if (!string.IsNullOrWhiteSpace(plot) && plot.Trim().Length > SeriesLimits.PlotMaxLength)
            {
                errors[nameof(NewEpisode.Plot)] = PlotRule;
            }

            if (!ContentMetadataRules.TryParseReleaseDate(releaseDate, out _))
            {
                errors[nameof(NewEpisode.ReleaseDate)] = ReleaseDateRule;
            }

            if (rating is { } value
                && (value < SeriesLimits.RatingMin
                    || value > SeriesLimits.RatingMax
                    || decimal.Round(value, 1) != value))
            {
                errors[nameof(NewEpisode.Rating)] = RatingRule;
            }

            return errors;
        }

        /// <summary>
        /// Checks everything about a season request that can be judged without touching the
        /// file system or the database.
        /// </summary>
        public static Dictionary<string, string[]> ValidateSeason(
            Guid seriesId,
            int seasonNumber,
            string folderPath,
            EpisodeStatus status,
            EpisodeType type)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (seriesId == Guid.Empty)
            {
                errors[nameof(NewSeason.SeriesId)] = ["The season must belong to a series."];
            }

            if (seasonNumber is < SeriesLimits.SeasonNumberMin or > SeriesLimits.SeasonNumberMax)
            {
                errors[nameof(NewSeason.SeasonNumber)] = SeasonRule;
            }

            if (folderPath.Trim().Length is 0 or > SeriesLimits.FolderPathMaxLength)
            {
                errors[nameof(NewSeason.FolderPath)] = FolderRule;
            }

            if (!Enum.IsDefined(status))
            {
                errors[nameof(NewSeason.Status)] = StatusRule;
            }

            if (!Enum.IsDefined(type))
            {
                errors[nameof(NewSeason.Type)] = TypeRule;
            }

            return errors;
        }
    }
}
