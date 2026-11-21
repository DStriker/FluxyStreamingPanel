using Fluxy.Application.Settings.Utils;
using Fluxy.Core.Settings.Commands;
using Fluxy.Core.Settings.Enums;
using Fluxy.Core.Settings.Utils;

namespace Fluxy.UnitTests.Settings
{
    /// <summary>
    /// The application settings rules: every bound at its edge and one past it, the time zone the
    /// platform actually resolves, the whitelist's canonical form and the write-only key.
    /// </summary>
    public sealed class SettingsRulesTests
    {
        [Fact]
        public void EuropeBerlinResolvesOnThisPlatform()
        {
            // The default the row is seeded with. If this platform could not resolve it, a fresh
            // installation would refuse the value it was born with.
            Assert.True(
                Fluxy.Application.Users.Utils.TimeZonePolicy.TryNormalize("Europe/Berlin", out var zone));
            Assert.Equal("Europe/Berlin", zone);
        }

        [Fact]
        public void TheDefaultsAllPassTheRulesOfTheirOwnRow()
        {
            var errors = SettingsRules.Validate(Patch());

            Assert.Empty(errors);
        }

        [Fact]
        public void ServiceNameIsTrimmedAndMeasuredAfterTrimming()
        {
            Assert.Empty(SettingsRules.Validate(Patch(serviceName: "  Fluxy  ")));

            Assert.Contains(
                nameof(ApplicationSettingsPatch.ServiceName),
                SettingsRules.Validate(Patch(serviceName: "   ")).Keys);

            Assert.Contains(
                nameof(ApplicationSettingsPatch.ServiceName),
                SettingsRules.Validate(
                    Patch(serviceName: new string('a', SettingsLimits.ServiceNameMaxLength + 1))).Keys);

            Assert.Empty(SettingsRules.Validate(
                Patch(serviceName: new string('a', SettingsLimits.ServiceNameMaxLength))));
        }

        [Fact]
        public void Enigma2BouquetNameIsTrimmedAndMeasuredAfterTrimming()
        {
            Assert.Empty(SettingsRules.Validate(Patch(enigma2BouquetName: "  Fluxy IPTV  ")));

            Assert.Contains(
                nameof(ApplicationSettingsPatch.Enigma2BouquetName),
                SettingsRules.Validate(Patch(enigma2BouquetName: "  ")).Keys);

            Assert.Contains(
                nameof(ApplicationSettingsPatch.Enigma2BouquetName),
                SettingsRules.Validate(Patch(
                    enigma2BouquetName:
                        new string('a', SettingsLimits.Enigma2BouquetNameMaxLength + 1))).Keys);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Mars/OlympusMons")]
        public void AnUnusableTimeZoneIsRefused(string value)
        {
            Assert.Contains(
                nameof(ApplicationSettingsPatch.DefaultTimeZone),
                SettingsRules.Validate(Patch(defaultTimeZone: value)).Keys);
        }

        [Fact]
        public void ATooLongTimeZoneIsRefusedBeforeThePlatformIsAsked()
        {
            Assert.Contains(
                nameof(ApplicationSettingsPatch.DefaultTimeZone),
                SettingsRules.Validate(
                    Patch(defaultTimeZone: new string('a', SettingsLimits.DefaultTimeZoneMaxLength + 1)))
                    .Keys);
        }

        [Fact]
        public void TheRecaptchaKeysAllowEmptyAndRefuseOnlyALengthPastTheColumn()
        {
            Assert.Empty(SettingsRules.Validate(Patch(recaptchaSiteKey: "", recaptchaSecretKey: "")));

            Assert.Empty(SettingsRules.Validate(Patch(
                recaptchaSiteKey: new string('a', SettingsLimits.RecaptchaKeyMaxLength),
                recaptchaSecretKey: new string('b', SettingsLimits.RecaptchaKeyMaxLength))));

            var errors = SettingsRules.Validate(Patch(
                recaptchaSiteKey: new string('a', SettingsLimits.RecaptchaKeyMaxLength + 1),
                recaptchaSecretKey: new string('b', SettingsLimits.RecaptchaKeyMaxLength + 1)));

            Assert.Contains(nameof(ApplicationSettingsPatch.RecaptchaSiteKey), errors.Keys);
            Assert.Contains(nameof(ApplicationSettingsPatch.RecaptchaSecretKey), errors.Keys);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10000)]
        public void TheFloodLimitAcceptsItsWholeDocumentedRange(int value)
        {
            Assert.Empty(SettingsRules.Validate(Patch(publicApiFloodLimit: value)));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(10001)]
        public void TheFloodLimitRefusesAValueOutsideItsRange(int value)
        {
            Assert.Contains(
                nameof(ApplicationSettingsPatch.PublicApiFloodLimit),
                SettingsRules.Validate(Patch(publicApiFloodLimit: value)).Keys);
        }

