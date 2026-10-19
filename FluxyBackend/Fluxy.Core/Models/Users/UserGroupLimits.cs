namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// Bounds of the values a group row stores.
    /// </summary>
    /// <remarks>
    /// The limits live in this layer rather than beside the table configuration so a service
    /// can judge a request without referencing the data access assembly. The configuration
    /// uses the same constants, so the column width and the rule cannot drift apart.
    /// </remarks>
    public static class UserGroupLimits
    {
        /// <summary>Longest display name a group may carry.</summary>
        public const int NameMaxLength = 100;
    }
}
