using Fluxy.Core.Settings.Enums;
using Fluxy.Core.Settings.Models;

namespace Fluxy.Core.Settings.Utils
{
    /// <summary>
    /// The MAG values a fresh installation starts with, and the answer whenever the settings row
    /// is absent.
    /// </summary>
    /// <remarks>
    /// The table exists for the reason <see cref="ApplicationSettingsDefaults"/> does: the
    /// integration fixture truncates the database and re-seeds only the base groups and the main
    /// server, so the snapshot provider answers these values rather than inventing them from a
    /// null row.
    /// </remarks>
    public static class MagSettingsDefaults
    {
        /// <summary>Identifier of the single settings row, the same on every installation.</summary>
        public static readonly Guid Id = new("00000000-0000-0000-0000-000000000502");

        /// <summary>Whether every category is shown.</summary>
        public const bool ShowAllCategories = true;

        /// <summary>Whether the strict security checks are enforced.</summary>
        public const bool StrictSecurity = true;

        /// <summary>Whether channel logos are drawn.</summary>
        public const bool ShowChannelLogos = true;

        /// <summary>Whether preview logos are drawn.</summary>
        public const bool ShowPreviewLogos = true;

        /// <summary>Container the portal delivers by default.</summary>
        public const MagContainer DefaultContainer = MagContainer.MpegTs;

        /// <summary>Theme the portal is shown with by default.</summary>
        public const MagTheme DefaultTheme = MagTheme.Default;

        /// <summary>Longest recording the portal allows, in minutes.</summary>
        public const int RecordingMaxLengthMinutes = 180;

        /// <summary>How many local recordings a device may keep.</summary>
        public const int MaxLocalRecordings = 10;

        /// <summary>How many simultaneous viewers one device may have.</summary>
        public const int PlaybackLimit = 3;

        /// <summary>The three models that may record.</summary>
        public static IReadOnlyList<string> AllowedRecordingStbTypes { get; } =
            ["MAG255", "MAG256", "MAG257"];

        /// <summary>The whole default row, with every model allowed and no recording exception.</summary>
        public static MagSettings Create() => new()
        {
            Id = Id,
            ShowAllCategories = ShowAllCategories,
            StrictSecurity = StrictSecurity,
            ShowChannelLogos = ShowChannelLogos,
            ShowPreviewLogos = ShowPreviewLogos,
            DefaultContainer = DefaultContainer,
            DefaultTheme = DefaultTheme,
            RecordingMaxLengthMinutes = RecordingMaxLengthMinutes,
            MaxLocalRecordings = MaxLocalRecordings,
            PlaybackLimit = PlaybackLimit,
            AllowedStbTypes = [.. StbTypeCatalog.All],
            AllowedRecordingStbTypes = [.. AllowedRecordingStbTypes]
        };
    }
}
