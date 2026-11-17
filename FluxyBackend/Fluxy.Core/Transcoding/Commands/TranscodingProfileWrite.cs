using Fluxy.Core.Transcoding.Enums;

namespace Fluxy.Core.Transcoding.Commands
{
    /// <summary>
    /// The values a create or an update writes to a transcoding profile row.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Separate from <see cref="Models.TranscodingProfile"/> because a write has already been
    /// resolved by the service: every bound has been judged, the name has been trimmed, the
    /// blank strings have become nulls and the create defaults have been applied. The
    /// repository therefore writes exactly what it is given instead of repeating the rules.
    /// </para>
    /// <para>
    /// The three non-nullable values are the ones a row always has. Every other value is
    /// nullable, and null means "do not pass this flag to ffmpeg" - it is a meaningful state
    /// rather than a missing one.
    /// </para>
    /// </remarks>
    public sealed record TranscodingProfileWrite
    {
        /// <summary>Display name, unique for the installation.</summary>
        public required string Name { get; init; }

        /// <summary>Video encoder.</summary>
        public required VideoCodec VideoCodec { get; init; }

        /// <summary>Audio encoder.</summary>
        public required AudioCodec AudioCodec { get; init; }

        /// <summary>Encoder preset.</summary>
        public required TranscodingPreset Preset { get; init; }

        /// <summary>H.264 profile, or null.</summary>
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

        /// <summary>Aspect ratio override, or null.</summary>
        public string? AspectRatio { get; init; }

        /// <summary>Output frame rate, or null.</summary>
        public decimal? Framerate { get; init; }

        /// <summary>Audio sample rate in hertz, or null.</summary>
        public int? AudioSampleRate { get; init; }

        /// <summary>Audio channel count, or null.</summary>
        public int? AudioChannels { get; init; }

        /// <summary>Encoder thread count, or null.</summary>
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
