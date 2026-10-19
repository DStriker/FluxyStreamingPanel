namespace Fluxy.Core.Models.Content
{
    /// <summary>
    /// Bounds of the values a content category row stores.
    /// </summary>
    /// <remarks>
    /// The limits live in this layer rather than beside the table configuration so a service
    /// can judge a request without referencing the data access assembly. The configuration
    /// uses the same constant, so the column width and the rule cannot drift apart.
    /// </remarks>
    public static class ContentCategoryLimits
    {
        /// <summary>Longest display name a category may carry.</summary>
        public const int NameMaxLength = 100;
    }
}
