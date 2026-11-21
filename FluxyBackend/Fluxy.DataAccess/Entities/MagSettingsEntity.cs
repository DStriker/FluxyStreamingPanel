using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Settings.Enums;
using Fluxy.Core.Settings.Models;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of the single MAG settings row. It is mutable, has no behaviour
    /// beyond mapping to <see cref="MagSettings"/>, and knows nothing about HTTP.
    /// </summary>
    /// <remarks>
    /// Both model lists are PostgreSQL <c>text[]</c> columns for the reason the server country
    /// list is: they are read with the row and never queried by position. Every column is
    /// non-nullable and declares no database default.
    /// </remarks>
    public sealed class MagSettingsEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MagSettingsEntity"/> class for the row
        /// whose identifier the defaults statics fix.
        /// </summary>
        /// <remarks>
        /// The two timestamps are placeholders: both are overwritten for an inserted row by
        /// <c>FluxyDbContext.ApplyAudit</c>, which is the only place audit values are ever
        /// written.
        /// </remarks>
        /// <param name="id">The identifier the row must carry.</param>
        public MagSettingsEntity(Guid id)
            : base(id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        {
        }

        /// <summary>Whether every category is shown.</summary>
        public bool ShowAllCategories { get; set; }

        /// <summary>Whether the strict security checks are enforced.</summary>
        public bool StrictSecurity { get; set; }

        /// <summary>Whether channel logos are drawn.</summary>
        public bool ShowChannelLogos { get; set; }

        /// <summary>Whether preview logos are drawn.</summary>
        public bool ShowPreviewLogos { get; set; }

        /// <summary>Container the portal delivers by default.</summary>
        public MagContainer DefaultContainer { get; set; }

        /// <summary>Theme the portal is shown with by default.</summary>
        public MagTheme DefaultTheme { get; set; }

        /// <summary>Longest recording the portal allows, in minutes.</summary>
        public int RecordingMaxLengthMinutes { get; set; }

        /// <summary>How many local recordings a device may keep.</summary>
        public int MaxLocalRecordings { get; set; }

        /// <summary>How many simultaneous viewers one device may have.</summary>
        public int PlaybackLimit { get; set; }

        /// <summary>Set-top-box models allowed to connect.</summary>
        public string[] AllowedStbTypes { get; set; } = [];

        /// <summary>Set-top-box models allowed to record.</summary>
        public string[] AllowedRecordingStbTypes { get; set; } = [];

        /// <summary>Builds the business model out of this row.</summary>
        public MagSettings ToModel() => new()
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
            AllowedStbTypes = AllowedStbTypes,
            AllowedRecordingStbTypes = AllowedRecordingStbTypes
        };

        /// <summary>Copies every editable field of the business model onto this row.</summary>
        /// <remarks>
        /// The identifier and the audit stamps are deliberately not copied. Both lists are copied
        /// into fresh arrays so a later change to the model cannot reach into a tracked row
        /// behind the repository's back.
        /// </remarks>
        /// <param name="model">Model whose values the row must carry.</param>
        public void FromModel(MagSettings model)
        {
            ShowAllCategories = model.ShowAllCategories;
            StrictSecurity = model.StrictSecurity;
            ShowChannelLogos = model.ShowChannelLogos;
            ShowPreviewLogos = model.ShowPreviewLogos;
            DefaultContainer = model.DefaultContainer;
            DefaultTheme = model.DefaultTheme;
            RecordingMaxLengthMinutes = model.RecordingMaxLengthMinutes;
            MaxLocalRecordings = model.MaxLocalRecordings;
            PlaybackLimit = model.PlaybackLimit;
            AllowedStbTypes = model.AllowedStbTypes.ToArray();
            AllowedRecordingStbTypes = model.AllowedRecordingStbTypes.ToArray();
        }
    }
}
