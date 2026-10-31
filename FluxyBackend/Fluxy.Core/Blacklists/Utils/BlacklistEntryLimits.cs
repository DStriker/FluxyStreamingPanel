namespace Fluxy.Core.Blacklists.Utils
{
    /// <summary>
    /// Bounds of the values a blacklist entry row stores.
    /// </summary>
    /// <remarks>
    /// The limits live in this layer rather than beside the table configuration so a service
    /// can judge a request without referencing the data access assembly. The configuration
    /// uses the same constants, so a column width and the rule that refuses an oversized value
    /// cannot drift apart.
    /// </remarks>
    public static class BlacklistEntryLimits
    {
        /// <summary>
        /// Longest value an entry may carry. A user agent is the longest of the three kinds by
        /// a wide margin, and 512 characters holds every one this project has seen.
        /// </summary>
        public const int ValueMaxLength = 512;

        /// <summary>Longest free-text note an entry may carry.</summary>
        public const int NoteMaxLength = 1000;
    }
}
