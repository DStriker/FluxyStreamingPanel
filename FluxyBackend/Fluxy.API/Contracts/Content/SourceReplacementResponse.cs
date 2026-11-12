namespace Fluxy.API.Contracts.Content
{
    /// <summary>
    /// How one source replacement run ended across the four playback domains.
    /// </summary>
    /// <remarks>
    /// <see cref="Domains"/> always carries all four entries, in the stable order
    /// <c>videoStreams</c>, <c>audioStreams</c>, <c>videoOnDemand</c>, <c>seriesEpisodes</c> -
    /// a client that draws four lines must never see one appear or vanish because a table was
    /// empty. <see cref="Updated"/> and <see cref="Skipped"/> are the sums of the domain
    /// counts, so a screen can show a total without adding them itself.
    /// </remarks>
    public sealed record SourceReplacementResponse
    {
        /// <summary>Stable outcome code - <c>source_replacement_completed</c> on a valid run.</summary>
        public required string Code { get; init; }

        /// <summary>English fallback sentence.</summary>
        public required string Message { get; init; }

        /// <summary>Entities whose values changed, summed over the domains.</summary>
        public required int Updated { get; init; }

        /// <summary>Candidate addresses the replacement left unchanged, summed over the domains.</summary>
        public required int Skipped { get; init; }

        /// <summary>One count per playback domain, always all four, in the stable order.</summary>
        public required IReadOnlyList<SourceReplacementDomainCount> Domains { get; init; }

        /// <summary>Entities the run declined to touch, in domain order; empty when none.</summary>
        public required IReadOnlyList<SourceReplacementRefusalItem> Refusals { get; init; }
    }
}
