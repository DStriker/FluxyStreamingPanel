namespace Fluxy.Core.Settings.Commands
{
    /// <summary>
    /// The whole MAG settings row as the admin form sends it.
    /// </summary>
    /// <remarks>
    /// A full write: every one of the eleven values is required and replaces the stored one. The
    /// two container and theme fields arrive as text because their wire vocabulary is the
    /// owner's, not the enum member names; the service turns them into members and refuses a
    /// value that names none.
    /// </remarks>
    public sealed record MagSettingsPatch
    {
        /// <summary>Whether every category is shown.</summary>
        public required bool ShowAllCategories { get; init; }

        /// <summary>Whether the strict security checks are enforced.</summary>
        public required bool StrictSecurity { get; init; }

        /// <summary>Whether channel logos are drawn.</summary>
        public required bool ShowChannelLogos { get; init; }

        /// <summary>Whether preview logos are drawn.</summary>
        public required bool ShowPreviewLogos { get; init; }

        /// <summary>Container the portal delivers by default, as text.</summary>
        public required string? DefaultContainer { get; init; }

        /// <summary>Theme the portal is shown with by default, as text.</summary>
        public required string? DefaultTheme { get; init; }

        /// <summary>Longest recording the portal allows, in minutes.</summary>
        public required int RecordingMaxLengthMinutes { get; init; }

        /// <summary>How many local recordings a device may keep; <c>0</c> allows none.</summary>
        public required int MaxLocalRecordings { get; init; }

        /// <summary>How many simultaneous viewers one device may have.</summary>
        public required int PlaybackLimit { get; init; }

        /// <summary>Set-top-box models allowed to connect.</summary>
        public required IReadOnlyList<string> AllowedStbTypes { get; init; }

        /// <summary>Set-top-box models allowed to record.</summary>
        public required IReadOnlyList<string> AllowedRecordingStbTypes { get; init; }
    }
}
