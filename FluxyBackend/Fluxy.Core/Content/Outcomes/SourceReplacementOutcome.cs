namespace Fluxy.Core.Content.Outcomes
{
    /// <summary>
    /// How one replacement run ended across all four playback domains.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A valid run always carries one count per domain, in the enum's order, whether or not the
    /// domain had a candidate - a client that draws four lines must never see a line appear or
    /// vanish because a table happened to be empty.
    /// </para>
    /// <para>
    /// <see cref="Errors"/> is non-null only when the two input strings could not be used at
    /// all; in that case nothing was scanned, nothing was staged and every count is zero.
    /// </para>
    /// </remarks>
    public sealed record SourceReplacementOutcome
    {
        /// <summary>One count per domain, always all four, in the enum's order.</summary>
        public required IReadOnlyList<SourceReplacementDomainCount> Domains { get; init; }

        /// <summary>Entities the run declined to touch, in domain order.</summary>
        public required IReadOnlyList<SourceReplacementRefusal> Refusals { get; init; }

        /// <summary>Rejected input fields, or null when the request was usable.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

        /// <summary>Entities whose values changed, summed over the domains.</summary>
        public int Updated => Domains.Sum(count => count.Updated);

        /// <summary>Candidate addresses left unchanged, summed over the domains.</summary>
        public int Skipped => Domains.Sum(count => count.Skipped);

        /// <summary>Whether the two input strings were usable.</summary>
        public bool Accepted => Errors is null || Errors.Count == 0;
    }
}
