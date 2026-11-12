namespace Fluxy.Core.Content.Rows
{
    /// <summary>
    /// One candidate source of a series episode, shaped for the replacement scan.
    /// </summary>
    /// <remarks>
    /// An episode's stored name may be absent - a folder-scan season names nothing - so the
    /// projection resolves a readable label instead of carrying a null the refusal line would
    /// have to explain.
    /// </remarks>
    public sealed record StoredEpisodeSourceRow
    {
        /// <summary>Identifier of the episode.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name of the episode, resolved for the refusal line.</summary>
        public required string Name { get; init; }

        /// <summary>The source itself, as stored.</summary>
        public required string Source { get; init; }
    }
}
