namespace Fluxy.Core.Transcoding.Enums
{
    /// <summary>
    /// The H.264 profile a transcoding profile asks ffmpeg for - the value of <c>-profile:v</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="None"/> is the default a create applies when the request names no profile:
    /// no flag is sent, and the encoder picks the profile its other settings imply. The other
    /// seven are the H.264 profiles ffmpeg names, in ascending level order.
    /// </para>
    /// <para>
    /// The members are a scale where the level matters - a receiver that only understands
    /// Baseline 3.0 will refuse a High 4.2 stream - but nothing here compares two of them: the
    /// value travels to ffmpeg and the device decides.
    /// </para>
    /// </remarks>
    public enum VideoProfile
    {
        /// <summary>No profile flag is sent (<c>none</c>).</summary>
        None,

        /// <summary>H.264 Baseline profile, level 3.0 (<c>baseline30</c>).</summary>
        Baseline30,

        /// <summary>H.264 Baseline profile, level 3.1 (<c>baseline31</c>).</summary>
        Baseline31,

        /// <summary>H.264 Main profile, level 3.1 (<c>main31</c>).</summary>
        Main31,

        /// <summary>H.264 Main profile, level 4.0 (<c>main40</c>).</summary>
        Main40,

        /// <summary>H.264 High profile, level 4.0 (<c>high40</c>).</summary>
        High40,

        /// <summary>H.264 High profile, level 4.1 (<c>high41</c>).</summary>
        High41,

        /// <summary>H.264 High profile, level 4.2 (<c>high42</c>).</summary>
        High42
    }
}
