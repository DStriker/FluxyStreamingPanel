namespace Fluxy.Core.Streams.Utils
{
    /// <summary>
    /// Column widths of a stream problem row and the bounds of the endpoint that reads them.
    /// </summary>
    /// <remarks>
    /// The limits live in this layer rather than beside the table configuration so the transport
    /// and the write boundary can judge a value without referencing the data access assembly, and
    /// the configuration uses the same constants, so a column width and the rule that cuts a value
    /// to it cannot drift apart.
    /// </remarks>
    public static class StreamProblemListLimits
    {
        /// <summary>Longest failure message a row may store.</summary>
        public const int ErrorMessageMaxLength = 1000;

        /// <summary>Items one page holds when the caller names no size.</summary>
        public const int DefaultPageSize = 20;

        /// <summary>Most items one page may hold.</summary>
        public const int MaxPageSize = 1000;

        /// <summary>Most characters one search may carry.</summary>
        public const int MaxSearchLength = 200;
    }
}
