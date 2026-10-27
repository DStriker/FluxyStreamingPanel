namespace Fluxy.Core.Content.Utils
{
    /// <summary>
    /// Bounds of the values a bouquet row stores.
    /// </summary>
    /// <remarks>
    /// The limits live in this layer rather than beside the table configuration so a service can
    /// judge a request without referencing the data access assembly. The configuration uses the
    /// same constant, so the column width and the rule cannot drift apart.
    /// </remarks>
    public static class BouquetLimits
    {
        /// <summary>Longest display name a bouquet may carry.</summary>
        public const int NameMaxLength = 100;
    }
}
