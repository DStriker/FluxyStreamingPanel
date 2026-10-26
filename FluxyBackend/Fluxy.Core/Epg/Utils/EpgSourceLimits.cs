namespace Fluxy.Core.Epg.Utils
{
    /// <summary>
    /// Bounds of the values a guide source row stores.
    /// </summary>
    /// <remarks>
    /// The limits live in this layer rather than beside the table configuration so a service
    /// can judge a request without referencing the data access assembly. The configuration
    /// uses the same constants, so a column width and the rule that refuses an oversized value
    /// cannot drift apart.
    /// </remarks>
    public static class EpgSourceLimits
    {
        /// <summary>Longest display name a source may carry.</summary>
        public const int NameMaxLength = 200;

        /// <summary>Longest address a guide is fetched from.</summary>
        public const int UrlMaxLength = 2048;

        /// <summary>Fewest days of programmes a source may keep.</summary>
        public const int DaysKeepMin = 1;

        /// <summary>Most days of programmes a source may keep.</summary>
        public const int DaysKeepMax = 365;

        /// <summary>Days kept when a create names none.</summary>
        public const int DaysKeepDefault = 7;
    }
}
