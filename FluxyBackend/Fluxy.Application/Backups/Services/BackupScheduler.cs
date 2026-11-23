using Fluxy.Application.Backups.Utils;
using Fluxy.Core.Backups.Interfaces;
using Fluxy.Core.Backups.Options;
using Fluxy.Core.Backups.ReadModels;
using Fluxy.Core.Settings.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fluxy.Application.Backups.Services
{
    /// <summary>
    /// Takes one backup per configured period bucket while the operator has backups switched on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Concrete, with no Core contract.</b> Nothing calls a hosted service: the host starts it,
    /// and the only surface it has is the one it shares with every other background job. A Core
    /// interface would be a contract with no caller.
    /// </para>
    /// <para>
    /// <b>The rule it enforces is a bucket, not an interval</b> - see
    /// <see cref="BackupDuePolicy"/>. "Is a backup due" is read from the newest file's own name,
    /// so the scheduler holds no state across restarts: a host that was down over midnight takes
    /// today's backup on its first tick, and one that restarts three times takes one.
    /// </para>
    /// <para>
    /// <b>It is inert in two ways.</b> The <c>Testing</c> environment returns before the loop, so
    /// the integration suite's host never takes a dump and never writes into a directory it did
    /// not create. And a disabled <c>BackupEnabled</c> skips the tick, because the setting is the
    /// operator's switch and a scheduler that ignored it would make the switch a lie.
    /// </para>
    /// <para>
    /// <b>Nothing escapes the loop.</b> An exception thrown out of <see cref="ExecuteAsync"/>
    /// stops the host in .NET 6 and later, so each tick is wrapped: a failed dump is logged and
    /// the next tick tries again, which is the only recovery a scheduler can offer.
    /// </para>
    /// </remarks>
    public sealed class BackupScheduler : BackgroundService
    {
        private readonly TimeProvider _time;
        private readonly ISettingsSnapshotProvider _settings;
        private readonly IBackupService _backups;
        private readonly BackupsOptions _options;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<BackupScheduler> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackupScheduler"/> class.
        /// </summary>
        /// <param name="time">Clock the delay and the due decision are measured with.</param>
        /// <param name="settings">Snapshot that says whether backups are on and how often.</param>
        /// <param name="backups">The archives the tick creates.</param>
        /// <param name="options">How long one tick waits.</param>
        /// <param name="environment">Environment that makes the job a no-op under test.</param>
        /// <param name="logger">Logger every tick and every failure is reported to.</param>
        public BackupScheduler(
            TimeProvider time,
            ISettingsSnapshotProvider settings,
            IBackupService backups,
            BackupsOptions options,
            IHostEnvironment environment,
            ILogger<BackupScheduler> logger)
        {
            _time = time;
            _settings = settings;
            _backups = backups;
            _options = options;
            _environment = environment;
            _logger = logger;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_environment.IsEnvironment("Testing"))
            {
                _logger.LogInformation(
                    "The backup scheduler is inert in the Testing environment.");

                return;
            }

            var interval = TimeSpan.FromSeconds(Math.Max(1, _options.TickSeconds));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await DelayAsync(interval, stoppingToken);
                    await TickAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    // Logged and swallowed on purpose: an exception here would stop the host, and
                    // the next tick is the recovery.
                    _logger.LogError(exception, "A backup scheduler tick failed.");
                }
            }
        }

        /// <summary>One due check, and the dump when it says a backup is due.</summary>
        /// <remarks>
        /// The newest archive is read from the directory rather than remembered, so a backup the
        /// operator took by hand through the API also counts as this period's backup - the two
        /// paths share one rule, which is what stops a scheduled dump landing beside a manual one.
        /// </remarks>
        private async Task TickAsync(CancellationToken cancellationToken)
        {
            var settings = _settings.ApplicationSnapshot;

            if (!settings.BackupEnabled)
            {
                return;
            }

            IReadOnlyList<BackupFileInfo> files;

            try
            {
                files = await _backups.ListAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Could not read the backup directory.");

                return;
            }

            var latest = files.Count > 0 ? files[0].CreatedAtUtc : (DateTimeOffset?)null;

            if (!BackupDuePolicy.IsDue(_time.GetUtcNow(), settings.BackupPeriod, latest))
            {
                return;
            }

            var outcome = await _backups.CreateAsync(cancellationToken);

            if (outcome.Succeeded)
            {
                _logger.LogInformation(
                    "The scheduled backup {FileName} was created.",
                    outcome.FileName);
            }
            else
            {
                _logger.LogWarning(
                    "The scheduled backup was not created ({Failure}).",
                    outcome.Failure);
            }
        }

        /// <summary>
        /// Waits one interval through the injected clock, so a test can advance time instead of
        /// sleeping.
        /// </summary>
        private Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken)
            => Task.Delay(interval, _time, cancellationToken);
    }
}