        [Fact]
        public void TheWhitelistIsCanonicalizedAndDeDuplicated()
        {
            var normalized = SettingsRules.NormalizeWhitelist(
                [" 203.0.113.7 ", "203.0.113.7", "2001:db8::1", "10.0.0.0/8", "10.0.0.0/8"]);

            Assert.Equal(["203.0.113.7", "2001:db8::1", "10.0.0.0/8"], normalized);
        }

        [Theory]
        [InlineData("not-an-address")]
        [InlineData("10.0.0.0/33")]
        [InlineData("")]
        [InlineData("   ")]
        public void AWhitelistEntryThatIsNoAddressIsRefusedUnderTheList(string value)
        {
            Assert.Contains(
                nameof(ApplicationSettingsPatch.PublicApiFloodWhitelist),
                SettingsRules.Validate(Patch(publicApiFloodWhitelist: [value])).Keys);
        }

        [Fact]
        public void TheWhitelistCapIsOneHundredDistinctAddresses()
        {
            var atCap = Enumerable.Range(0, SettingsLimits.WhitelistMaxEntries)
                .Select(index => $"10.0.{index / 256}.{index % 256}")
                .ToArray();

            var overCap = Enumerable.Range(0, SettingsLimits.WhitelistMaxEntries + 1)
                .Select(index => $"10.0.{index / 256}.{index % 256}")
                .ToArray();

            Assert.Empty(SettingsRules.Validate(Patch(publicApiFloodWhitelist: atCap)));

            Assert.Contains(
                nameof(ApplicationSettingsPatch.PublicApiFloodWhitelist),
                SettingsRules.Validate(Patch(publicApiFloodWhitelist: overCap)).Keys);
        }

