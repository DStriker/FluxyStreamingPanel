using Fluxy.Core.Settings.Interfaces;
using Fluxy.Core.Settings.ReadModels;
using Fluxy.Core.Settings.Utils;

namespace Fluxy.UnitTests.Settings
{
    /// <summary>
    /// The snapshot provider as a service sees it: two readings that can be replaced, and a
    /// reload that counts how often it was asked for.
    /// </summary>
    /// <remarks>
    /// A fake rather than a mock, the way every other collaborator in these tests is: the tests
    /// that reach a snapshot are not testing the cache, they are testing what a consumer does
    /// with the value it is handed.
    /// </remarks>
    public sealed class FakeSettingsSnapshotProvider : ISettingsSnapshotProvider
    {
        /// <summary>A provider answering the built-in defaults.</summary>
        public FakeSettingsSnapshotProvider()
        {
        }

        /// <summary>A provider answering the readings the caller supplies.</summary>
        /// <param name="application">Application settings the consumer reads.</param>
        /// <param name="mag">MAG settings the consumer reads.</param>
        public FakeSettingsSnapshotProvider(
            ApplicationSettingsSnapshot application,
            MagSettingsSnapshot? mag = null)
        {
            ApplicationSnapshot = application;
            MagSnapshot = mag ?? DefaultMag();
        }

        /// <inheritdoc />
        public ApplicationSettingsSnapshot ApplicationSnapshot { get; set; } = DefaultApplication();

        /// <inheritdoc />
        public MagSettingsSnapshot MagSnapshot { get; set; } = DefaultMag();

        /// <summary>How many times <see cref="ReloadAsync"/> was asked for.</summary>
        public int Reloads { get; private set; }

        /// <inheritdoc />
        public Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            Reloads++;

            return Task.CompletedTask;
        }

        /// <summary>The default application snapshot, built from the Core defaults.</summary>
        public static ApplicationSettingsSnapshot DefaultApplication()
        {
            var settings = ApplicationSettingsDefaults.Create();

            return new ApplicationSettingsSnapshot
            {
                ServiceName = settings.ServiceName,
                DefaultTimeZone = settings.DefaultTimeZone,
                Enigma2BouquetName = settings.Enigma2BouquetName,
                RecaptchaSiteKey = settings.RecaptchaSiteKey,
                RecaptchaSecretKey = settings.RecaptchaSecretKey,
                EnableTrials = settings.EnableTrials,
                PublicApiFloodLimit = settings.PublicApiFloodLimit,
                PublicApiFloodWhitelist = settings.PublicApiFloodWhitelist,
                RequireUserAgent = settings.RequireUserAgent,
                RetentionUserLogs = settings.RetentionUserLogs,
                RetentionCreditLogs = settings.RetentionCreditLogs,
                RetentionConnectionLogs = settings.RetentionConnectionLogs,
                RetentionApiRequestFailures = settings.RetentionApiRequestFailures,
                RetentionStreamProblems = settings.RetentionStreamProblems,
                RetentionMagEvents = settings.RetentionMagEvents,
                BackupEnabled = settings.BackupEnabled,
                BackupPeriod = settings.BackupPeriod,
                BackupRotationCount = settings.BackupRotationCount,
                FallbackStreamDownUrl = settings.FallbackStreamDownUrl,
                FallbackUserBannedUrl = settings.FallbackUserBannedUrl,
                FallbackUserExpiredUrl = settings.FallbackUserExpiredUrl,
                FallbackMaxConnectionsUrl = settings.FallbackMaxConnectionsUrl
            };
        }

        /// <summary>The default MAG snapshot, built from the Core defaults.</summary>
        public static MagSettingsSnapshot DefaultMag()
        {
            var settings = MagSettingsDefaults.Create();

            return new MagSettingsSnapshot
            {
                ShowAllCategories = settings.ShowAllCategories,
                StrictSecurity = settings.StrictSecurity,
                ShowChannelLogos = settings.ShowChannelLogos,
                ShowPreviewLogos = settings.ShowPreviewLogos,
                DefaultContainer = settings.DefaultContainer,
                DefaultTheme = settings.DefaultTheme,
                RecordingMaxLengthMinutes = settings.RecordingMaxLengthMinutes,
                MaxLocalRecordings = settings.MaxLocalRecordings,
                PlaybackLimit = settings.PlaybackLimit,
                AllowedStbTypes = settings.AllowedStbTypes,
                AllowedRecordingStbTypes = settings.AllowedRecordingStbTypes
            };
        }
    }
}
