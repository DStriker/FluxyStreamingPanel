namespace Fluxy.API.Contracts.Transcoding
{
    /// <summary>
    /// One transcoding profile as the API answers with it, in both the list and the detail.
    /// </summary>
    /// <remarks>
    /// The option fields are the same values the request accepts, reported under the names they
    /// were sent by: the four enums as ffmpeg's lowercase names, the numbers as numbers. The
    /// nullable ones stay null when the row carries no value, which is the state "no flag is
    /// passed to ffmpeg" rather than a missing field.
    /// </remarks>
    public sealed record AdminTranscodingProfileResponse
    {
        /// <summary>Identifier of the profile, as text. The table shows it shortened.</summary>
        public required string Id { get; init; }

        /// <summary>Display name.</summary>
        public required string Name { get; init; }

        /// <summary>Video encoder, as its ffmpeg name.</summary>
        public required string VideoCodec { get; init; }

        /// <summary>Audio encoder, as its ffmpeg name.</summary>
        public required string AudioCodec { get; init; }

        /// <summary>Preset, as its ffmpeg name.</summary>
        public required string Preset { get; init; }

        /// <summary>H.264 profile, as its ffmpeg name, or null.</summary>
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

        /// <summary>Constant rate factor, or null.</summary>
        public int? Crf { get; init; }

        /// <summary>Output width, or null.</summary>
        public int? Width { get; init; }

        /// <summary>Output height, or null.</summary>
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
