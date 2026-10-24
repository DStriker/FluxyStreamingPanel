using Fluxy.Core.Vod.Enums;

namespace Fluxy.Core.Vod.Commands
{
    /// <summary>
    /// Everything an administrator supplies to create on-demand items from an M3U playlist.
    /// </summary>
    /// <remarks>
    /// The playlist travels as its text rather than as parsed entries, because reading a file
    /// is an input concern and every rule that decides what an item may contain belongs behind
    /// the service. The name and the source are absent on purpose: both come from the playlist.
    /// The kind, the status and the category are shared by every item the file creates.
    /// </remarks>
    public sealed record NewVodImport
    {
        /// <summary>Delivery kind every imported item carries.</summary>
        public required VodType Type { get; init; }

        /// <summary>Whether every imported item opens published or disabled.</summary>
        public required VodStatus Status { get; init; }

        /// <summary>Category every imported item is filed under.</summary>
        public required Guid CategoryId { get; init; }

        /// <summary>Text of the uploaded playlist.</summary>
        public required string Playlist { get; init; }
    }
}
