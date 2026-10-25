using Fluxy.Core.Series.Enums;
using Fluxy.Core.Vod.Enums;

namespace Fluxy.API.Controllers.Support
{
    /// <summary>
    /// Turns the text of a series field into the member it names, for every endpoint that reads
    /// one.
    /// </summary>
    /// <remarks>
    /// Matched member by member rather than with enum binding: binding takes a member name
    /// spelled in full and would refuse the conventional lower-case one, before the antiforgery
    /// check could even run. The transcode format is the shared on-demand enum, so its parser is
    /// the on-demand one rather than a second copy of the same five words.
    /// </remarks>
    internal static class SeriesTextParser
    {
        /// <summary>Turns text into the series state it names, or null when it names none.</summary>
        public static SeriesStatus? ParseSeriesStatusOrNull(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "active" => SeriesStatus.Active,
                "inactive" => SeriesStatus.Inactive,
                _ => null
            };

        /// <summary>Turns text into the episode state it names, or null when it names none.</summary>
        public static EpisodeStatus? ParseEpisodeStatusOrNull(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "active" => EpisodeStatus.Active,
                "inactive" => EpisodeStatus.Inactive,
                _ => null
            };

        /// <summary>Turns text into the delivery kind it names, or null when it names none.</summary>
        public static EpisodeType? ParseEpisodeTypeOrNull(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "relay" => EpisodeType.Relay,
                "redirect" => EpisodeType.Redirect,
                _ => null
            };

        /// <summary>Turns a column name into the series sort field it names, or null.</summary>
        public static SeriesSortField? ParseSeriesSortField(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "name" => SeriesSortField.Name,
                "category" => SeriesSortField.Category,
                "status" => SeriesSortField.Status,
                "releasedate" => SeriesSortField.ReleaseDate,
                "rating" => SeriesSortField.Rating,
                "seasons" => SeriesSortField.Seasons,
                "episodes" => SeriesSortField.Episodes,
                _ => null
            };

        /// <summary>Turns a column name into the episode sort field it names, or null.</summary>
        public static EpisodeSortField? ParseEpisodeSortField(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "season" => EpisodeSortField.Season,
                "episode" => EpisodeSortField.Episode,
                "name" => EpisodeSortField.Name,
                "status" => EpisodeSortField.Status,
                "type" => EpisodeSortField.Type,
                "releasedate" => EpisodeSortField.ReleaseDate,
                "rating" => EpisodeSortField.Rating,
                _ => null
            };

        /// <summary>Turns text into the bulk series operation it names, or null.</summary>
        public static BulkSeriesAction? ParseBulkSeriesActionOrNull(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "change-status" => BulkSeriesAction.ChangeStatus,
                "move-category" => BulkSeriesAction.MoveCategory,
                "delete" => BulkSeriesAction.Delete,
                _ => null
            };

        /// <summary>Turns text into the bulk episode operation it names, or null.</summary>
        public static BulkEpisodeAction? ParseBulkEpisodeActionOrNull(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "change-status" => BulkEpisodeAction.ChangeStatus,
                "change-type" => BulkEpisodeAction.ChangeType,
                "delete" => BulkEpisodeAction.Delete,
                _ => null
            };

        /// <summary>Turns text into the shared transcode format it names, or null.</summary>
        public static VodTranscodeFormat? ParseFormatOrNull(string? value)
            => VodTextParser.ParseFormatOrNull(value);
    }
}
