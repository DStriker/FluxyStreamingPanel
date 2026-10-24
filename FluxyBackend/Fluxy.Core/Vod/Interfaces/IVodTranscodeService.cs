using Fluxy.Core.Vod.Enums;
using Fluxy.Core.Vod.Outcomes;
using Fluxy.Core.Vod.ReadModels;

namespace Fluxy.Core.Vod.Interfaces
{
    /// <summary>
    /// Answers whether this installation can transcode, and refuses a request honestly when
    /// it cannot.
    /// </summary>
    /// <remarks>
    /// The encoding itself is not implemented in this pass. The contract exists so the
    /// transport, the menu and the response table have one place to ask, and so the real
    /// implementation can arrive without moving any of them.
    /// </remarks>
    public interface IVodTranscodeService
    {
        /// <summary>Reports whether a transcoder is configured and which formats it offers.</summary>
        VodTranscodeAvailability GetAvailability();

        /// <summary>Asks for one item to be converted to one format.</summary>
        /// <param name="vodId">Identifier of the item.</param>
        /// <param name="format">Container wanted.</param>
        /// <returns>Unavailable while no transcoder exists; invalid for a format outside the enum.</returns>
        VodTranscodeOutcome Request(Guid vodId, VodTranscodeFormat format);
    }
}
