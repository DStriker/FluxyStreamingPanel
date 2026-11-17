using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Transcoding.Enums;
using Fluxy.Core.Transcoding.Models;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of one transcoding profile. It is mutable, has no behaviour
    /// beyond mapping to <see cref="TranscodingProfile"/>, and knows nothing about HTTP.
    /// </summary>
    /// <remarks>
    /// Every option column is nullable because null is the meaningful state "do not pass this
    /// flag to ffmpeg" - not a missing value the database should invent. The three columns a
    /// profile always has are the two codecs and the preset, and the service writes their
    /// do-nothing members when a create names none.
    /// </remarks>
    public sealed class TranscodingProfileEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TranscodingProfileEntity"/> class for a
        /// profile that is about to be inserted. The identifier and both audit timestamps are
        /// generated.
        /// </summary>
        public TranscodingProfileEntity()
        {
        }

        /// <summary>Display name, unique for the installation.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Video encoder.</summary>
        public VideoCodec VideoCodec { get; set; }

        /// <summary>Audio encoder.</summary>
        public AudioCodec AudioCodec { get; set; }

        /// <summary>Encoder preset.</summary>
        public TranscodingPreset Preset { get; set; }

        /// <summary>H.264 profile, or null.</summary>
        public VideoProfile? VideoProfile { get; set; }

        /// <summary>Average video bitrate in kbps, or null.</summary>
        public int? VideoBitrateAvg { get; set; }

        /// <summary>Average audio bitrate in kbps, or null.</summary>
        public int? AudioBitrateAvg { get; set; }

        /// <summary>Lower bitrate tolerance in kbps, or null.</summary>
        public int? MinBitrateTolerance { get; set; }

        /// <summary>Upper bitrate tolerance in kbps, or null.</summary>
        public int? MaxBitrateTolerance { get; set; }

        /// <summary>Decoder buffer size in kbps, or null.</summary>
        public int? BufferSize { get; set; }

        /// <summary>Constant rate factor, or null.</summary>
        public int? Crf { get; set; }

        /// <summary>Output width, or null.</summary>
        public int? Width { get; set; }

        /// <summary>Output height, or null.</summary>
        public int? Height { get; set; }

        /// <summary>Aspect ratio override, or null.</summary>
        public string? AspectRatio { get; set; }

        /// <summary>Output frame rate, or null.</summary>
        public decimal? Framerate { get; set; }

        /// <summary>Audio sample rate in hertz, or null.</summary>
        public int? AudioSampleRate { get; set; }

        /// <summary>Audio channel count, or null.</summary>
        public int? AudioChannels { get; set; }

        /// <summary>Encoder thread count, or null.</summary>
        public int? Threads { get; set; }

        /// <summary>Watermark address, or null.</summary>
        public string? LogoUrl { get; set; }

        /// <summary>Left edge of the redaction rectangle, or null.</summary>
        public int? RedactionX { get; set; }

        /// <summary>Top edge of the redaction rectangle, or null.</summary>
        public int? RedactionY { get; set; }

        /// <summary>Width of the redaction rectangle, or null.</summary>
        public int? RedactionWidth { get; set; }

        /// <summary>Height of the redaction rectangle, or null.</summary>
        public int? RedactionHeight { get; set; }

        /// <summary>Builds the business model out of this row.</summary>
        public TranscodingProfile ToModel() => new()
        {
            Id = Id,
            Name = Name,
            VideoCodec = VideoCodec,
            AudioCodec = AudioCodec,
            Preset = Preset,
            VideoProfile = VideoProfile,
            VideoBitrateAvg = VideoBitrateAvg,
            AudioBitrateAvg = AudioBitrateAvg,
            MinBitrateTolerance = MinBitrateTolerance,
            MaxBitrateTolerance = MaxBitrateTolerance,
            BufferSize = BufferSize,
            Crf = Crf,
            Width = Width,
            Height = Height,
            AspectRatio = AspectRatio,
            Framerate = Framerate,
            AudioSampleRate = AudioSampleRate,
            AudioChannels = AudioChannels,
            Threads = Threads,
            LogoUrl = LogoUrl,
            RedactionX = RedactionX,
            RedactionY = RedactionY,
            RedactionWidth = RedactionWidth,
            RedactionHeight = RedactionHeight,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }
}
