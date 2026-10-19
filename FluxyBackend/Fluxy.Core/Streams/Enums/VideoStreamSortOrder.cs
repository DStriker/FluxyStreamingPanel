namespace Fluxy.Core.Streams.Enums
{
    /// <summary>Which way the streams table runs.</summary>
    public enum VideoStreamSortOrder
    {
        /// <summary>Smallest first: A before Z, stopped before running.</summary>
        Ascending,

        /// <summary>Largest first: Z before A, running before stopped.</summary>
        Descending
    }
}
