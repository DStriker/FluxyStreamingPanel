namespace Fluxy.API.Contracts.Settings
{
    /// <summary>
    /// The MAG portal settings as the admin screen reads them.
    /// </summary>
    /// <remarks>
    /// <c>defaultContainer</c> is answered as the canonical <c>mpeg-ts</c> or <c>hls</c>, and
    /// <c>defaultTheme</c> as the owner's own spelling (<c>ocean blue</c>, and <c>cappucino</c>
    /// deliberately uncorrected). Both model lists are canonical spellings in the operator's
    /// order.
    /// </remarks>
    public sealed record MagSettingsResponse
    {
        /// <summary>Whether every category is shown.</summary>
        public required bool ShowAllCategories { get; init; }

        /// <summary>Whether the strict security checks are enforced.</summary>
        public required bool StrictSecurity { get; init; }

        /// <summary>Whether channel logos are drawn.</summary>
        public required bool ShowChannelLogos { get; init; }

        /// <summary>Whether preview logos are drawn.</summary>
        public required bool ShowPreviewLogos { get; init; }

        /// <summary>Container the portal delivers by default.</summary>
        public required string DefaultContainer { get; init; }

        /// <summary>Theme the portal is shown with by default.</summary>
        public required string DefaultTheme { get; init; }

        /// <summary>Longest recording the portal allows, in minutes.</summary>
        public required int RecordingMaxLengthMinutes { get; init; }

        /// <summary>How many local recordings a device may keep.</summary>
        public required int MaxLocalRecordings { get; init; }

        /// <summary>How many simultaneous viewers one device may have.</summary>
        public required int PlaybackLimit { get; init; }

        /// <summary>Set-top-box models allowed to connect, in the operator's order.</summary>
        public required IReadOnlyList<string> AllowedStbTypes { get; init; }

        /// <summary>Set-top-box models allowed to record, in the operator's order.</summary>
        public required IReadOnlyList<string> AllowedRecordingStbTypes { get; init; }
    }
}
