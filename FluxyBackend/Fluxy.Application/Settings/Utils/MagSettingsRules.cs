using Fluxy.Core.Settings.Commands;
using Fluxy.Core.Settings.Models;
using Fluxy.Core.Settings.Utils;
using Fluxy.Core.Subscriptions.Utils;

namespace Fluxy.Application.Settings.Utils
{
    /// <summary>
    /// The rules and sentences every write to the MAG settings shares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two container and theme fields are text on the way in because the wire vocabulary is
    /// the owner's, not the enum member names. Validation refuses a value that names no member;
    /// normalisation is called only once nothing was refused, so it can assume every parse
    /// succeeds.
    /// </para>
    /// <para>
    /// <b>Both model lists must name at least one entry.</b> An empty list is not "no
    /// restriction" here - it is a portal nobody could connect to - and a model outside
    /// <see cref="StbTypeCatalog"/> is refused rather than dropped, because a client that sent a
    /// name believes it asked for it.
    /// </para>
    /// <para>
    /// The keys of the refused map are the C# property names of
    /// <see cref="MagSettingsPatch"/>; the transport rewrites them to camelCase.
    /// </para>
    /// </remarks>
    public static class MagSettingsRules
    {
        /// <summary>The sentence a container that names no member gets.</summary>
        public static readonly string[] DefaultContainerRule = [MagContainerCatalog.Hint];

        /// <summary>The sentence a theme that names no member gets.</summary>
        public static readonly string[] DefaultThemeRule = [MagThemeCatalog.Hint];

        /// <summary>The sentence a recording length outside its range gets.</summary>
        public static readonly string[] RecordingMaxLengthRule =
        [
            "The recording length must be between " +
                SettingsLimits.RecordingMaxLengthMinutesMin + " and " +
                SettingsLimits.RecordingMaxLengthMinutesMax + " minutes."
        ];

        /// <summary>The sentence a local recording count outside its range gets.</summary>
        public static readonly string[] MaxLocalRecordingsRule =
        [
            "The local recording count must be between " +
                SettingsLimits.MaxLocalRecordingsMin + " and " +
                SettingsLimits.MaxLocalRecordingsMax + "; 0 allows none."
        ];

        /// <summary>The sentence a playback limit outside its range gets.</summary>
        public static readonly string[] PlaybackLimitRule =
        [
            "The playback limit must be between " + SettingsLimits.PlaybackLimitMin + " and " +
                SubscriptionLimits.ConnectionsMax + "."
        ];

        /// <summary>The sentence a model list that is empty or names an unknown model gets.</summary>
        public static readonly string[] StbTypesRule =
        [
            "The list must name at least one set-top-box model, and every model must be one of " +
                "the " + StbTypeCatalog.All.Count + " known ones."
        ];

        /// <summary>
        /// Checks every value of the patch that can be judged without the database.
        /// </summary>
        /// <param name="patch">Values as they arrived.</param>
        /// <returns>The rejected fields, empty when the row may be stored.</returns>
        public static Dictionary<string, string[]> Validate(MagSettingsPatch patch)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (MagContainerCatalog.Parse(patch.DefaultContainer) is null)
            {
                errors[nameof(MagSettingsPatch.DefaultContainer)] = DefaultContainerRule;
            }

            if (MagThemeCatalog.Parse(patch.DefaultTheme) is null)
            {
                errors[nameof(MagSettingsPatch.DefaultTheme)] = DefaultThemeRule;
            }

            if (patch.RecordingMaxLengthMinutes
                is < SettingsLimits.RecordingMaxLengthMinutesMin
                or > SettingsLimits.RecordingMaxLengthMinutesMax)
            {
                errors[nameof(MagSettingsPatch.RecordingMaxLengthMinutes)] =
                    RecordingMaxLengthRule;
            }

            if (patch.MaxLocalRecordings
                is < SettingsLimits.MaxLocalRecordingsMin or > SettingsLimits.MaxLocalRecordingsMax)
            {
                errors[nameof(MagSettingsPatch.MaxLocalRecordings)] = MaxLocalRecordingsRule;
            }

            if (patch.PlaybackLimit is < SettingsLimits.PlaybackLimitMin
                or > SettingsLimits.PlaybackLimitMax)
            {
                errors[nameof(MagSettingsPatch.PlaybackLimit)] = PlaybackLimitRule;
            }

            CheckStbTypes(
                errors,
                nameof(MagSettingsPatch.AllowedStbTypes),
                patch.AllowedStbTypes);

            CheckStbTypes(
                errors,
                nameof(MagSettingsPatch.AllowedRecordingStbTypes),
                patch.AllowedRecordingStbTypes);

            return errors;
        }

        /// <summary>
        /// Builds the row a validated patch describes.
        /// </summary>
        /// <param name="patch">Values that already passed validation.</param>
        /// <param name="id">Identifier of the row.</param>
        /// <returns>The values as they are about to be stored.</returns>
        public static MagSettings Normalize(MagSettingsPatch patch, Guid id) => new()
        {
            Id = id,
            ShowAllCategories = patch.ShowAllCategories,
            StrictSecurity = patch.StrictSecurity,
            ShowChannelLogos = patch.ShowChannelLogos,
            ShowPreviewLogos = patch.ShowPreviewLogos,
            DefaultContainer = MagContainerCatalog.Parse(patch.DefaultContainer)!.Value,
            DefaultTheme = MagThemeCatalog.Parse(patch.DefaultTheme)!.Value,
            RecordingMaxLengthMinutes = patch.RecordingMaxLengthMinutes,
            MaxLocalRecordings = patch.MaxLocalRecordings,
            PlaybackLimit = patch.PlaybackLimit,
            AllowedStbTypes = StbTypeCatalog.Normalize(patch.AllowedStbTypes),
            AllowedRecordingStbTypes = StbTypeCatalog.Normalize(patch.AllowedRecordingStbTypes)
        };

        private static void CheckStbTypes(
            Dictionary<string, string[]> errors,
            string field,
            IReadOnlyList<string>? values)
        {
            if (values is null || values.Count == 0)
            {
                errors[field] = StbTypesRule;

                return;
            }

            foreach (var value in values)
            {
                if (!StbTypeCatalog.TryCanonicalize(value, out _))
                {
                    errors[field] = StbTypesRule;

                    return;
                }
            }
        }
    }
}
