namespace Fluxy.Core.Users.Utils
{
    /// <summary>
    /// Column widths of a user log row and the bounds of the endpoint that reads them.
    /// </summary>
    /// <remarks>
    /// The limits live in this layer rather than beside the table configuration so the transport
    /// can judge a request without referencing the data access assembly, and the configuration
    /// uses the same constants, so a column width and the rule that refuses an oversized value
    /// cannot drift apart. The widths are the connection log's, because the two tables capture the
    /// same four facts about a request.
    /// </remarks>
    public static class UserLogListLimits
    {
        /// <summary>Longest address a row may store: the longest textual IPv6 form, 45 characters.</summary>
        public const int IpMaxLength = 45;

        /// <summary>Longest user agent a row may store.</summary>
        public const int UserAgentMaxLength = 512;

        /// <summary>Length of an ISO 3166-1 alpha-2 country code.</summary>
        public const int CountryCodeLength = 2;

        /// <summary>Longest provider name a row may store.</summary>
        public const int IspMaxLength = 255;

        /// <summary>Items one page holds when the caller names no size.</summary>
        public const int DefaultPageSize = 20;

        /// <summary>Most items one page may hold.</summary>
        public const int MaxPageSize = 1000;

        /// <summary>Most characters one search may carry.</summary>
        public const int MaxSearchLength = 200;

        /// <summary>
        /// Most values one repeated filter (<c>userIds</c>, <c>groupIds</c>, <c>recordTypes</c>)
        /// may carry. Above it the request is refused rather than trimmed: a filter silently
        /// narrowed to its first hundred values would answer a different set from the one named.
        /// </summary>
        public const int MaxFilterValues = 100;
    }
}
