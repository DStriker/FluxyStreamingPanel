namespace Fluxy.Core.Streams.Enums
{
    /// <summary>Which way the audio streams table is ordered.</summary>
    /// <remarks>
    /// Parsed from plain text rather than bound to the enum, because enum binding accepts a
    /// member name spelled in full and would refuse the conventional <c>sortOrder=desc</c>.
    /// </remarks>
    public enum AudioStreamSortOrder
    {
        /// <summary>Smallest first.</summary>
        Ascending,

        /// <summary>Largest first.</summary>
        Descending
    }
}
