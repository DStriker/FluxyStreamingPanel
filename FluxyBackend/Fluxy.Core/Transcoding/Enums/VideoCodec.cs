namespace Fluxy.Core.Transcoding.Enums
{
    /// <summary>
    /// The video encoder a transcoding profile asks ffmpeg for - the value of <c>-c:v</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The members are the encoders this installation has been asked to offer, not every
    /// encoder ffmpeg may be built with: a list of everything would be a list nobody can read
    /// and half of it would be answered with "unknown encoder" on the build actually installed.
    /// <see cref="Copy"/> is a member of its own because "do not touch the video stream" is a
    /// real answer and the default a create applies when the body names none.
    /// </para>
    /// <para>
    /// The numeric values are never stored. The API turns a member into the lowercase name
    /// ffmpeg spells on the command line (see the text parser in the API layer), and the
    /// documents a client reads use the same spelling, so the number is only ever an artefact
    /// of C#.
    /// </para>
    /// <para>
    /// The members are not ordered - one is not "better" than another - so nothing compares
    /// two of them.
    /// </para>
    /// </remarks>
    public enum VideoCodec
    {
        /// <summary>Copy the video stream without re-encoding it (<c>copy</c>).</summary>
        Copy,

        /// <summary>H.264 through the x264 library (<c>libx264</c>).</summary>
        Libx264,

        /// <summary>H.265 through the x265 library (<c>libx265</c>).</summary>
        Libx265,

        /// <summary>VP8 through libvpx (<c>libvpx</c>).</summary>
        Libvpx,

        /// <summary>VP9 through libvpx (<c>libvpx-vp9</c>).</summary>
        LibvpxVp9,

        /// <summary>AV1 through libaom (<c>libaom-av1</c>).</summary>
        LibaomAv1,

        /// <summary>AV1 through SVT-AV1 (<c>libsvtav1</c>).</summary>
        LibsvtAv1,

        /// <summary>AV1 through rav1e (<c>librav1e</c>).</summary>
        Librav1e,

        /// <summary>MPEG-4 Part 2 (<c>mpeg4</c>).</summary>
        Mpeg4,

        /// <summary>Microsoft MPEG-4 version 2 (<c>msmpeg4v2</c>).</summary>
        Msmpeg4V2,

        /// <summary>Microsoft MPEG-4 version 3 (<c>msmpeg4</c>).</summary>
        Msmpeg4,

        /// <summary>Theora (<c>libtheora</c>).</summary>
        Libtheora,

        /// <summary>MPEG-4 Part 2 through Xvid (<c>libxvid</c>).</summary>
        Libxvid,

        /// <summary>H.264 through NVENC (<c>h264_nvenc</c>).</summary>
        H264Nvenc,

        /// <summary>H.265 through NVENC (<c>hevc_nvenc</c>).</summary>
        HevcNvenc,

        /// <summary>AV1 through NVENC (<c>av1_nvenc</c>).</summary>
        Av1Nvenc,

        /// <summary>H.264 through Intel Quick Sync (<c>h264_qsv</c>).</summary>
        H264Qsv,

        /// <summary>H.265 through Intel Quick Sync (<c>hevc_qsv</c>).</summary>
        HevcQsv,

        /// <summary>AV1 through Intel Quick Sync (<c>av1_qsv</c>).</summary>
        Av1Qsv,

        /// <summary>H.264 through VA-API (<c>h264_vaapi</c>).</summary>
        H264Vaapi,

        /// <summary>H.265 through VA-API (<c>hevc_vaapi</c>).</summary>
        HevcVaapi,

        /// <summary>AV1 through VA-API (<c>av1_vaapi</c>).</summary>
        Av1Vaapi,

        /// <summary>H.264 through AMF (<c>h264_amf</c>).</summary>
        H264Amf,

        /// <summary>H.265 through AMF (<c>hevc_amf</c>).</summary>
        HevcAmf,

        /// <summary>AV1 through AMF (<c>av1_amf</c>).</summary>
        Av1Amf,

        /// <summary>H.264 through VideoToolbox (<c>h264_videotoolbox</c>).</summary>
        H264Videotoolbox,

        /// <summary>H.265 through VideoToolbox (<c>hevc_videotoolbox</c>).</summary>
        HevcVideotoolbox,

        /// <summary>Apple ProRes (<c>prores</c>).</summary>
        Prores,

        /// <summary>Motion JPEG (<c>mjpeg</c>).</summary>
        Mjpeg,

        /// <summary>Avid DNxHD (<c>dnxhd</c>).</summary>
        Dnxhd,

        /// <summary>FFV1 lossless video (<c>ffv1</c>).</summary>
        Ffv1,

        /// <summary>HuffYUV lossless video (<c>huffyuv</c>).</summary>
        Huffyuv,

        /// <summary>Windows Media Video 8 (<c>wmv2</c>).</summary>
        Wmv2,

        /// <summary>Uncompressed video (<c>rawvideo</c>).</summary>
        Rawvideo
    }
}
