namespace Fluxy.Core.Subscriptions.Utils
{
    /// <summary>
    /// Bounds of the values a subscription row stores, and of the endpoints that read and write
    /// it.
    /// </summary>
    /// <remarks>
    /// The limits live in this layer rather than beside the table configuration so a service can
    /// judge a request without referencing the data access assembly, and the configuration uses
    /// the same constants, so a column width and the rule that refuses an oversized value cannot
    /// drift apart.
    /// </remarks>
    public static class SubscriptionLimits
    {
        /// <summary>
        /// Length of a playlist id. A cryptographically random 32-character hex string, generated
        /// by the server and unique across subscriptions.
        /// </summary>
        public const int PlaylistIdLength = 32;

        /// <summary>Longest MAG or Enigma2 MAC, in canonical <c>XX:XX:XX:XX:XX:XX</c> form.</summary>
        public const int MacLength = 17;

        /// <summary>Longest single entry of the IP or user-agent whitelist.</summary>
        public const int WhitelistEntryMaxLength = 200;

        /// <summary>Most entries one whitelist may hold. Empty means no restriction.</summary>
        public const int WhitelistMaxEntries = 20;

        /// <summary>Fewest simultaneous connections a subscription may allow when it sets a ceiling.</summary>
        public const int ConnectionsMin = 1;

        /// <summary>Most simultaneous connections a subscription may allow.</summary>
        public const int ConnectionsMax = 10000;

        /// <summary>Items one page holds when the caller names no size.</summary>
        public const int DefaultPageSize = 20;

        /// <summary>Most items one page may hold.</summary>
        public const int MaxPageSize = 1000;

        /// <summary>Most characters one search may carry.</summary>
        public const int MaxSearchLength = 200;
    }
}
