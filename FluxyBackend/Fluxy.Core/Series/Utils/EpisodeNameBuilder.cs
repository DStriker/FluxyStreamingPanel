namespace Fluxy.Core.Series.Utils
{
    /// <summary>
    /// Builds the display name of an episode created from a file in a season folder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The base is the series name. The two checkboxes of the add-season form add the season
    /// and episode numbers to it: both become <c>S01E02</c>, one becomes <c>S01</c> or
    /// <c>E02</c>, and none leaves the series name alone. The file name is deliberately not
    /// part of the result - the operator asked for the series and the number to name the
    /// episode, and the file itself is still the row's source.
    /// </para>
    /// <para>
    /// The result is cut to <see cref="SeriesLimits.EpisodeNameMaxLength"/> characters by
    /// shortening the base, never the tag: a name without its season and episode numbers is
    /// the one part of the format a reader relies on.
    /// </para>
    /// </remarks>
    public static class EpisodeNameBuilder
    {
        /// <summary>Builds the name one episode gets.</summary>
        /// <param name="seriesName">Display name of the series.</param>
        /// <param name="seasonNumber">Season the episode belongs to.</param>
        /// <param name="episodeNumber">Number of the episode inside its season.</param>
        /// <param name="addSeasonNumber">Whether the season number belongs in the name.</param>
        /// <param name="addEpisodeNumber">Whether the episode number belongs in the name.</param>
        /// <returns>The generated name, at most 200 characters long.</returns>
        public static string Build(
            string seriesName,
            int seasonNumber,
            int episodeNumber,
            bool addSeasonNumber,
            bool addEpisodeNumber)
        {
            var tag = string.Empty;

            if (addSeasonNumber)
            {
                tag += $"S{seasonNumber:D2}";
            }

            if (addEpisodeNumber)
            {
                tag += $"E{episodeNumber:D2}";
            }

            var suffix = tag.Length == 0 ? string.Empty : " " + tag;
            var room = SeriesLimits.EpisodeNameMaxLength - suffix.Length;
            var baseName = seriesName.Trim();

            if (baseName.Length > room)
            {
                baseName = room <= 0 ? string.Empty : baseName[..room].TrimEnd();
            }

            return (baseName + suffix).Trim();
        }
    }
}
