using Fluxy.Core.Series.Outcomes;
using Fluxy.Core.Series.ReadModels;
using Fluxy.Core.Vod.Enums;

namespace Fluxy.Core.Series.Interfaces
{
    /// <summary>
    /// Answers whether this installation can transcode episodes, and refuses a request honestly
    /// when it cannot.
    /// </summary>
    /// <remarks>
    /// The encoding itself is not implemented in this pass. The contract exists so the table,
    /// the menu and the response table have one place to ask, and so the real implementation
    /// can arrive without moving any of them.
    /// </remarks>
    public interface IEpisodeTranscodeService
    {
        /// <summary>Reports whether a transcoder is configured and which formats it offers.</summary>
        EpisodeTranscodeAvailability GetAvailability();

        /// <summary>Asks for one episode to be converted to one format.</summary>
        EpisodeTranscodeOutcome Request(
            Guid seriesId,
            Guid episodeId,
            VodTranscodeFormat format);
    }
}
