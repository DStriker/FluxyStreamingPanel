using Fluxy.Core.Transcoding.Enums;

namespace Fluxy.Core.Transcoding.ReadModels
{
    /// <summary>
    /// One transcoding profile as the API answers with it - every option the row holds.
    /// </summary>
    /// <remarks>
    /// The list and the detail answer the same shape, because everything here is what the
    /// table's options cell draws and what the form opens on. There is no "list versus detail"
    /// split as there is for a stream: a profile has no relations to leave out.
    /// </remarks>
    public sealed record TranscodingProfileView
    {
        /// <summary>Identifier of the profile.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name.</summary>
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
