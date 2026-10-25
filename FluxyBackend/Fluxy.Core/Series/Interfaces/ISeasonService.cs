using Fluxy.Core.Series.Commands;
using Fluxy.Core.Series.Outcomes;

namespace Fluxy.Core.Series.Interfaces
{
    /// <summary>
    /// Creates a whole season by reading a folder of video files.
    /// </summary>
    /// <remarks>
    /// A season is a batch of episode rows rather than an entity of its own, so this is a
    /// separate service from <see cref="IEpisodeService"/>: the request is a folder rather than
    /// one row, and the answer counts a batch rather than describing one episode.
    /// </remarks>
    public interface ISeasonService
    {
        /// <summary>Creates every episode of one season from the files in a folder.</summary>
        Task<SeasonOutcome> CreateAsync(
            NewSeason input,
            CancellationToken cancellationToken = default);
    }
}
