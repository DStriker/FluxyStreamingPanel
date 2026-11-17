using Fluxy.Core.Transcoding.Enums;

namespace Fluxy.Core.Transcoding.Models
{
    /// <summary>
    /// Immutable business representation of one transcoding profile: a named set of ffmpeg
    /// options a stream or a catalogue row may be converted with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A profile is a bag of options, and every option is optional except the name.</b> A
    /// null value means "do not pass this flag to ffmpeg", so a profile that names nothing but
    /// its name is a profile that changes nothing - which is a legitimate profile and the shape
    /// the seeded row almost has. The three options a create always writes - the two codecs and
    /// the preset - are the exceptions: <see cref="VideoCodec.Copy"/>,
    /// <see cref="AudioCodec.Copy"/> and <see cref="TranscodingPreset.Default"/> mean "pass no
    /// flag", so a create that names none of them stores the value that does nothing rather
    /// than a null the picker would have to explain.
    /// </para>
    /// <para>
    /// <b>A value is stored only when it is a valid one.</b> The service judges every bound the
    /// table states - a CRF outside 0..51, a channel count outside 1..8, a width below -1 - and
    /// an untouched column stays null. The redaction rectangle is all-or-none: four numbers
    /// that describe one region, where three of them name nothing.
    /// </para>
    /// <para>
    /// The name is unique for the installation and compared <i>case sensitively</i>, which is
    /// the rule the guide sources, the accounts and the groups are held to.
    /// </para>
    /// <para>
    /// Nothing about ffmpeg lives here. The model records what an operator asked for; the
    /// relaying process that will build the command line reads these values and decides how to
    /// spell them.
    /// </para>
    /// </remarks>
    public sealed record TranscodingProfile
    {
        /// <summary>Identifier of the profile.</summary>
        public required Guid Id { get; init; }

        /// <summary>
        /// Display name, between 1 and
        /// <see cref="Utils.TranscodingProfileLimits.NameMaxLength"/> characters, unique for the
        /// installation when compared case sensitively.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>Video encoder, or <see cref="VideoCodec.Copy"/> to leave the stream alone.</summary>
        public required VideoCodec VideoCodec { get; init; }

        /// <summary>Audio encoder, or <see cref="AudioCodec.Copy"/> to leave the stream alone.</summary>
        public required AudioCodec AudioCodec { get; init; }

        /// <summary>The encoder preset, or <see cref="TranscodingPreset.Default"/> for none.</summary>
        public required TranscodingPreset Preset { get; init; }

        /// <summary>H.264 profile flag, or null to send none.</summary>
        public VideoProfile? VideoProfile { get; init; }

        /// <summary>Average video bitrate in kbps, or null to send none.</summary>
        public int? VideoBitrateAvg { get; init; }

        /// <summary>Average audio bitrate in kbps, or null to send none.</summary>
        public int? AudioBitrateAvg { get; init; }

        /// <summary>Lower end of the bitrate tolerance window in kbps, or null.</summary>
        public int? MinBitrateTolerance { get; init; }

        /// <summary>Upper end of the bitrate tolerance window in kbps, or null.</summary>
        public int? MaxBitrateTolerance { get; init; }

        /// <summary>Decoder buffer size in kbps, or null.</summary>
        public int? BufferSize { get; init; }

        /// <summary>
        /// Constant rate factor, 0..51, or null. Mutually exclusive with the bitrate fields in
        /// ffmpeg's own terms; this pass records both and lets the encoder complain.
        /// </summary>
        public int? Crf { get; init; }

        /// <summary>
        /// Output width in pixels, or null for the source width. <c>-1</c> is stored as-is and
        /// means "derive it from the height so the aspect ratio is kept".
        /// </summary>
        public int? Width { get; init; }

        /// <summary>Output height in pixels, or null. <c>-1</c> keeps the aspect ratio.</summary>
        public int? Height { get; init; }

        /// <summary>
        /// Aspect ratio override such as <c>16:9</c>, or null. A bounded string: ffmpeg accepts
        /// several spellings and this pass records what the operator typed.
        /// </summary>
        public string? AspectRatio { get; init; }

        /// <summary>Output frame rate, greater than zero, or null.</summary>
        public decimal? Framerate { get; init; }

        /// <summary>Audio sample rate in hertz, greater than zero, or null.</summary>
        public int? AudioSampleRate { get; init; }

        /// <summary>Number of audio channels, 1..8, or null.</summary>
        public int? AudioChannels { get; init; }

        /// <summary>
        /// Encoder thread count, or null. <c>0</c> is stored as-is and means "let ffmpeg
        /// decide".
        /// </summary>
        public int? Threads { get; init; }

        /// <summary>Address of the watermark picture, or null when the profile has none.</summary>
        public string? LogoUrl { get; init; }

        /// <summary>Left edge of the redaction rectangle, or null when the whole set is absent.</summary>
        public int? RedactionX { get; init; }

        /// <summary>Top edge of the redaction rectangle, or null when the whole set is absent.</summary>
        public int? RedactionY { get; init; }

        /// <summary>Width of the redaction rectangle, or null when the whole set is absent.</summary>
        public int? RedactionWidth { get; init; }

        /// <summary>Height of the redaction rectangle, or null when the whole set is absent.</summary>
        public int? RedactionHeight { get; init; }

        /// <summary>Whether the profile carries a redaction rectangle at all.</summary>
        public bool HasRedaction =>
            RedactionX is not null
            && RedactionY is not null
            && RedactionWidth is not null
            && RedactionHeight is not null;

        /// <summary>When the profile was created. No endpoint changes it.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>When the row was last written.</summary>
        public required DateTimeOffset UpdatedAt { get; init; }
    }
}
