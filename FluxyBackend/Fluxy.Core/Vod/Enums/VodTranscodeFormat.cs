namespace Fluxy.Core.Vod.Enums
{
    /// <summary>
    /// A container an operator may ask a video to be converted to.
    /// </summary>
    /// <remarks>
    /// The members are the five formats the management screen offers. The numeric values are
    /// fixed because they are what a request names and what a future job row would store; a new
    /// format is added at the end. The order is the order the menu draws.
    /// </remarks>
    public enum VodTranscodeFormat
    {
        /// <summary>MPEG-4 Part 14.</summary>
        Mp4 = 1,

        /// <summary>Audio Video Interleave.</summary>
        Avi = 2,

        /// <summary>Matroska.</summary>
        Mkv = 3,

        /// <summary>MPEG program stream.</summary>
        Mpg = 4,

        /// <summary>Flash Video.</summary>
        Flv = 5
    }
}
