using Fluxy.Core.Streams.Models;
using Fluxy.Core.Streams.ReadModels;

namespace Fluxy.Core.Streams.Utils
{
    /// <summary>
    /// The one rule that answers "does this stream carry a programme guide", asked in two
    /// shapes: as a getter on <see cref="VideoStream"/> and as a column of
    /// <see cref="VideoStreamListItem"/>.
    /// </summary>
    /// <remarks>
    /// A guide is two facts, not one - which source the schedule is read from and which key
    /// the channel is filed under inside it - and a stream that names only one of them names
    /// neither, because a source without a key cannot say which channel it is about and a key
    /// without a source is a name nothing will look up. The table therefore shows a single
    /// "has EPG" column, and this is the only place that decides what it means.
    /// </remarks>
    public static class VideoStreamEpg
    {
        /// <summary>
        /// Whether both halves of a programme guide reference are present.
        /// </summary>
        /// <param name="sourceId">Guide the schedule is read from, or null.</param>
        /// <param name="channelKey">Key the channel is filed under, or null.</param>
        /// <returns>True when a source is named and the key is not blank.</returns>
        public static bool Configured(Guid? sourceId, string? channelKey)
            => sourceId is not null && !string.IsNullOrWhiteSpace(channelKey);
    }
}
