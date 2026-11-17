using Fluxy.Core.Transcoding.Enums;

namespace Fluxy.API.Controllers.Support
{
    /// <summary>
    /// Turns the text of a transcoding profile field into the member it names, and back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The wire names are ffmpeg's lowercase names, not the C# member names.</b>
    /// <c>Enum.TryParse</c> would accept <c>Libx264</c> and refuse <c>libx264</c>, and it could
    /// not spell <c>msmpeg4v2</c> or <c>pcm_alaw</c> at all unless the member were named after
    /// it - so the mapping is a table, stated once per enum, and the response uses the same
    /// table. One spelling in both directions is what lets the frontend's option list and this
    /// parser be pinned by a test.
    /// </para>
    /// <para>
    /// Null and an empty string are both "nothing was named" for the optional members, so the
    /// caller decides what that means: a create treats it as "the default", a patch as "no
    /// flag". A value that is present and names no member is refused by the caller with
    /// <c>validation_failed</c> under the field it came in.
    /// </para>
    /// </remarks>
    internal static class TranscodingProfileTextParser
    {
        private static readonly IReadOnlyDictionary<string, VideoCodec> VideoCodecs =
            new Dictionary<string, VideoCodec>(StringComparer.OrdinalIgnoreCase)
            {
                ["copy"] = VideoCodec.Copy,
                ["libx264"] = VideoCodec.Libx264,
                ["libx265"] = VideoCodec.Libx265,
                ["libvpx"] = VideoCodec.Libvpx,
                ["libvpx-vp9"] = VideoCodec.LibvpxVp9,
                ["libaom-av1"] = VideoCodec.LibaomAv1,
                ["libsvtav1"] = VideoCodec.LibsvtAv1,
                ["librav1e"] = VideoCodec.Librav1e,
                ["mpeg4"] = VideoCodec.Mpeg4,
                ["msmpeg4v2"] = VideoCodec.Msmpeg4V2,
                ["msmpeg4"] = VideoCodec.Msmpeg4,
                ["libtheora"] = VideoCodec.Libtheora,
                ["libxvid"] = VideoCodec.Libxvid,
                ["h264_nvenc"] = VideoCodec.H264Nvenc,
                ["hevc_nvenc"] = VideoCodec.HevcNvenc,
                ["av1_nvenc"] = VideoCodec.Av1Nvenc,
                ["h264_qsv"] = VideoCodec.H264Qsv,
                ["hevc_qsv"] = VideoCodec.HevcQsv,
                ["av1_qsv"] = VideoCodec.Av1Qsv,
                ["h264_vaapi"] = VideoCodec.H264Vaapi,
                ["hevc_vaapi"] = VideoCodec.HevcVaapi,
                ["av1_vaapi"] = VideoCodec.Av1Vaapi,
                ["h264_amf"] = VideoCodec.H264Amf,
                ["hevc_amf"] = VideoCodec.HevcAmf,
                ["av1_amf"] = VideoCodec.Av1Amf,
                ["h264_videotoolbox"] = VideoCodec.H264Videotoolbox,
                ["hevc_videotoolbox"] = VideoCodec.HevcVideotoolbox,
                ["prores"] = VideoCodec.Prores,
                ["mjpeg"] = VideoCodec.Mjpeg,
                ["dnxhd"] = VideoCodec.Dnxhd,
                ["ffv1"] = VideoCodec.Ffv1,
                ["huffyuv"] = VideoCodec.Huffyuv,
                ["wmv2"] = VideoCodec.Wmv2,
                ["rawvideo"] = VideoCodec.Rawvideo
            };

        private static readonly IReadOnlyDictionary<string, AudioCodec> AudioCodecs =
            new Dictionary<string, AudioCodec>(StringComparer.OrdinalIgnoreCase)
            {
                ["copy"] = AudioCodec.Copy,
                ["aac"] = AudioCodec.Aac,
                ["libmp3lame"] = AudioCodec.Libmp3lame,
                ["libopus"] = AudioCodec.Libopus,
                ["libvorbis"] = AudioCodec.Libvorbis,
                ["ac3"] = AudioCodec.Ac3,
                ["eac3"] = AudioCodec.Eac3,
                ["mp2"] = AudioCodec.Mp2,
                ["alac"] = AudioCodec.Alac,
                ["flac"] = AudioCodec.Flac,
                ["pcm_s16le"] = AudioCodec.PcmS16Le,
                ["pcm_s24le"] = AudioCodec.PcmS24Le,
                ["pcm_alaw"] = AudioCodec.PcmAlaw,
                ["pcm_mulaw"] = AudioCodec.PcmMulaw,
                ["wmav2"] = AudioCodec.Wmav2,
                ["libopencore_amrnb"] = AudioCodec.LibopencoreAmrnb,
                ["libvo_amrwbenc"] = AudioCodec.LibvoAmrwbenc,
                ["dca"] = AudioCodec.Dca,
                ["truehd"] = AudioCodec.Truehd,
                ["libmpegh"] = AudioCodec.Libmpegh,
                ["libspeex"] = AudioCodec.Libspeex,
                ["libfdk_aac"] = AudioCodec.LibfdkAac,
                ["mp3"] = AudioCodec.Mp3,
                ["ac4"] = AudioCodec.Ac4,
                ["opus"] = AudioCodec.Opus,
                ["mp1"] = AudioCodec.Mp1,
                ["aac_at"] = AudioCodec.AacAt
            };

