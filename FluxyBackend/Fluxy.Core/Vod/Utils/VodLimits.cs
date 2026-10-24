namespace Fluxy.Core.Vod.Utils
{
    /// <summary>
    /// Bounds of the values an on-demand row stores.
    /// </summary>
    /// <remarks>
    /// The limits live in this layer rather than beside the table configuration so a service
    /// can judge a request without referencing the data access assembly. The configuration
    /// uses the same constants, so a column width and the rule that refuses an oversized value
    /// cannot drift apart.
    /// </remarks>
    public static class VodLimits
    {
        /// <summary>Longest display name an item may carry.</summary>
        public const int NameMaxLength = 200;

        /// <summary>Longest source address: a URL or a path to a file.</summary>
        public const int SourceMaxLength = 2048;

        /// <summary>Longest subtitles address.</summary>
        public const int SubtitleUrlMaxLength = 2048;

        /// <summary>Longest metadata address (poster or backdrop).</summary>
        public const int ImageUrlMaxLength = 2048;

        /// <summary>Longest IMDb identifier.</summary>
        public const int ImdbIdMaxLength = 32;

        /// <summary>Longest synopsis kept in the text column.</summary>
        public const int PlotMaxLength = 4000;

        /// <summary>Longest director name.</summary>
        public const int DirectorMaxLength = 200;

        /// <summary>Exact length of a country code: ISO 3166-1 alpha-2.</summary>
        public const int CountryCodeLength = 2;

        /// <summary>Most cast members one item records.</summary>
        public const int CastMaxItems = 20;

        /// <summary>Longest cast member name.</summary>
        public const int CastItemMaxLength = 100;

        /// <summary>Most genres one item records.</summary>
        public const int GenresMaxItems = 10;

        /// <summary>Longest genre name.</summary>
        public const int GenreItemMaxLength = 60;

        /// <summary>Lowest rating an item may carry.</summary>
        public const decimal RatingMin = 0m;

        /// <summary>Highest rating an item may carry.</summary>
        public const decimal RatingMax = 10m;
    }
}
