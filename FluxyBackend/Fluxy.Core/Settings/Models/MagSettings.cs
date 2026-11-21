using Fluxy.Core.Settings.Enums;

namespace Fluxy.Core.Settings.Models
{
    /// <summary>
    /// The installation-wide MAG portal settings an operator owns: what the portal shows, which
    /// containers and themes it defaults to, how long recordings may run, and which set-top-box
    /// models may connect.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One row per installation, carrying the fixed identifier of
    /// <see cref="Utils.MagSettingsDefaults.Id"/>. Nothing in this pass produces a MAG portal -
    /// the row is stored, validated and read by an operator only - so the service that owns it
    /// knows nothing beyond the rules its values have to satisfy.
    /// </para>
    /// <para>
    /// Both model lists are stored as PostgreSQL <c>text[]</c> columns, the shape the server
    /// country lists and the subscription whitelists use: they are read with the row and never
    /// queried by position, so a child table would buy a join for an order the array keeps.
    /// </para>
    /// </remarks>
    public sealed record MagSettings
    {
        /// <summary>Identifier of the single row.</summary>
        public required Guid Id { get; init; }

        /// <summary>Whether the portal shows every category rather than a collapsed set.</summary>
        public required bool ShowAllCategories { get; init; }

        /// <summary>Whether the portal enforces the strict security checks.</summary>
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

        /// <summary>How many local recordings a device may keep; <c>0</c> means none.</summary>
        public required int MaxLocalRecordings { get; init; }

        /// <summary>How many simultaneous viewers one device may have.</summary>
        public required int PlaybackLimit { get; init; }

        /// <summary>Set-top-box models allowed to connect, in the operator's order.</summary>
        public required string[] AllowedStbTypes { get; init; }

        /// <summary>Set-top-box models allowed to record, in the operator's order.</summary>
        public required string[] AllowedRecordingStbTypes { get; init; }
    }
}
