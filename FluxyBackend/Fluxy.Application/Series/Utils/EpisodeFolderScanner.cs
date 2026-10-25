using Fluxy.Core.Series.Utils;

namespace Fluxy.Application.Series.Utils
{
    /// <summary>
    /// Reads the video files of one season folder.
    /// </summary>
    /// <remarks>
    /// Only the top level is read: a season is one folder of files, and a recursive walk would
    /// fold a nested "extras" directory into the episode numbering. The extension allow list is
    /// the second filter - a folder of cover art and subtitles must not become a season of rows
    /// whose source no player could open. The result is ordered naturally, so
    /// <c>Episode 2</c> is numbered before <c>Episode 10</c>.
    /// </remarks>
    public static class EpisodeFolderScanner
    {
        /// <summary>Reads the episode files of a resolved folder, in natural order.</summary>
        /// <param name="resolvedFolder">Absolute path of the folder to read.</param>
        /// <returns>The files, ordered the way a person reads their names.</returns>
        public static IReadOnlyList<string> Scan(string resolvedFolder)
            => Directory
                .EnumerateFiles(resolvedFolder, "*", SearchOption.TopDirectoryOnly)
                .Where(file => IsEpisodeFile(file))
                .OrderBy(
                    file => Path.GetFileName(file) ?? string.Empty,
                    NaturalStringComparer.Instance)
                .ToList();

        /// <summary>Whether a file carries an extension the season form accepts.</summary>
        /// <param name="path">Path to judge.</param>
        public static bool IsEpisodeFile(string path)
        {
            var extension = Path.GetExtension(path);

            return SeriesLimits.EpisodeFileExtensions
                .Contains(extension, StringComparer.OrdinalIgnoreCase);
        }
    }
}
