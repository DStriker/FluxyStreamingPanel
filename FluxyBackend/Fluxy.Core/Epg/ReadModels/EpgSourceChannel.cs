namespace Fluxy.Core.Epg.ReadModels
{
    /// <summary>
    /// One channel a guide source is used by: a configured stream and the key it is filed
    /// under inside that source.
    /// </summary>
    /// <remarks>
    /// Read-only and derived - nothing here is a stored row of its own. The pair a stream
    /// carries (<c>EpgSourceId</c> plus <c>EpgChannelKey</c>) is what makes the stream a
    /// channel of this source, and this projection is the join that answers "which streams
    /// read their schedule from here".
    /// </remarks>
    public sealed record EpgSourceChannel
    {
        /// <summary>Identifier of the stream that uses the source.</summary>
        public required Guid StreamId { get; init; }

        /// <summary>Display name of that stream, which is what the table draws.</summary>
        public required string StreamName { get; init; }

        /// <summary>Channel key the stream is filed under inside this guide.</summary>
        public required string EpgChannelKey { get; init; }
    }
}
