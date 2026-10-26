namespace Fluxy.API.Contracts.Epg
{
    /// <summary>
    /// One channel that reads its guide from a source: the stream and the key it is filed
    /// under inside that guide.
    /// </summary>
    public sealed record AdminEpgSourceChannelResponse
    {
        /// <summary>Identifier of the stream that uses the source, as text.</summary>
        public required string StreamId { get; init; }

        /// <summary>Display name of that stream.</summary>
        public required string StreamName { get; init; }

        /// <summary>Channel key the stream is filed under inside this guide.</summary>
        public required string EpgChannelKey { get; init; }
    }
}
