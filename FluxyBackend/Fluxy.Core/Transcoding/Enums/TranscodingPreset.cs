namespace Fluxy.Core.Transcoding.Enums
{
    /// <summary>
    /// The speed/efficiency trade-off a transcoding profile asks ffmpeg for - the value of
    /// <c>-preset</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are the presets this installation offers, fastest first. <see cref="Default"/> is
    /// the sentinel a new profile is written with when the request names no preset: it means
    /// "leave the choice to the encoder", which is what ffmpeg does when the flag is absent.
    /// </para>
    /// <para>
    /// <b>The count is ten because the specification asks for ten and for <c>default</c> as the
    /// create default at the same time.</b> ffmpeg names nine usable presets between
    /// <c>ultrafast</c> and <c>veryslow</c> plus <c>placebo</c>; <c>placebo</c> is the one left
    /// out, because it buys an imperceptible size gain at many times the cost and because the
    /// default sentinel has to have a slot of its own.
    /// </para>
    /// <para>
    /// The members are a scale - a slower preset compresses better at the same quality - so
    /// unlike the codec lists the order does mean something. Nothing in this pass compares two
    /// of them: the preset is passed through to ffmpeg and the encoder decides what it means.
    /// </para>
    /// </remarks>
    public enum TranscodingPreset
    {
        /// <summary>No preset name is sent; the encoder's own default applies (<c>default</c>).</summary>
        Default,

        /// <summary>Fastest, largest output (<c>ultrafast</c>).</summary>
        Ultrafast,

        /// <summary>Faster than veryfast, larger output (<c>superfast</c>).</summary>
        Superfast,

        /// <summary>Fast, larger output (<c>veryfast</c>).</summary>
        Veryfast,

        /// <summary>Faster than fast (<c>faster</c>).</summary>
        Faster,

        /// <summary>Faster than the library default (<c>fast</c>).</summary>
        Fast,

        /// <summary>The x264/x265 default preset (<c>medium</c>).</summary>
        Medium,

        /// <summary>Slower than the default (<c>slow</c>).</summary>
        Slow,

        /// <summary>Slower still (<c>slower</c>).</summary>
        Slower,

        /// <summary>Slowest useful preset offered here (<c>veryslow</c>).</summary>
        Veryslow
    }
}
