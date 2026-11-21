using Fluxy.Application.Settings.Utils;
using Fluxy.Core.Settings.Commands;
using Fluxy.Core.Settings.Enums;
using Fluxy.Core.Settings.Utils;
using Fluxy.Core.Subscriptions.Utils;

namespace Fluxy.UnitTests.Settings
{
    /// <summary>
    /// The MAG settings rules: the two vocabularies, every numeric bound, and the two model lists
    /// that must name at least one known model.
    /// </summary>
    public sealed class MagSettingsRulesTests
    {
        [Fact]
        public void TheDefaultsAllPassTheRulesOfTheirOwnRow()
        {
            Assert.Empty(MagSettingsRules.Validate(Patch()));
        }

        [Fact]
        public void TheDefaultsNameEveryModelAndTheThreeThatMayRecord()
        {
            var settings = MagSettingsDefaults.Create();

            Assert.Equal(StbTypeCatalog.All, settings.AllowedStbTypes);
            Assert.Equal(["MAG255", "MAG256", "MAG257"], settings.AllowedRecordingStbTypes);
        }

        [Theory]
        [InlineData("mpeg-ts")]
        [InlineData("mpegts")]
        [InlineData("HLS")]
        public void EveryContainerNameIsAccepted(string value)
        {
            Assert.Empty(MagSettingsRules.Validate(Patch(defaultContainer: value)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("rtmp")]
        public void AContainerOutsideTheVocabularyIsRefused(string? value)
        {
            Assert.Contains(
                nameof(MagSettingsPatch.DefaultContainer),
                MagSettingsRules.Validate(Patch(defaultContainer: value)).Keys);
        }

        [Theory]
        [InlineData("default")]
        [InlineData("digital")]
        [InlineData("emerald")]
        [InlineData("cappucino")]
        [InlineData("Ocean Blue")]
        public void EveryThemeNameIsAccepted(string value)
        {
            Assert.Empty(MagSettingsRules.Validate(Patch(defaultTheme: value)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("cappuccino")]
        public void AThemeOutsideTheVocabularyIsRefused(string? value)
        {
            Assert.Contains(
                nameof(MagSettingsPatch.DefaultTheme),
                MagSettingsRules.Validate(Patch(defaultTheme: value)).Keys);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(1440)]
        public void TheRecordingLengthAcceptsItsWholeRange(int minutes)
        {
            Assert.Empty(MagSettingsRules.Validate(
                Patch(recordingMaxLengthMinutes: minutes)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1441)]
        public void TheRecordingLengthRefusesAValueOutsideItsRange(int minutes)
        {
            Assert.Contains(
                nameof(MagSettingsPatch.RecordingMaxLengthMinutes),
                MagSettingsRules.Validate(Patch(recordingMaxLengthMinutes: minutes)).Keys);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        public void TheLocalRecordingCountAcceptsItsWholeRange(int value)
        {
            Assert.Empty(MagSettingsRules.Validate(Patch(maxLocalRecordings: value)));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(1001)]
        public void TheLocalRecordingCountRefusesAValueOutsideItsRange(int value)
        {
            Assert.Contains(
                nameof(MagSettingsPatch.MaxLocalRecordings),
                MagSettingsRules.Validate(Patch(maxLocalRecordings: value)).Keys);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(SubscriptionLimits.ConnectionsMax)]
        public void ThePlaybackLimitAcceptsItsWholeRange(int value)
        {
            Assert.Empty(MagSettingsRules.Validate(Patch(playbackLimit: value)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(SubscriptionLimits.ConnectionsMax + 1)]
        public void ThePlaybackLimitRefusesAValueOutsideItsRange(int value)
        {
            Assert.Contains(
                nameof(MagSettingsPatch.PlaybackLimit),
                MagSettingsRules.Validate(Patch(playbackLimit: value)).Keys);
        }

        [Fact]
        public void AnEmptyModelListIsRefused()
        {
            var errors = MagSettingsRules.Validate(
                Patch(allowedStbTypes: [], allowedRecordingStbTypes: []));

            Assert.Contains(nameof(MagSettingsPatch.AllowedStbTypes), errors.Keys);
            Assert.Contains(nameof(MagSettingsPatch.AllowedRecordingStbTypes), errors.Keys);
        }

        [Fact]
        public void AnUnknownModelIsRefusedRatherThanDropped()
        {
            Assert.Contains(
                nameof(MagSettingsPatch.AllowedStbTypes),
                MagSettingsRules.Validate(Patch(allowedStbTypes: ["MAG255", "MAG999"])).Keys);

            Assert.Contains(
                nameof(MagSettingsPatch.AllowedRecordingStbTypes),
                MagSettingsRules.Validate(Patch(allowedRecordingStbTypes: ["Commodore 64"])).Keys);
        }

        [Fact]
        public void AListOfOneKnownModelIsAccepted()
        {
            Assert.Empty(MagSettingsRules.Validate(
                Patch(allowedStbTypes: ["mag255"], allowedRecordingStbTypes: ["Aura HD"])));
        }

        [Fact]
        public void NormalizeCanonicalizesBothModelLists()
        {
            var settings = MagSettingsRules.Normalize(
                Patch(
                    defaultContainer: "mpegts",
                    defaultTheme: "OCEAN BLUE",
                    allowedStbTypes: ["mag255", "MAG255", "wR320"],
                    allowedRecordingStbTypes: ["mag256"]),
                MagSettingsDefaults.Id);

            Assert.Equal(MagContainer.MpegTs, settings.DefaultContainer);
            Assert.Equal(MagTheme.OceanBlue, settings.DefaultTheme);
            Assert.Equal(["MAG255", "WR320"], settings.AllowedStbTypes);
            Assert.Equal(["MAG256"], settings.AllowedRecordingStbTypes);
            Assert.Equal(MagSettingsDefaults.Id, settings.Id);
        }

        /// <summary>A patch that passes every rule, with the named values replaced.</summary>
        private static MagSettingsPatch Patch(
            string? defaultContainer = "mpeg-ts",
            string? defaultTheme = "default",
            int? recordingMaxLengthMinutes = null,
            int? maxLocalRecordings = null,
            int? playbackLimit = null,
            IReadOnlyList<string>? allowedStbTypes = null,
            IReadOnlyList<string>? allowedRecordingStbTypes = null)
            => new()
            {
                ShowAllCategories = MagSettingsDefaults.ShowAllCategories,
                StrictSecurity = MagSettingsDefaults.StrictSecurity,
                ShowChannelLogos = MagSettingsDefaults.ShowChannelLogos,
                ShowPreviewLogos = MagSettingsDefaults.ShowPreviewLogos,
                DefaultContainer = defaultContainer,
                DefaultTheme = defaultTheme,
                RecordingMaxLengthMinutes =
                    recordingMaxLengthMinutes ?? MagSettingsDefaults.RecordingMaxLengthMinutes,
                MaxLocalRecordings = maxLocalRecordings ?? MagSettingsDefaults.MaxLocalRecordings,
                PlaybackLimit = playbackLimit ?? MagSettingsDefaults.PlaybackLimit,
                AllowedStbTypes = allowedStbTypes ?? [.. StbTypeCatalog.All],
                AllowedRecordingStbTypes = allowedRecordingStbTypes
                    ?? [.. MagSettingsDefaults.AllowedRecordingStbTypes]
            };
    }
}
