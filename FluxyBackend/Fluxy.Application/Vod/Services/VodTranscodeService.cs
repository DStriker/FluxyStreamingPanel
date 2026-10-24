using Fluxy.Core.Vod.Enums;
using Fluxy.Core.Vod.Interfaces;
using Fluxy.Core.Vod.Outcomes;
using Fluxy.Core.Vod.ReadModels;

namespace Fluxy.Application.Vod.Services
{
    /// <summary>
    /// The transcoding surface of this pass: the formats the menu offers, and an honest
    /// refusal because nothing yet encodes a file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The endpoint, the format list and the response table exist so the management screen can
    /// draw the operation and say why it is unavailable, rather than offering five buttons
    /// whose every press would be refused by a missing route. When an encoder arrives it
    /// replaces the body of <see cref="Request"/> and nothing above this class moves.
    /// </para>
    /// <para>
    /// <b>No job row is written.</b> A queued job that no worker would ever pick up is a lie
    /// the operator would discover only by waiting, and a failed job would be a record of an
    /// attempt that never started. The truthful answer is the one this class gives.
    /// </para>
    /// </remarks>
    public sealed class VodTranscodeService : IVodTranscodeService
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
        public VodTranscodeAvailability GetAvailability() => new()
        {
            Available = false,
            Formats = Formats
        };

        /// <inheritdoc />
        public VodTranscodeOutcome Request(Guid vodId, VodTranscodeFormat format)
        {
            // The identifier is part of the contract rather than this stub's answer: the real
            // implementation needs it, and accepting it now keeps the transport unchanged.
            _ = vodId;

            if (!Enum.IsDefined(format))
            {
                return new VodTranscodeOutcome
                {
                    Action = VodTranscodeAction.Invalid,
                    Format = format,
                    Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        // The C# name of the request property, so the transport can normalize
                        // it to the camelCase field the form draws.
                        ["Format"] = FormatRule
                    }
                };
            }

            return new VodTranscodeOutcome
            {
                Action = VodTranscodeAction.Unavailable,
                Format = format
            };
        }
    }
}
