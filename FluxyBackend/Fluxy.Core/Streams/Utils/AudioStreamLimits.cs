namespace Fluxy.Core.Streams.Utils
{
    /// <summary>
    /// The limits every audio stream write is held to.
    /// </summary>
    /// <remarks>
    /// The numbers are shared by the service that enforces them, the entity configuration that
    /// sizes the columns and the form that draws the counters. Writing one of them twice is how
    /// a form starts accepting a name the database refuses, so there is one table here.
    /// </remarks>
    public static class AudioStreamLimits
    {
        /// <summary>Longest display name. A stream is one line of a table beside a column of
        /// category names.</summary>
        public const int NameMaxLength = 200;

        /// <summary>Longest icon address. A URL, and one a browser has to be able to fetch.</summary>
        public const int LogoUrlMaxLength = 500;

        /// <summary>Longest source the relaying process may report back as the one it is using.</summary>
        public const int ActiveSourceMaxLength = 1000;

        /// <summary>Longest Enigma2 service reference.</summary>
        public const int Enigma2SidMaxLength = 200;

        /// <summary>Longest restream user agent.</summary>
        public const int RestreamUserAgentMaxLength = 500;

        /// <summary>Longest restream proxy address.</summary>
        public const int RestreamProxyMaxLength = 500;

        /// <summary>Longest address one source row may carry.</summary>
        public const int SourceUrlMaxLength = 2000;
    }
}