        private static readonly IReadOnlyDictionary<string, TranscodingPreset> Presets =
            new Dictionary<string, TranscodingPreset>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = TranscodingPreset.Default,
                ["ultrafast"] = TranscodingPreset.Ultrafast,
                ["superfast"] = TranscodingPreset.Superfast,
                ["veryfast"] = TranscodingPreset.Veryfast,
                ["faster"] = TranscodingPreset.Faster,
                ["fast"] = TranscodingPreset.Fast,
                ["medium"] = TranscodingPreset.Medium,
                ["slow"] = TranscodingPreset.Slow,
                ["slower"] = TranscodingPreset.Slower,
                ["veryslow"] = TranscodingPreset.Veryslow
            };

        private static readonly IReadOnlyDictionary<string, VideoProfile> Profiles =
            new Dictionary<string, VideoProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["none"] = VideoProfile.None,
                ["baseline3.0"] = VideoProfile.Baseline30,
                ["baseline3.1"] = VideoProfile.Baseline31,
                ["main3.1"] = VideoProfile.Main31,
                ["main4.0"] = VideoProfile.Main40,
                ["high4.0"] = VideoProfile.High40,
                ["high4.1"] = VideoProfile.High41,
                ["high4.2"] = VideoProfile.High42
            };

        /// <summary>Video codec names as the API reads and writes them, in enum order.</summary>
        public static IReadOnlyList<string> VideoCodecNames { get; } =
            Enum.GetValues<VideoCodec>().Select(NameOf).ToArray();

        /// <summary>Audio codec names as the API reads and writes them, in enum order.</summary>
        public static IReadOnlyList<string> AudioCodecNames { get; } =
            Enum.GetValues<AudioCodec>().Select(NameOf).ToArray();

        /// <summary>Preset names as the API reads and writes them, in enum order.</summary>
        public static IReadOnlyList<string> PresetNames { get; } =
            Enum.GetValues<TranscodingPreset>().Select(NameOf).ToArray();

        /// <summary>Video profile names as the API reads and writes them, in enum order.</summary>
        public static IReadOnlyList<string> VideoProfileNames { get; } =
            Enum.GetValues<VideoProfile>().Select(profile => NameOf(Profiles, profile)).ToArray();

        /// <summary>Turns text into the video codec it names, or null when it names none.</summary>
        /// <param name="value">Value from a JSON body.</param>
        public static VideoCodec? ParseVideoCodec(string? value)
            => Parse(VideoCodecs, value);

        /// <summary>Turns text into the audio codec it names, or null when it names none.</summary>
        /// <param name="value">Value from a JSON body.</param>
        public static AudioCodec? ParseAudioCodec(string? value)
            => Parse(AudioCodecs, value);

        /// <summary>Turns text into the preset it names, or null when it names none.</summary>
        /// <param name="value">Value from a JSON body.</param>
        public static TranscodingPreset? ParsePreset(string? value)
            => Parse(Presets, value);

        /// <summary>Turns text into the video profile it names, or null when it names none.</summary>
        /// <param name="value">Value from a JSON body.</param>
        public static VideoProfile? ParseVideoProfile(string? value)
            => Parse(Profiles, value);

        /// <summary>The name a video codec is reported under.</summary>
        /// <param name="codec">Codec to name.</param>
        public static string NameOf(VideoCodec codec) => NameOf(VideoCodecs, codec);

        /// <summary>The name an audio codec is reported under.</summary>
        /// <param name="codec">Codec to name.</param>
        public static string NameOf(AudioCodec codec) => NameOf(AudioCodecs, codec);

        /// <summary>The name a preset is reported under.</summary>
        /// <param name="preset">Preset to name.</param>
        public static string NameOf(TranscodingPreset preset) => NameOf(Presets, preset);

        /// <summary>The name a video profile is reported under.</summary>
        /// <param name="profile">Profile to name.</param>
        public static string? NameOf(VideoProfile? profile)
            => profile is null ? null : NameOf(Profiles, profile.Value);

        /// <summary>The sentence a video codec that names nothing gets.</summary>
        public static string VideoCodecHint { get; } =
            "The video codec must be one of: " + string.Join(", ", VideoCodecNames) + ".";

        /// <summary>The sentence an audio codec that names nothing gets.</summary>
        public static string AudioCodecHint { get; } =
            "The audio codec must be one of: " + string.Join(", ", AudioCodecNames) + ".";

        /// <summary>The sentence a preset that names nothing gets.</summary>
        public static string PresetHint { get; } =
            "The preset must be one of: " + string.Join(", ", PresetNames) + ".";

        /// <summary>The sentence a video profile that names nothing gets.</summary>
        public static string VideoProfileHint { get; } =
            "The video profile must be one of: " + string.Join(", ", VideoProfileNames) + ".";

        private static TEnum? Parse<TEnum>(
            IReadOnlyDictionary<string, TEnum> table,
            string? value)
            where TEnum : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return table.TryGetValue(value.Trim(), out var member) ? member : null;
        }

        private static string NameOf<TEnum>(
            IReadOnlyDictionary<string, TEnum> table,
            TEnum member)
            where TEnum : struct, Enum
        {
            foreach (var entry in table)
            {
                if (EqualityComparer<TEnum>.Default.Equals(entry.Value, member))
                {
                    return entry.Key;
                }
            }

            throw new ArgumentOutOfRangeException(
                nameof(member),
                member,
                "No wire name is defined for that member.");
        }
    }
}
