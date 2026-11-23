using Fluxy.Application.Backups.Utils;
using Fluxy.Core.Backups.Enums;
using Fluxy.Core.Backups.Interfaces;
using Fluxy.Core.Backups.Options;
using Fluxy.Core.Backups.Outcomes;
using Fluxy.Core.Backups.ReadModels;
using Fluxy.Core.Backups.Utils;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Outcomes;
using Fluxy.Core.Settings.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fluxy.Application.Backups.Services
{
    /// <summary>
    /// The archives on disk: their names, their creation, their removal and the one restore.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Singleton with a single permit, and that permit is the single-flight gate.</b> Three
    /// writes exist and any two of them at once would be wrong in a way no lock inside the
    /// database could repair: a second dump beside a restore captures a half-restored schema, and
    /// two restores would interleave two archives. Each write asks for the permit without waiting
    /// and answers <see cref="BackupFailure.Busy"/> when it cannot have it, because "try again in
    /// a moment" is the honest answer to an operator who pressed twice.
    /// </para>
    /// <para>
    /// <b>It holds no <c>DbContext</c>.</b> The two things it needs from the container - the
    /// installer that runs the migration after a restore, and the settings snapshot that says how
    /// many archives to keep - are resolved from their own scope per operation.
    /// </para>
    /// <para>
    /// <b>A dump is durable before anything else happens.</b> The tool writes
    /// <c>&lt;finalName&gt;.tmp</c>, the service renames it only after the tool has exited zero,
    /// and only then does rotation consider the directory. A failed run leaves no
    /// pattern-matching file behind, so a crash in the middle never looks like a backup.
    /// </para>
    /// </remarks>
    public sealed class BackupService : IBackupService
    {
        private readonly BackupsOptions _options;
        private readonly IDatabaseBackupTool _tool;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeProvider _time;
        private readonly ILogger<BackupService> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="BackupService"/> class.
        /// </summary>
        /// <param name="options">Container and directory the archives are written in.</param>
        /// <param name="tool">The external dump/restore mechanism.</param>
        /// <param name="scopeFactory">Factory the migration and the settings read open a scope through.</param>
        /// <param name="time">Clock the archive name is stamped with.</param>
        /// <param name="logger">Logger the outcomes are reported to.</param>
        public BackupService(
            BackupsOptions options,
            IDatabaseBackupTool tool,
            IServiceScopeFactory scopeFactory,
            TimeProvider time,
            ILogger<BackupService> logger)
        {
            _options = options;
            _tool = tool;
            _scopeFactory = scopeFactory;
            _time = time;
            _logger = logger;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<BackupFileInfo>> ListAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var directory = _options.Directory;

            if (!Directory.Exists(directory))
            {
                return Task.FromResult<IReadOnlyList<BackupFileInfo>>([]);
            }

            var files = new DirectoryInfo(directory)
                .EnumerateFiles("*" + BackupFileName.Extension, SearchOption.TopDirectoryOnly)
                .Select(file => BackupFileName.TryParse(file.Name, out var utc)
                    ? new BackupFileInfo(file.Name, file.Length, utc)
                    : null)
                .OfType<BackupFileInfo>()
                .OrderByDescending(file => file.FileName, StringComparer.Ordinal)
                .ToList();

            return Task.FromResult<IReadOnlyList<BackupFileInfo>>(files);
        }

        /// <inheritdoc />
        public async Task<BackupOutcome> CreateAsync(CancellationToken cancellationToken)
        {
            if (!_gate.Wait(0))
            {
                return new BackupOutcome(BackupFailure.Busy, null);
            }

            try
            {
                Directory.CreateDirectory(_options.Directory);

                var now = Truncate(_time.GetUtcNow());
                var fileName = NextFreeName(now);

                if (fileName is null)
                {
                    _logger.LogError(
                        "Could not name a backup archive: every collision suffix for {Instant} is " +
                        "already taken.",
                        now);

                    return new BackupOutcome(BackupFailure.Failed, null);
                }

                var finalPath = Path.Combine(_options.Directory, fileName);
                var temporaryPath = finalPath + ".tmp";

                BackupFailure failure;

                try
                {
                    failure = Classify(await _tool.CreateDumpAsync(temporaryPath, cancellationToken));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    _logger.LogError(exception, "Could not write the backup archive {FileName}.", fileName);

                    failure = BackupFailure.Failed;
                }

                if (failure != BackupFailure.None)
                {
                    TryDelete(temporaryPath);

                    _logger.LogError(
                        "The backup archive {FileName} was not created ({Failure}).",
                        fileName,
                        failure);

                    return new BackupOutcome(failure, null);
                }

                try
                {
                    File.Move(temporaryPath, finalPath, overwrite: false);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // The archive was written but could not be given its final name, which is the
                    // one state the contract refuses to call a backup: nothing pattern-matching
                    // exists, so the failure is answered rather than hidden.
                    _logger.LogError(exception, "Could not publish the backup archive {FileName}.", fileName);

                    TryDelete(temporaryPath);

                    return new BackupOutcome(BackupFailure.Failed, null);
                }

                _logger.LogInformation("Created the backup archive {FileName}.", fileName);

                await RotateAsync(cancellationToken);

                return new BackupOutcome(BackupFailure.None, fileName);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <inheritdoc />
        public async Task<BackupOutcome> RestoreAsync(string fileName, CancellationToken cancellationToken)
        {
            if (!_gate.Wait(0))
            {
                return new BackupOutcome(BackupFailure.Busy, fileName);
            }

            try
            {
                if (!TryResolveExisting(fileName, out var path))
                {
                    return new BackupOutcome(BackupFailure.NotFound, fileName);
                }

                var result = await _tool.RestoreDumpAsync(path, cancellationToken);

                if (!result.Succeeded)
                {
                    var failure = result.ToolUnavailable
                        ? BackupFailure.ToolUnavailable
                        : BackupFailure.Failed;

                    _logger.LogError(
                        "Restoring the database from {FileName} failed ({Failure}).",
                        fileName,
                        failure);

                    return new BackupOutcome(failure, fileName);
                }

                // The archive carries a schema from the moment it was taken. Applying the pending
                // migrations makes the database current again, which is why a restore of an older
                // archive still ends in a schema this build can serve.
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    await scope.ServiceProvider
                        .GetRequiredService<IDatabaseInitializer>()
                        .MigrateAsync(cancellationToken);
                }

                // The settings row is inside the dump, so the cached reading describes a database
                // that no longer exists. Reloading keeps the process from serving the pre-restore
                // values until the ordinary staleness window expires.
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    await scope.ServiceProvider
                        .GetRequiredService<ISettingsSnapshotProvider>()
                        .ReloadAsync(cancellationToken);
                }

                _logger.LogInformation(
                    "Restored the database from {FileName} and applied the pending migrations.",
                    fileName);

                return new BackupOutcome(BackupFailure.None, fileName);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <inheritdoc />
        public Task<BackupOutcome> DeleteAsync(string fileName, CancellationToken cancellationToken)
        {
            if (!_gate.Wait(0))
            {
                return Task.FromResult(new BackupOutcome(BackupFailure.Busy, fileName));
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!TryResolveExisting(fileName, out var path))
                {
                    return Task.FromResult(new BackupOutcome(BackupFailure.NotFound, fileName));
                }

                File.Delete(path);

                _logger.LogInformation("Deleted the backup archive {FileName}.", fileName);

                return Task.FromResult(new BackupOutcome(BackupFailure.None, fileName));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(exception, "Could not delete the backup archive {FileName}.", fileName);

                return Task.FromResult(new BackupOutcome(BackupFailure.Failed, fileName));
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>The first name of this instant that is not taken, or null when all are.</summary>
        private string? NextFreeName(DateTimeOffset now)
        {
            var candidate = BackupFileName.Build(now);

            if (!File.Exists(Path.Combine(_options.Directory, candidate)))
            {
                return candidate;
            }

            for (var attempt = BackupFileName.FirstCollisionAttempt;
                 attempt <= BackupFileName.LastCollisionAttempt;
                 attempt++)
            {
                candidate = BackupFileName.BuildCollision(now, attempt);

                if (!File.Exists(Path.Combine(_options.Directory, candidate)))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Deletes everything past the configured count, after the new archive is durable.
        /// </summary>
        /// <remarks>
        /// The count is read here rather than in the scheduler so a manual create rotates the same
        /// way a scheduled one does: there is one rule, and it belongs to the operation that added
        /// a file.
        /// </remarks>
        private async Task RotateAsync(CancellationToken cancellationToken)
        {
            int keep;

            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                keep = scope.ServiceProvider
                    .GetRequiredService<ISettingsSnapshotProvider>()
                    .ApplicationSnapshot
                    .BackupRotationCount;
            }

            var names = Directory.Exists(_options.Directory)
                ? Directory.EnumerateFiles(_options.Directory, "*" + BackupFileName.Extension)
                    .Select(path => Path.GetFileName(path))
                    .ToList()
                : [];

            foreach (var name in BackupRotation.SelectForDeletion(names, keep))
            {
                cancellationToken.ThrowIfCancellationRequested();

                TryDelete(Path.Combine(_options.Directory, name));
            }
        }

        /// <summary>
        /// Resolves a client-supplied name to a real archive, refusing everything else.
        /// </summary>
        /// <remarks>
        /// An invalid name, a traversal attempt and a name that resolves to nothing all answer the
        /// same <c>false</c>: the API turns that into one <c>404</c>, because telling the two apart
        /// would make the endpoint a way to ask which paths exist on the host. The accept-set is
        /// the listing rule, deliberately: a well-formed but foreign <c>.dump</c> in the directory
        /// is not listed and not rotated, so it is not addressable here either.
        /// </remarks>
        private bool TryResolveExisting(string fileName, out string path)
        {
            path = string.Empty;

            if (!BackupFileNameRules.IsValid(fileName)
                || !BackupFileName.TryParse(fileName, out _)
                || !BackupFileNameRules.IsInsideDirectory(_options.Directory, fileName))
            {
                return false;
            }

            var candidate = Path.Combine(_options.Directory, fileName);

            if (!File.Exists(candidate))
            {
                return false;
            }

            path = candidate;

            return true;
        }

        /// <summary>Deletes a path, ignoring an absence and logging nothing for one.</summary>
        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The next create rotates again; a leftover file is not worth failing an operation
                // that already succeeded for.
                _ = exception;
            }
        }

        /// <summary>Turns a tool answer into the failure the service reports.</summary>
        private static BackupFailure Classify(BackupToolResult result)
            => result.Succeeded
                ? BackupFailure.None
                : result.ToolUnavailable
                    ? BackupFailure.ToolUnavailable
                    : BackupFailure.Failed;

        /// <summary>The instant cut to the second, which is the precision the name carries.</summary>
        private static DateTimeOffset Truncate(DateTimeOffset value)
            => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, TimeSpan.Zero);
    }
}
