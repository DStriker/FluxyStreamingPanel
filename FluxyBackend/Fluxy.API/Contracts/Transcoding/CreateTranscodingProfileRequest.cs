namespace Fluxy.API.Contracts.Transcoding
{
    /// <summary>
    /// What the profile form sends to create a transcoding profile.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No data annotations on purpose: framework validation runs before the action, so a body
    /// that failed one would never reach the antiforgery check. Every field is checked in the
    /// service after the CSRF pair has been accepted.
    /// </para>
    /// <para>
    /// <b>The enum fields travel as ffmpeg's own lowercase names</b> (<c>libx264</c>,
    /// <c>pcm_alaw</c>, <c>veryslow</c>, <c>high4.0</c>) rather than as C# member names, because
    /// that is what the operator reads on the screen and what the frontend's option list
    /// mirrors. The controller parses them through the text parser, which owns the spelling of
    /// every member in both directions.
    /// </para>
    /// <para>
    /// Every option is optional, and a null means "do not pass this flag to ffmpeg". The three
    /// the service defaults are the two codecs and the preset: a body that names none of them
    /// creates a profile that changes nothing.
    /// </para>
    /// </remarks>
    public sealed record CreateTranscodingProfileRequest
    {
        /// <summary>Display name. Trimmed before it is stored.</summary>
        public string? Name { get; init; }

        /// <summary>Video encoder name, or null for <c>copy</c>.</summary>
        public string? VideoCodec { get; init; }

        /// <summary>Audio encoder name, or null for <c>copy</c>.</summary>
        public string? AudioCodec { get; init; }

        /// <summary>Preset name, or null for <c>default</c>.</summary>
        public string? Preset { get; init; }

        /// <summary>H.264 profile name, or null for none.</summary>
        public string? VideoProfile { get; init; }

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
