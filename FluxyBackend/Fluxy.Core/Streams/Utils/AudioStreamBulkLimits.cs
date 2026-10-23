namespace Fluxy.Core.Streams.Utils
{
    /// <summary>
    /// The bounds of one bulk operation over audio streams.
    /// </summary>
    /// <remarks>
    /// The selection a table offers is the page the operator is looking at, so the most rows a
    /// bulk request can name is exactly one page. A larger set would mean the client assembled
    /// identifiers the operator never saw in one place.
    /// </remarks>
    public static class AudioStreamBulkLimits
    {
        /// <summary>Most identifiers one bulk request may name.</summary>
        public const int MaxItems = AudioStreamListLimits.MaxPageSize;
    }
}
