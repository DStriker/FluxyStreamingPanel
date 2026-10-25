using Fluxy.Core.Series.Interfaces;
using Fluxy.Core.Series.Outcomes;
using Fluxy.Core.Series.ReadModels;
using Fluxy.Core.Vod.Enums;

namespace Fluxy.Application.Series.Services
{
    /// <summary>
    /// The transcoding surface of this pass: the formats the menu offers, and an honest
    /// refusal because nothing yet encodes a file.
    /// </summary>
    /// <remarks>
    /// The format and action enums are shared with the on-demand catalogue. The endpoint, the
    /// format list and the response table exist so the episode table can draw the operation and
    /// say why it is unavailable, rather than offering buttons whose every press would be
    /// refused by a missing route. No job row is written: a queued job no worker would pick up
    /// is a lie the operator would discover only by waiting.
    /// </remarks>
    public sealed class EpisodeTranscodeService : IEpisodeTranscodeService
    {
        /// <summary>The five formats the menu offers, in the order it draws them.</summary>
        private static readonly IReadOnlyList<VodTranscodeFormat> Formats =
        [
            VodTranscodeFormat.Mp4,
            VodTranscodeFormat.Avi,
            VodTranscodeFormat.Mkv,
            VodTranscodeFormat.Mpg,
            VodTranscodeFormat.Flv
        ];

        /// <summary>The sentence a format outside the enum gets.</summary>
        private static readonly string[] FormatRule =
            ["The format must be one of: mp4, avi, mkv, mpg, flv."];

        /// <inheritdoc />
        public EpisodeTranscodeAvailability GetAvailability() => new()
        {
            Available = false,
            Formats = Formats
        };

        /// <inheritdoc />
        public EpisodeTranscodeOutcome Request(
            Guid seriesId,
            Guid episodeId,
            VodTranscodeFormat format)
        {
            // The identifiers are part of the contract rather than this stub's answer: the real
            // implementation needs them, and accepting them now keeps the transport unchanged.
            _ = seriesId;
            _ = episodeId;

            if (!Enum.IsDefined(format))
            {
                return new EpisodeTranscodeOutcome
                {
                    Action = VodTranscodeAction.Invalid,
                    Format = format,
                    Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["Format"] = FormatRule
                    }
                };
            }

            return new EpisodeTranscodeOutcome
            {
                Action = VodTranscodeAction.Unavailable,
                Format = format
            };
        }
    }
}
