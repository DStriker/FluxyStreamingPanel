namespace Fluxy.API.Contracts.Transcoding
{
    /// <summary>
    /// What the profile form sends to change a transcoding profile.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The form holds one profile, so it sends the whole option set - the convention the tariff
    /// plan and the streams already follow. An option the operator cleared therefore arrives as
    /// <c>null</c> and clears the column, which is the only way to unset an ffmpeg flag that was
    /// set once. The four non-null columns - the name, the two codecs and the preset - treat
    /// <c>null</c> as "keep the stored value".
    /// </para>
    /// <para>
    /// Enum fields are ffmpeg's lowercase names, exactly as on the create.
    /// </para>
    /// </remarks>
    public sealed record UpdateTranscodingProfileRequest
    {
        /// <summary>New name, or null to keep the stored one.</summary>
        public string? Name { get; init; }

        /// <summary>New video encoder name, or null to keep the stored one.</summary>
        public string? VideoCodec { get; init; }

        /// <summary>New audio encoder name, or null to keep the stored one.</summary>
        public string? AudioCodec { get; init; }

        /// <summary>New preset name, or null to keep the stored one.</summary>
        public string? Preset { get; init; }

        /// <summary>New H.264 profile name, or null to send none.</summary>
        public string? VideoProfile { get; init; }

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
