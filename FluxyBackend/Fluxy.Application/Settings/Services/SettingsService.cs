using Fluxy.Application.Settings.Utils;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Settings.Commands;
using Fluxy.Core.Settings.Interfaces;
using Fluxy.Core.Settings.Models;
using Fluxy.Core.Settings.Outcomes;
using Fluxy.Core.Settings.ReadModels;
using Fluxy.Core.Settings.Repositories;
using Fluxy.Core.Settings.Utils;

namespace Fluxy.Application.Settings.Services
{
    /// <summary>
    /// Reads the application settings and writes them on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The row is created by the migration and the startup seeder, so a write is always an update
    /// of a row that exists; a row that is somehow absent is inserted with the same values rather
    /// than answered as a failure, because the operator's save is the same intent either way.
    /// </para>
    /// <para>
    /// <b>The private key is write-only.</b> The patch's <c>RecaptchaSecretKey</c> absent or
    /// empty keeps whatever the row already holds; a non-empty value replaces it. That is why the
    /// validation of a blank secret passes and why the response answers
    /// <c>recaptchaConfigured</c> instead of the value itself.
    /// </para>
    /// <para>
    /// <b>The snapshot is refreshed after the save, before the response is built.</b> A
    /// concurrent reader of another process sees the change within the TTL; the process that
    /// served the write sees it at once.
    /// </para>
    /// </remarks>
    public sealed class SettingsService : ISettingsService
    {
        private readonly IApplicationSettingsRepository _settings;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISettingsSnapshotProvider _snapshots;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsService"/> class.
        /// </summary>
        /// <param name="settings">Repository holding the single settings row.</param>
        /// <param name="unitOfWork">Commits the change the operation stages.</param>
        /// <param name="snapshots">Cache the change is published to after the save.</param>
        public SettingsService(
            IApplicationSettingsRepository settings,
            IUnitOfWork unitOfWork,
            ISettingsSnapshotProvider snapshots)
        {
            _settings = settings;
            _unitOfWork = unitOfWork;
            _snapshots = snapshots;
        }

        /// <inheritdoc />
        public async Task<ApplicationSettingsSnapshot> GetAsync(
            CancellationToken cancellationToken = default)
        {
            var settings = await _settings.GetAsync(cancellationToken);

            return ToSnapshot(settings ?? ApplicationSettingsDefaults.Create());
        }

        /// <inheritdoc />
        public async Task<SettingsOutcome> UpdateAsync(
            ApplicationSettingsPatch patch,
            CancellationToken cancellationToken = default)
        {
            var errors = SettingsRules.Validate(patch);

            if (errors.Count > 0)
            {
                return new SettingsOutcome
                {
                    Action = SettingsAction.Invalid,
                    Errors = errors
                };
            }

            var stored = await _settings.GetAsync(cancellationToken);
            var id = stored?.Id ?? ApplicationSettingsDefaults.Id;

            // Absent or empty keeps what the row holds; a value replaces it. The trim happens
            // here so a secret of spaces is the "keep" it reads as rather than a stored blank.
            var requested = patch.RecaptchaSecretKey?.Trim();
            var secret = string.IsNullOrEmpty(requested)
                ? stored?.RecaptchaSecretKey ?? ApplicationSettingsDefaults.RecaptchaSecretKey
                : requested;

            var settings = SettingsRules.Normalize(patch, id, secret);

            if (stored is null)
            {
                await _settings.AddAsync(settings, cancellationToken);
            }
            else if (!await _settings.UpdateAsync(settings, cancellationToken))
            {
                // The row vanished between the read and the write. Re-staging it as an insert is
                // the truthful repair: the caller asked for these values to be the settings.
                await _settings.AddAsync(settings, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _snapshots.ReloadAsync(cancellationToken);

            return new SettingsOutcome
            {
                Action = SettingsAction.Updated
            };
        }

        /// <summary>Turns the stored model into the snapshot other callers read.</summary>
        private static ApplicationSettingsSnapshot ToSnapshot(ApplicationSettings settings) => new()
        {
            ServiceName = settings.ServiceName,
            DefaultTimeZone = settings.DefaultTimeZone,
            Enigma2BouquetName = settings.Enigma2BouquetName,
            RecaptchaSiteKey = settings.RecaptchaSiteKey,
            RecaptchaSecretKey = settings.RecaptchaSecretKey,
            EnableTrials = settings.EnableTrials,
            PublicApiFloodLimit = settings.PublicApiFloodLimit,
            PublicApiFloodWhitelist = [.. settings.PublicApiFloodWhitelist],
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
}
