namespace Fluxy.Core.Streams.Utils
{
    /// <summary>
    /// Bounds of the values a configured stream row stores.
    /// </summary>
    /// <remarks>
    /// The limits live in this layer rather than beside the table configuration so a service
    /// can judge a request without referencing the data access assembly. The configuration
    /// uses the same constants, so a column width and the rule that refuses an oversized value
    /// cannot drift apart.
    /// </remarks>
    public static class VideoStreamLimits
    {
        /// <summary>Longest display name a stream may carry.</summary>
        public const int NameMaxLength = 200;

        /// <summary>Longest icon address.</summary>
        public const int LogoUrlMaxLength = 500;

        /// <summary>Longest source the relaying process may report back as the one in use.</summary>
        public const int ActiveSourceMaxLength = 1000;

        /// <summary>Longest Enigma2 service reference.</summary>
        public const int Enigma2SidMaxLength = 200;

        /// <summary>Longest channel key inside a programme guide.</summary>
        public const int EpgChannelKeyMaxLength = 200;

        /// <summary>Longest user agent a relayed request may carry.</summary>
        public const int RestreamUserAgentMaxLength = 500;

        /// <summary>Longest proxy address a relay may go out through.</summary>
        public const int RestreamProxyMaxLength = 500;

        /// <summary>Longest URL a stream relays from.</summary>
        public const int SourceUrlMaxLength = 2000;

        /// <summary>Longest path of a file a stream plays.</summary>
        public const int FilePathMaxLength = 1000;

        /// <summary>
        /// Most on-demand items one playlist stream may hold.
        /// </summary>
        /// <remarks>
        /// A bound rather than a taste: the list is written as one row per entry in a single
        /// save, and a request naming an unbounded number of them would let one form build a
        /// statement the database refuses at the end of a long round trip. A playlist longer
        /// than this is a catalogue question rather than a stream's.
        /// </remarks>
        public const int VodItemsMaxCount = 1000;
    }
}
