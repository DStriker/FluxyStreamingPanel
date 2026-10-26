namespace Fluxy.Core.Epg.Utils
{
    /// <summary>
    /// Bounds of the values a programme row stores. The description has no bound and is a
    /// <c>text</c> column: a guide decides how long a synopsis is, and truncating one to fit a
    /// number invented here would be this application editing a source of truth it only
    /// copies.
    /// </summary>
    public static class EpgDataLimits
    {
        /// <summary>Longest channel key a guide file uses.</summary>
        public const int ChannelIdMaxLength = 200;

        /// <summary>Longest programme title.</summary>
        public const int TitleMaxLength = 500;

        /// <summary>Longest language tag (BCP 47 allows a handful of subtags).</summary>
        public const int LanguageMaxLength = 35;
    }
}
