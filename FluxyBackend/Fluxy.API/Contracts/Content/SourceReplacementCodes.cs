namespace Fluxy.API.Contracts.Content
{
    /// <summary>
    /// The stable codes the source replacement endpoint answers with.
    /// </summary>
    /// <remarks>
    /// Two successes are possible - a run that changed something and a valid run that matched
    /// nothing - and they share <see cref="CompletedCode"/> because both are the same
    /// statement: the addresses now read as the replacement asked. The two per-entity refusals
    /// have codes of their own so a client can translate them.
    /// </remarks>
    public static class SourceReplacementCodes
    {
        /// <summary>Code reported when a valid run has been applied.</summary>
        public const string CompletedCode = "source_replacement_completed";

        /// <summary>Code reported for one entity the replacement would have left blank.</summary>
        public const string BlankCode = "source_replacement_blank";

        /// <summary>Code reported for one entity the replacement would have overfilled.</summary>
        public const string TooLongCode = "source_replacement_too_long";
    }
}
