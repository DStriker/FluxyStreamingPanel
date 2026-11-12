using Fluxy.Core.Content.Enums;

namespace Fluxy.Core.Content.Outcomes
{
    /// <summary>
    /// How one of the four playback domains ended a source replacement.
    /// </summary>
    /// <remarks>
    /// <see cref="Updated"/> counts entities whose stored values changed.
    /// <see cref="Skipped"/> counts candidate addresses whose ordinal replacement produced the
    /// same text - a candidate the SQL filter admitted but the rule left alone. An entity the
    /// rule refused appears in neither number; it is reported separately.
    /// </remarks>
    public sealed record SourceReplacementDomainCount
    {
        /// <summary>Which playback domain this count describes.</summary>
        public required SourceDomain Domain { get; init; }

        /// <summary>Entities whose values were changed.</summary>
        public required int Updated { get; init; }

        /// <summary>Candidate addresses the replacement left unchanged.</summary>
        public required int Skipped { get; init; }
    }
}
