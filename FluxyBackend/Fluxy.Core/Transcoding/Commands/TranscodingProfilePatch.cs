using Fluxy.Core.Transcoding.Enums;

namespace Fluxy.Core.Transcoding.Commands
{
    /// <summary>
    /// What an administrator changes about an existing transcoding profile.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The patch carries the whole option set.</b> The form holds one profile, so what the
    /// operator sees and what the row becomes are the same statement - the convention the
    /// tariff plan and the streams already follow. A field that is absent therefore means what
    /// its column can mean: <see cref="Name"/>, <see cref="VideoCodec"/>,
    /// <see cref="AudioCodec"/> and <see cref="Preset"/> are non-null columns and a null keeps
    /// the stored value, while every other field is nullable and a null clears it - which is the
    /// only way to unset an option that was set once.
    /// </para>
    /// <para>
    /// The distinction matters because "no flag is passed to ffmpeg" is a real state rather
    /// than a missing one: a profile that no longer needs a CRF must be able to say so without
    /// pretending the field was never part of the request.
    /// </para>
    /// <para>
    /// The redaction rectangle follows the create: four nullable numbers judged all-or-none by
    /// the service, so a patch that names two of them and clears the other two is refused.
    /// </para>
    /// </remarks>
    public sealed record TranscodingProfilePatch
    {
        /// <summary>New name, or null to keep the stored one. Trimmed before it is stored.</summary>
        public string? Name { get; init; }

        /// <summary>New video encoder, or null to keep the stored one.</summary>
        public VideoCodec? VideoCodec { get; init; }

        /// <summary>New audio encoder, or null to keep the stored one.</summary>
        public AudioCodec? AudioCodec { get; init; }

        /// <summary>New preset, or null to keep the stored one.</summary>
        public TranscodingPreset? Preset { get; init; }

        /// <summary>New H.264 profile, or null to send none.</summary>
        public VideoProfile? VideoProfile { get; init; }

        /// <summary>New average video bitrate in kbps, or null to send none.</summary>
        public int? VideoBitrateAvg { get; init; }

        /// <summary>New average audio bitrate in kbps, or null to send none.</summary>
        public int? AudioBitrateAvg { get; init; }

        /// <summary>New lower bitrate tolerance in kbps, or null to send none.</summary>
        public int? MinBitrateTolerance { get; init; }

        /// <summary>New upper bitrate tolerance in kbps, or null to send none.</summary>
        public int? MaxBitrateTolerance { get; init; }

        /// <summary>New decoder buffer size in kbps, or null to send none.</summary>
        public int? BufferSize { get; init; }

        /// <summary>New constant rate factor, or null to send none.</summary>
        public int? Crf { get; init; }

        /// <summary>New output width, or null to use the source width.</summary>
        public int? Width { get; init; }

        /// <summary>New output height, or null to use the source height.</summary>
        public int? Height { get; init; }

        /// <summary>New aspect ratio override, or null to send none.</summary>
        public string? AspectRatio { get; init; }

        /// <summary>New output frame rate, or null to send none.</summary>
        public decimal? Framerate { get; init; }

        /// <summary>New audio sample rate in hertz, or null to send none.</summary>
        public int? AudioSampleRate { get; init; }

        /// <summary>New audio channel count, or null to send none.</summary>
        public int? AudioChannels { get; init; }

        /// <summary>New encoder thread count, or null to send none.</summary>
        public int? Threads { get; init; }

        /// <summary>New watermark address, or null to send none.</summary>
        public string? LogoUrl { get; init; }

        /// <summary>New left edge of the redaction rectangle, or null to clear it.</summary>
        public int? RedactionX { get; init; }

        /// <summary>New top edge of the redaction rectangle, or null to clear it.</summary>
        public int? RedactionY { get; init; }

        /// <summary>New width of the redaction rectangle, or null to clear it.</summary>
        public int? RedactionWidth { get; init; }

        /// <summary>New height of the redaction rectangle, or null to clear it.</summary>
        public int? RedactionHeight { get; init; }
    }
}
