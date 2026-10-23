namespace Fluxy.Core.Streams.Outcomes
{
    /// <summary>
    /// One line of an uploaded playlist that did not become a stream entry.
    /// </summary>
    /// <remarks>
    /// A dangling <c>#EXTINF</c> and an address that is not an absolute URL are both problems
    /// of the file rather than of a stream row, so they carry the sentence the parser produced
    /// instead of an error map keyed by a stream property.
    /// </remarks>
    public sealed record M3uPlaylistProblem
    {
        /// <summary>One-based line the problem was found on.</summary>
        public required int LineNumber { get; init; }

        /// <summary>Name the entry would have carried, when one was known.</summary>
        public string? Name { get; init; }

        /// <summary>English sentence naming what is wrong with the line.</summary>
        public required string Message { get; init; }
    }
}
