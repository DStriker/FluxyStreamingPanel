namespace Fluxy.API.Contracts.Settings
{
    /// <summary>
    /// What the MAG settings form sends to change the MAG settings.
    /// </summary>
    /// <remarks>
    /// A full write: every one of the eleven values is required. The two container and theme
    /// fields arrive as text because their wire vocabulary is the owner's, not the enum member
    /// names. No data annotations, for the reason every admin write here gives: the antiforgery
    /// check has to run before anything judges the body.
    /// </remarks>
    public sealed record UpdateMagSettingsRequest
    {
        /// <summary>Whether every category is shown.</summary>
        public bool? ShowAllCategories { get; init; }

        /// <summary>Whether the strict security checks are enforced.</summary>
        public bool? StrictSecurity { get; init; }

        /// <summary>Whether channel logos are drawn.</summary>
        public bool? ShowChannelLogos { get; init; }

        /// <summary>Whether preview logos are drawn.</summary>
        public bool? ShowPreviewLogos { get; init; }

        /// <summary>Container the portal delivers by default: <c>mpeg-ts</c>, <c>mpegts</c> or <c>hls</c>.</summary>
        public string? DefaultContainer { get; init; }

        /// <summary>Theme the portal is shown with by default.</summary>
        public string? DefaultTheme { get; init; }

        /// <summary>Longest recording the portal allows, in minutes.</summary>
        public int? RecordingMaxLengthMinutes { get; init; }

        /// <summary>How many local recordings a device may keep; <c>0</c> allows none.</summary>
        public int? MaxLocalRecordings { get; init; }

        /// <summary>How many simultaneous viewers one device may have.</summary>
        public int? PlaybackLimit { get; init; }

        /// <summary>Set-top-box models allowed to connect.</summary>
        public IReadOnlyList<string>? AllowedStbTypes { get; init; }

        /// <summary>Set-top-box models allowed to record.</summary>
        public IReadOnlyList<string>? AllowedRecordingStbTypes { get; init; }
    }
}
