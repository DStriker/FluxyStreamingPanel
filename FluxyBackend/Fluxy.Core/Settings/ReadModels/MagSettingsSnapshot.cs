using Fluxy.Core.Settings.Enums;

namespace Fluxy.Core.Settings.ReadModels
{
    /// <summary>
    /// One immutable reading of the MAG portal settings, as the server uses them.
    /// </summary>
    /// <remarks>
    /// Nothing in this pass consumes the values - there is no MAG portal producer - so the
    /// snapshot exists to be read by an operator and to give a future producer one stable shape.
    /// Both arrays are copies, so a reader cannot reach into a cached instance.
    /// </remarks>
    public sealed record MagSettingsSnapshot
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
        public required MagContainer DefaultContainer { get; init; }

        /// <summary>Theme the portal is shown with by default.</summary>
        public required MagTheme DefaultTheme { get; init; }

        /// <summary>Longest recording the portal allows, in minutes.</summary>
        public required int RecordingMaxLengthMinutes { get; init; }

        /// <summary>How many local recordings a device may keep.</summary>
        public required int MaxLocalRecordings { get; init; }

        /// <summary>How many simultaneous viewers one device may have.</summary>
        public required int PlaybackLimit { get; init; }

        /// <summary>Set-top-box models allowed to connect, in the operator's order.</summary>
        public required string[] AllowedStbTypes { get; init; }

        /// <summary>Set-top-box models allowed to record, in the operator's order.</summary>
        public required string[] AllowedRecordingStbTypes { get; init; }
    }
}
