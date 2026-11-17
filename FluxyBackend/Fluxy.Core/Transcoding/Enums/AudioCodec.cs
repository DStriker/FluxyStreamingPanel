namespace Fluxy.Core.Transcoding.Enums
{
    /// <summary>
    /// The audio encoder a transcoding profile asks ffmpeg for - the value of <c>-c:a</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The members are the encoders an operator may pick, not every encoder an ffmpeg build
    /// can carry. <see cref="Copy"/> is a member of its own because "do not touch the audio
    /// stream" is a real answer and the default a create applies when the body names none.
    /// </para>
    /// <para>
    /// The numeric values are never stored: the API turns a member into the lowercase name
    /// ffmpeg spells on the command line, including the underscored ones such as
    /// <c>pcm_alaw</c>, which no C# member could be named after.
    /// </para>
    /// <para>
    /// The members are not ordered - one is not "better" than another - so nothing compares
    /// two of them.
    /// </para>
    /// </remarks>
    public enum AudioCodec
    {
        /// <summary>Copy the audio stream without re-encoding it (<c>copy</c>).</summary>
        Copy,

        /// <summary>AAC through the built-in encoder (<c>aac</c>).</summary>
        Aac,

        /// <summary>MP3 through LAME (<c>libmp3lame</c>).</summary>
        Libmp3lame,

        /// <summary>Opus (<c>libopus</c>).</summary>
        Libopus,

        /// <summary>Vorbis (<c>libvorbis</c>).</summary>
        Libvorbis,

        /// <summary>AC-3 (<c>ac3</c>).</summary>
        Ac3,

        /// <summary>E-AC-3 (<c>eac3</c>).</summary>
        Eac3,

        /// <summary>MPEG-2 audio layer II (<c>mp2</c>).</summary>
        Mp2,

        /// <summary>Apple Lossless (<c>alac</c>).</summary>
        Alac,

        /// <summary>Free Lossless Audio Codec (<c>flac</c>).</summary>
        Flac,

        /// <summary>Uncompressed 16-bit PCM (<c>pcm_s16le</c>).</summary>
        PcmS16Le,

        /// <summary>Uncompressed 24-bit PCM (<c>pcm_s24le</c>).</summary>
        PcmS24Le,

        /// <summary>A-law PCM (<c>pcm_alaw</c>).</summary>
        PcmAlaw,

        /// <summary>Mu-law PCM (<c>pcm_mulaw</c>).</summary>
        PcmMulaw,

        /// <summary>Windows Media Audio 2 (<c>wmav2</c>).</summary>
        Wmav2,

        /// <summary>AMR narrowband (<c>libopencore_amrnb</c>).</summary>
        LibopencoreAmrnb,

        /// <summary>AMR wideband (<c>libvo_amrwbenc</c>).</summary>
        LibvoAmrwbenc,

        /// <summary>DTS (<c>dca</c>).</summary>
        Dca,

        /// <summary>TrueHD (<c>truehd</c>).</summary>
        Truehd,

        /// <summary>MPEG-H 3D audio (<c>libmpegh</c>).</summary>
        Libmpegh,

        /// <summary>Speex (<c>libspeex</c>).</summary>
        Libspeex,

        /// <summary>Adaptive Multi-Rate wideband through FDK AAC (<c>libfdk_aac</c>).</summary>
        LibfdkAac,

        /// <summary>MP3 through the built-in encoder (<c>mp3</c>).</summary>
        Mp3,

        /// <summary>AC-4 (<c>ac4</c>).</summary>
        Ac4,

        /// <summary>Opus through the native encoder (<c>opus</c>).</summary>
        Opus,

        /// <summary>MPEG-1 audio layer I (<c>mp1</c>).</summary>
        Mp1,

        /// <summary>AAC through an external encoder (<c>aac_at</c>).</summary>
        AacAt
    }
}
