namespace Fluxy.Core.Content.Rows
{
    /// <summary>
    /// One candidate source of an on-demand video, shaped for the replacement scan.
    /// </summary>
    /// <remarks>
    /// An on-demand item stores its playback address on its own row rather than in a child
    /// list, so the entity identifier is the whole key here.
    /// </remarks>
    public sealed record StoredVodSourceRow
    {
        /// <summary>Identifier of the on-demand item.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name of that item, read for the refusal line.</summary>
        public required string Name { get; init; }

        /// <summary>The source itself, as stored.</summary>
        public required string Source { get; init; }
    }
}
