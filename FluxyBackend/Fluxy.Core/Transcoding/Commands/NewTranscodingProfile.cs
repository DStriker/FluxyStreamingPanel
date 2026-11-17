using Fluxy.Core.Transcoding.Enums;

namespace Fluxy.Core.Transcoding.Commands
{
    /// <summary>
    /// Everything an administrator supplies to create a transcoding profile.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every option is nullable because leaving one out is the ordinary case: a profile that
    /// changes one thing and nothing else is a profile. The service applies the create defaults
    /// for the three that always have a value - <see cref="VideoCodec.Copy"/>,
    /// <see cref="AudioCodec.Copy"/> and <see cref="TranscodingPreset.Default"/> - and stores
    /// null for everything else, which means "do not pass that flag to ffmpeg".
    /// </para>
    /// <para>
    /// The redaction rectangle is four separate numbers rather than a nested object: they are
    /// four columns of one row, the form draws them as one composite control, and the service
    /// is what refuses a set that is present only in part.
    /// </para>
    /// </remarks>
    public sealed record NewTranscodingProfile
    {
        /// <summary>Display name. Trimmed before it is stored.</summary>
        public required string Name { get; init; }

        /// <summary>Video encoder, or null for <see cref="VideoCodec.Copy"/>.</summary>
        public VideoCodec? VideoCodec { get; init; }

        /// <summary>Audio encoder, or null for <see cref="AudioCodec.Copy"/>.</summary>
        public AudioCodec? AudioCodec { get; init; }

        /// <summary>Encoder preset, or null for <see cref="TranscodingPreset.Default"/>.</summary>
        public TranscodingPreset? Preset { get; init; }

        /// <summary>H.264 profile, or null for none.</summary>
        public VideoProfile? VideoProfile { get; init; }

        /// <summary>Average video bitrate in kbps, or null.</summary>
        public int? VideoBitrateAvg { get; init; }

        /// <summary>Average audio bitrate in kbps, or null.</summary>
        public int? AudioBitrateAvg { get; init; }

        /// <summary>Lower bitrate tolerance in kbps, or null.</summary>
        public int? MinBitrateTolerance { get; init; }

        /// <summary>Upper bitrate tolerance in kbps, or null.</summary>
        public int? MaxBitrateTolerance { get; init; }

        /// <summary>Decoder buffer size in kbps, or null.</summary>
        public int? BufferSize { get; init; }

        /// <summary>Constant rate factor 0..51, or null.</summary>
        public int? Crf { get; init; }

        /// <summary>Output width, or null. <c>-1</c> keeps the aspect ratio.</summary>
        public int? Width { get; init; }

        /// <summary>Output height, or null. <c>-1</c> keeps the aspect ratio.</summary>
        public int? Height { get; init; }

        /// <summary>Aspect ratio override such as <c>16:9</c>, or null.</summary>
        public string? AspectRatio { get; init; }

        /// <summary>Output frame rate, or null.</summary>
        public decimal? Framerate { get; init; }

        /// <summary>Audio sample rate in hertz, or null.</summary>
        public int? AudioSampleRate { get; init; }

        /// <summary>Audio channel count, or null.</summary>
        public int? AudioChannels { get; init; }

        /// <summary>Encoder thread count, or null. <c>0</c> means auto.</summary>
        public int? Threads { get; init; }

        /// <summary>Watermark address, or null.</summary>
        public string? LogoUrl { get; init; }

        /// <summary>Left edge of the redaction rectangle, or null.</summary>
        public int? RedactionX { get; init; }

        /// <summary>Top edge of the redaction rectangle, or null.</summary>
        public int? RedactionY { get; init; }

        /// <summary>Width of the redaction rectangle, or null.</summary>
        public int? RedactionWidth { get; init; }

        /// <summary>Height of the redaction rectangle, or null.</summary>
        public int? RedactionHeight { get; init; }
    }
}