        [Fact]
        public void ARepeatedAddressDoesNotCountTwiceAgainstTheCap()
        {
            var repeated = Enumerable.Repeat("203.0.113.7", SettingsLimits.WhitelistMaxEntries + 5)
                .ToArray();

            Assert.Empty(SettingsRules.Validate(Patch(publicApiFloodWhitelist: repeated)));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3650)]
        public void EveryRetentionAcceptsItsWholeRange(int days)
        {
            Assert.Empty(SettingsRules.Validate(Patch(
                retentionUserLogs: days,
                retentionCreditLogs: days,
                retentionConnectionLogs: days,
                retentionApiRequestFailures: days,
                retentionStreamProblems: days,
                retentionMagEvents: days)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3651)]
        public void EveryRetentionRefusesAValueOutsideItsRange(int days)
        {
            var errors = SettingsRules.Validate(Patch(
                retentionUserLogs: days,
                retentionCreditLogs: days,
                retentionConnectionLogs: days,
                retentionApiRequestFailures: days,
                retentionStreamProblems: days,
                retentionMagEvents: days));

            Assert.Contains(nameof(ApplicationSettingsPatch.RetentionUserLogs), errors.Keys);
            Assert.Contains(nameof(ApplicationSettingsPatch.RetentionCreditLogs), errors.Keys);
            Assert.Contains(nameof(ApplicationSettingsPatch.RetentionConnectionLogs), errors.Keys);
            Assert.Contains(nameof(ApplicationSettingsPatch.RetentionApiRequestFailures), errors.Keys);
            Assert.Contains(nameof(ApplicationSettingsPatch.RetentionStreamProblems), errors.Keys);
            Assert.Contains(nameof(ApplicationSettingsPatch.RetentionMagEvents), errors.Keys);
        }

        [Theory]
        [InlineData("hourly", BackupPeriod.Hourly)]
        [InlineData("daily", BackupPeriod.Daily)]
        [InlineData("weekly", BackupPeriod.Weekly)]
        [InlineData("MONTHLY", BackupPeriod.Monthly)]
        public void EveryBackupPeriodParses(string value, BackupPeriod expected)
        {
            Assert.Equal(expected, SettingsRules.ParseBackupPeriod(value));
            Assert.Empty(SettingsRules.Validate(Patch(backupPeriod: value)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("yearly")]
        public void ABackupPeriodOutsideTheVocabularyIsRefused(string? value)
        {
            Assert.Contains(
                nameof(ApplicationSettingsPatch.BackupPeriod),
                SettingsRules.Validate(Patch(backupPeriod: value)).Keys);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        public void TheRotationCountAcceptsItsWholeRange(int value)
        {
            Assert.Empty(SettingsRules.Validate(Patch(backupRotationCount: value)));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(1001)]
        public void TheRotationCountRefusesAValueOutsideItsRange(int value)
        {
            Assert.Contains(
                nameof(ApplicationSettingsPatch.BackupRotationCount),
                SettingsRules.Validate(Patch(backupRotationCount: value)).Keys);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("https://example.test/down")]
        [InlineData("http://example.test:8080/expired?x=1")]
        public void AFallbackAddressAcceptsEmptyAndAbsoluteHttpAddresses(string value)
        {
            Assert.Empty(SettingsRules.Validate(Patch(
                fallbackStreamDownUrl: value,
                fallbackUserBannedUrl: value,
                fallbackUserExpiredUrl: value,
                fallbackMaxConnectionsUrl: value)));
        }

        [Theory]
        [InlineData("example.test/down")]
        [InlineData("/relative")]
        [InlineData("ftp://example.test/down")]
        [InlineData("javascript:alert(1)")]
        public void AFallbackAddressRefusesAnythingButAnAbsoluteHttpScheme(string value)
        {
            var errors = SettingsRules.Validate(Patch(fallbackStreamDownUrl: value));

            Assert.Contains(nameof(ApplicationSettingsPatch.FallbackStreamDownUrl), errors.Keys);
        }

        [Fact]
        public void AFallbackAddressRefusesAValuePastItsColumn()
        {
            var value = "https://example.test/" +
                new string('a', SettingsLimits.FallbackUrlMaxLength);

            Assert.Contains(
                nameof(ApplicationSettingsPatch.FallbackUserExpiredUrl),
                SettingsRules.Validate(Patch(fallbackUserExpiredUrl: value)).Keys);
        }

        [Fact]
        public void NormalizeTrimsTheTextsAndKeepsTheSuppliedSecret()
        {
            var settings = SettingsRules.Normalize(
                Patch(serviceName: "  Named  ", recaptchaSiteKey: "  site  "),
                ApplicationSettingsDefaults.Id,
                "kept-secret");

            Assert.Equal("Named", settings.ServiceName);
            Assert.Equal("site", settings.RecaptchaSiteKey);
            Assert.Equal("kept-secret", settings.RecaptchaSecretKey);
            Assert.Equal(ApplicationSettingsDefaults.Id, settings.Id);
            Assert.Empty(settings.PublicApiFloodWhitelist);
        }

        /// <summary>A patch that passes every rule, with the named values replaced.</summary>
        private static ApplicationSettingsPatch Patch(
            string? serviceName = null,
            string? defaultTimeZone = null,
            string? enigma2BouquetName = null,
            string? recaptchaSiteKey = null,
            string? recaptchaSecretKey = null,
            int? publicApiFloodLimit = null,
            IReadOnlyList<string>? publicApiFloodWhitelist = null,
            int? retentionUserLogs = null,
            int? retentionCreditLogs = null,
            int? retentionConnectionLogs = null,
            int? retentionApiRequestFailures = null,
            int? retentionStreamProblems = null,
            int? retentionMagEvents = null,
            string? backupPeriod = "daily",
            int? backupRotationCount = null,
            string? fallbackStreamDownUrl = null,
            string? fallbackUserBannedUrl = null,
            string? fallbackUserExpiredUrl = null,
            string? fallbackMaxConnectionsUrl = null)
            => new()
            {
                ServiceName = serviceName ?? ApplicationSettingsDefaults.ServiceName,
                DefaultTimeZone = defaultTimeZone ?? ApplicationSettingsDefaults.DefaultTimeZone,
                Enigma2BouquetName =
                    enigma2BouquetName ?? ApplicationSettingsDefaults.Enigma2BouquetName,
                RecaptchaSiteKey = recaptchaSiteKey ?? ApplicationSettingsDefaults.RecaptchaSiteKey,
                RecaptchaSecretKey = recaptchaSecretKey,
                EnableTrials = ApplicationSettingsDefaults.EnableTrials,
                PublicApiFloodLimit =
                    publicApiFloodLimit ?? ApplicationSettingsDefaults.PublicApiFloodLimit,
                PublicApiFloodWhitelist = publicApiFloodWhitelist ?? [],
                RequireUserAgent = ApplicationSettingsDefaults.RequireUserAgent,
                RetentionUserLogs = retentionUserLogs ?? ApplicationSettingsDefaults.RetentionUserLogs,
                RetentionCreditLogs =
                    retentionCreditLogs ?? ApplicationSettingsDefaults.RetentionCreditLogs,
                RetentionConnectionLogs =
                    retentionConnectionLogs ?? ApplicationSettingsDefaults.RetentionConnectionLogs,
                RetentionApiRequestFailures = retentionApiRequestFailures
                    ?? ApplicationSettingsDefaults.RetentionApiRequestFailures,
                RetentionStreamProblems =
                    retentionStreamProblems ?? ApplicationSettingsDefaults.RetentionStreamProblems,
                RetentionMagEvents = retentionMagEvents ?? ApplicationSettingsDefaults.RetentionMagEvents,
                BackupEnabled = ApplicationSettingsDefaults.BackupEnabled,
                BackupPeriod = backupPeriod,
                BackupRotationCount =
                    backupRotationCount ?? ApplicationSettingsDefaults.BackupRotationCount,
                FallbackStreamDownUrl =
                    fallbackStreamDownUrl ?? ApplicationSettingsDefaults.FallbackStreamDownUrl,
                FallbackUserBannedUrl =
                    fallbackUserBannedUrl ?? ApplicationSettingsDefaults.FallbackUserBannedUrl,
                FallbackUserExpiredUrl =
                    fallbackUserExpiredUrl ?? ApplicationSettingsDefaults.FallbackUserExpiredUrl,
                FallbackMaxConnectionsUrl =
                    fallbackMaxConnectionsUrl ?? ApplicationSettingsDefaults.FallbackMaxConnectionsUrl
            };
    }
}
