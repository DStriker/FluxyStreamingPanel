namespace Fluxy.Core.Settings.Enums
{
    /// <summary>
    /// Container a MAG portal would deliver by default.
    /// </summary>
    /// <remarks>
    /// Nothing in this pass produces a MAG portal: the value is stored, validated and read by an
    /// operator only. The wire names are <c>mpeg-ts</c> and <c>hls</c>, the vocabulary the
    /// subscription output formats already use.
    /// </remarks>
    public enum MagContainer
    {
        /// <summary>MPEG transport stream.</summary>
        MpegTs = 1,

        /// <summary>HTTP live streaming.</summary>
        Hls = 2
    }
}
