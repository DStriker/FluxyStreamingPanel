namespace Fluxy.Core.Transcoding.Utils
{
    /// <summary>
    /// Bounds of the values a transcoding profile row stores.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The limits live in this layer rather than beside the table configuration so a service
    /// can judge a request without referencing the data access assembly. The configuration uses
    /// the same constants, so a column width and the rule that refuses an oversized value cannot
    /// drift apart.
    /// </para>
    /// <para>
    /// The numeric bounds are ffmpeg's own where ffmpeg states one, and an installation
    /// decision where it does not. <c>-1</c> is a real width and height (keep the aspect ratio)
    /// and <c>0</c> is a real thread count (decide automatically), which is why their minima are
    /// below zero/one rather than at them.
    /// </para>
    /// </remarks>
    public static class TranscodingProfileLimits
    {
        /// <summary>Fewest characters a profile name may carry.</summary>
        public const int NameMinLength = 1;

        /// <summary>Most characters a profile name may carry.</summary>
        public const int NameMaxLength = 100;

        /// <summary>Most characters an aspect-ratio override may carry.</summary>
        public const int AspectRatioMaxLength = 32;

        /// <summary>Most characters the watermark address may carry.</summary>
        public const int LogoUrlMaxLength = 2048;

        /// <summary>Lowest constant rate factor ffmpeg accepts.</summary>
        public const int CrfMin = 0;

        /// <summary>Highest constant rate factor ffmpeg accepts.</summary>
        public const int CrfMax = 51;

        /// <summary>Lowest width or height a profile may state; <c>-1</c> keeps the aspect ratio.</summary>
        public const int DimensionMin = -1;

        /// <summary>Lowest audio channel count a profile may state.</summary>
        public const int AudioChannelsMin = 1;

        /// <summary>Highest audio channel count a profile may state.</summary>
        public const int AudioChannelsMax = 8;

        /// <summary>Lowest encoder thread count a profile may state; <c>0</c> means auto.</summary>
        public const int ThreadsMin = 0;

        /// <summary>Lowest bitrate value in kbps a profile may state.</summary>
        public const int BitrateMin = 0;

        /// <summary>
        /// The index making a profile name unique for the installation. Matched by exact name
        /// when the database refuses the write, because the service's own pre-check is only the
        /// friendly path - the index is the guarantee.
        /// </summary>
        public const string NameIndexName = "IX_transcoding_profiles_name";
    }
}
