using Fluxy.Core.Settings.Interfaces;
using Fluxy.Core.Settings.Models;
using Fluxy.Core.Settings.ReadModels;
using Fluxy.Core.Settings.Repositories;
using Fluxy.Core.Settings.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fluxy.Application.Settings.Services
{
    /// <summary>
    /// Serves the installation settings from one cached reading, refreshed on a short window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Singleton, and it never holds a <c>DbContext</c>.</b> The settings are read by services
    /// of every scope - the reCAPTCHA check is the reason - so a scoped service could not answer
    /// them. Each refresh opens its own scope through <see cref="IServiceScopeFactory"/>, reads
    /// the two rows, copies them into immutable snapshots and disposes the scope. Nothing else
    /// from that scope outlives the read.
    /// </para>
    /// <para>
    /// <b>A read never throws.</b> When the reading is older than
    /// <see cref="SettingsLimits.SnapshotTtl"/> the property renews it before answering; a
    /// repository that fails leaves both readings exactly where they were and is reported as a
    /// warning - the settings are needed by flows that must keep working, and a database hiccup is
    /// not a reason to disable reCAPTCHA or start refusing trials.
    /// </para>
    /// <para>
    /// <b>One refresh at a time.</b> <see cref="SemaphoreSlim"/> with a single permit makes
    /// concurrent readers share one read rather than queue behind one each: a reader that finds
    /// another refresh running answers the snapshot that is already loaded instead of waiting for
    /// a second identical query. A write asks for a reload outright, because the operator who just
    /// saved must read back what they saved.
    /// </para>
    /// </remarks>
    public sealed class SettingsSnapshotProvider : ISettingsSnapshotProvider
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeProvider _time;
        private readonly ILogger<SettingsSnapshotProvider> _logger;
        private readonly SemaphoreSlim _refresh = new(1, 1);

        private volatile ApplicationSettingsSnapshot _application = ApplicationFromDefaults();
        private volatile MagSettingsSnapshot _mag = MagFromDefaults();

        /// <summary>
        /// When the readings above were loaded, as UTC ticks. Written under the refresh permit
        /// and read lock-free, which is why it is a <see cref="long"/> rather than the struct.
        /// </summary>
        private long _loadedAtTicks = DateTimeOffset.MinValue.UtcTicks;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsSnapshotProvider"/> class.
        /// </summary>
        /// <param name="scopeFactory">Factory the read opens its own scope through.</param>
        /// <param name="time">Clock the staleness window is measured with.</param>
        /// <param name="logger">Logger a failed refresh is reported to.</param>
        public SettingsSnapshotProvider(
            IServiceScopeFactory scopeFactory,
            TimeProvider time,
            ILogger<SettingsSnapshotProvider> logger)
        {
            _scopeFactory = scopeFactory;
            _time = time;
            _logger = logger;
        }

        /// <inheritdoc />
        public ApplicationSettingsSnapshot ApplicationSnapshot
        {
            get
            {
                RefreshWhenStale();

                var snapshot = _application;

                // The array is copied for the reader: the instance is cached, and a caller that
                // edited the list it was handed would be editing every later answer.
                return snapshot with
                {
                    PublicApiFloodWhitelist = [.. snapshot.PublicApiFloodWhitelist]
                };
            }
        }

        /// <inheritdoc />
        public MagSettingsSnapshot MagSnapshot
        {
            get
            {
                RefreshWhenStale();

                var snapshot = _mag;

                return snapshot with
                {
                    AllowedStbTypes = [.. snapshot.AllowedStbTypes],
                    AllowedRecordingStbTypes = [.. snapshot.AllowedRecordingStbTypes]
                };
            }
        }

        /// <inheritdoc />
        public async Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            await _refresh.WaitAsync(cancellationToken);

            try
            {
                await LoadAsync(cancellationToken);
            }
            finally
            {
                _refresh.Release();
            }
        }

        /// <summary>
        /// The default application snapshot, built from the statics a missing row answers with.
        /// </summary>
        private static ApplicationSettingsSnapshot ApplicationFromDefaults()
            => FromDefaults(ApplicationSettingsDefaults.Create());

        /// <summary>The default MAG snapshot, built from the statics a missing row answers with.</summary>
        private static MagSettingsSnapshot MagFromDefaults()
            => FromDefaults(MagSettingsDefaults.Create());

        private static ApplicationSettingsSnapshot FromDefaults(ApplicationSettings settings) => new()
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

        private static MagSettingsSnapshot FromDefaults(MagSettings settings) => new()
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
            AllowedStbTypes = [.. settings.AllowedStbTypes],
            AllowedRecordingStbTypes = [.. settings.AllowedRecordingStbTypes]
        };

        /// <summary>Whether the cached readings are older than the window.</summary>
        private bool IsStale()
            => _time.GetUtcNow().UtcTicks - Volatile.Read(ref _loadedAtTicks)
                >= SettingsLimits.SnapshotTtl.Ticks;

        /// <summary>
        /// Renews the readings when they are older than the window, and answers either way.
        /// </summary>
        /// <remarks>
        /// The property cannot await, so the renewing read runs synchronously here. A permit that
        /// cannot be taken means another refresh is already running, which is exactly the sharing
        /// the semaphore exists for: this caller answers the snapshot the running refresh is about
        /// to replace, and the one afterwards sees the new values.
        /// </remarks>
        private void RefreshWhenStale()
        {
            if (!IsStale())
            {
                return;
            }

            if (!_refresh.Wait(0))
            {
                return;
            }

            try
            {
                LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            finally
            {
                _refresh.Release();
            }
        }

        /// <summary>
        /// Reads both rows and replaces the cached readings, or leaves them untouched when the
        /// read failed.
        /// </summary>
        /// <remarks>
        /// Called with the refresh permit held, or from the startup seeder before any request is
        /// served. The two rows are read together: half a refresh would pair an operator's new
        /// application settings with yesterday's MAG settings.
        /// </remarks>
        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            ApplicationSettings? application;
            MagSettings? mag;

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();

                var applications = scope.ServiceProvider
                    .GetRequiredService<IApplicationSettingsRepository>();
                var mags = scope.ServiceProvider.GetRequiredService<IMagSettingsRepository>();

                application = await applications.GetAsync(cancellationToken);
                mag = await mags.GetAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Could not read the installation settings. The previous values stay in use " +
                    "until the next attempt.");

                return;
            }

            _application = FromDefaults(
                application ?? ApplicationSettingsDefaults.Create());
            _mag = FromDefaults(mag ?? MagSettingsDefaults.Create());

            Volatile.Write(ref _loadedAtTicks, _time.GetUtcNow().UtcTicks);
        }
    }
}
