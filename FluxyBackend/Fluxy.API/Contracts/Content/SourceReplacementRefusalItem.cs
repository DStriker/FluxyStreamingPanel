namespace Fluxy.API.Contracts.Content
{
    /// <summary>
    /// One entity a source replacement declined to touch.
    /// </summary>
    /// <remarks>
    /// A refusal is not a failed request: the run is a partial success by design, and every
    /// entity that could be written was written. The client shows these in a dialog so the
    /// operator knows exactly which rows still hold the old address.
    /// </remarks>
    public sealed record SourceReplacementRefusalItem
    {
        /// <summary>Wire name of the domain the refused entity belongs to.</summary>
        public required string Domain { get; init; }

        /// <summary>Identifier of the entity, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Display name of the entity.</summary>
        public required string Name { get; init; }

        /// <summary>Stable refusal code - <c>source_replacement_blank</c> or <c>..._too_long</c>.</summary>
        public required string Code { get; init; }
    }
}
