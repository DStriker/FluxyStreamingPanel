using System.Diagnostics;
using Fluxy.Core.Backups.Options;
using Fluxy.Core.Backups.Utils;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Outcomes;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Fluxy.DataAccess.Infrastructure.Backups
{
    /// <summary>
    /// Runs <c>pg_dump</c> and <c>pg_restore</c> inside the PostgreSQL container.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why <c>docker exec</c> rather than a host binary.</b> The client tools are not on this
    /// host's PATH, and the container ships clients of exactly the server's major version, which
    /// is the one constraint that matters: <c>pg_dump</c> refuses a server newer than itself, and
    /// <c>pg_restore</c> of an archive written by a newer client is not supported either. The
    /// image is <c>postgres:18.6</c> today, so the host needs nothing but <c>docker.exe</c>.
    /// </para>
    /// <para>
    /// <b>No password ever reaches an argument list or the log.</b> <c>docker exec</c> reaches the
    /// server over the container's unix socket, whose <c>pg_hba.conf</c> entry is <c>trust</c>;
    /// the TCP entry is the password-protected one and is deliberately not used here. Arguments
    /// carry the user and the database names and nothing else.
    /// </para>
    /// <para>
    /// <b>The archive is bytes, not text.</b> The dump is read from <c>stdout.BaseStream</c> and
    /// written straight to the destination file - a string read would decode and re-encode a
    /// binary format and corrupt it. The restore sends the reverse: the file's bytes go into
    /// <c>stdin</c> of an <c>-i</c> exec, so the archive never has to be copied into the container
    /// first.
    /// </para>
    /// <para>
    /// <b>A restore flushes the connection pool first.</b> <c>pg_restore --clean</c> drops and
    /// recreates the schema, and PostgreSQL refuses to drop objects a live session still holds
    /// open - which is exactly what this process's pooled connections are. The flush happens here
    /// rather than in the service because it needs Npgsql, and the architecture test keeps Npgsql
    /// out of <c>Fluxy.Application</c>.
    /// </para>
    /// </remarks>
    public sealed class PostgresBackupTool : IDatabaseBackupTool
    {
        /// <summary>How long one tool run may take before it is abandoned.</summary>
        private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(30);

        private readonly string _user;
        private readonly string _database;
        private readonly BackupsOptions _options;
        private readonly ILogger<PostgresBackupTool> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PostgresBackupTool"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string the application itself uses.</param>
        /// <param name="options">Name of the container the tool is run in.</param>
        /// <param name="logger">Logger the concrete reason of a refusal is reported to.</param>
        /// <remarks>
        /// The user and the database are parsed from the connection string because they have to
        /// match the ones the application uses: a dump of <c>postgres</c> would be a backup of
        /// nothing, and the failure would not appear until the day it was needed.
        /// </remarks>
        public PostgresBackupTool(
            string connectionString,
            BackupsOptions options,
            ILogger<PostgresBackupTool> logger)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);

            _user = string.IsNullOrWhiteSpace(builder.Username) ? "postgres" : builder.Username;
            _database = string.IsNullOrWhiteSpace(builder.Database) ? "postgres" : builder.Database;
            _options = options;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<BackupToolResult> CreateDumpAsync(
            string destinationFilePath,
            CancellationToken cancellationToken)
        {
            var arguments = new List<string>
            {
                "exec",
                _options.ContainerName,
                "pg_dump",
                "-U",
                _user,
                "-d",
                _database,
                "-Fc"
            };

            arguments.AddRange(BackupTableExclusions.Arguments);

            try
            {
                await using var destination = new FileStream(
                    destinationFilePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true);

                using var process = Start(arguments, redirectStandardInput: false);

                if (process is null)
                {
                    return BackupToolResult.Unavailable;
                }

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(RunTimeout);

                var copy = process.StandardOutput.BaseStream.CopyToAsync(destination, timeout.Token);
                var stderr = process.StandardError.ReadToEndAsync(timeout.Token);

                await copy;

                var exitCode = await WaitAsync(process, timeout.Token);
                var error = await stderr;

                if (exitCode != 0)
                {
                    return Refused(exitCode, error, "creating a dump");
                }

                return BackupToolResult.Success;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "The pg_dump run in container {Container} exceeded {Timeout} and was abandoned.",
                    _options.ContainerName,
                    RunTimeout);

                return BackupToolResult.Unavailable;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(exception, "Could not write the dump to {Path}.", destinationFilePath);

                return BackupToolResult.Failed;
            }
        }

        /// <inheritdoc />
        public async Task<BackupToolResult> RestoreDumpAsync(
            string dumpFilePath,
            CancellationToken cancellationToken)
        {
            // Before the tool starts, and deliberately here: the clean restore has to drop objects
            // no pooled session still holds. Npgsql cannot be named from the application layer.
            NpgsqlConnection.ClearAllPools();

            var arguments = new List<string>
            {
                "exec",
                "-i",
                _options.ContainerName,
                "pg_restore",
                "-U",
                _user,
                "-d",
                _database,
                "--no-owner",
                "--clean",
                "--if-exists",
                "--single-transaction"
            };

            try
            {
                await using var source = new FileStream(
                    dumpFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    useAsync: true);

                using var process = Start(arguments, redirectStandardInput: true);

                if (process is null)
                {
                    return BackupToolResult.Unavailable;
                }

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(RunTimeout);

                var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
                var send = source.CopyToAsync(process.StandardInput.BaseStream, timeout.Token);

                await send;
                await process.StandardInput.BaseStream.FlushAsync(timeout.Token);
                process.StandardInput.Close();

                var exitCode = await WaitAsync(process, timeout.Token);
                var error = await stderr;

                if (exitCode != 0)
                {
                    return Refused(exitCode, error, "restoring a dump");
                }

                return BackupToolResult.Success;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "The pg_restore run in container {Container} exceeded {Timeout} and was abandoned.",
                    _options.ContainerName,
                    RunTimeout);

                return BackupToolResult.Unavailable;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(exception, "Could not read the archive at {Path}.", dumpFilePath);

                return BackupToolResult.Failed;
            }
        }

        /// <summary>
        /// Starts docker, or answers null when it cannot be started at all.
        /// </summary>
        /// <remarks>
        /// A missing executable raises <see cref="System.ComponentModel.Win32Exception"/> from
        /// <c>Start</c> rather than producing a process with an exit code, which is the one case
        /// this method can classify itself. Everything a running docker says is classified by
        /// <see cref="Refused"/> from its exit code and its wording.
        /// </remarks>
        private Process? Start(List<string> arguments, bool redirectStandardInput)
        {
            var info = new ProcessStartInfo
            {
                FileName = "docker",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = redirectStandardInput,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
            {
                info.ArgumentList.Add(argument);
            }

            try
            {
                return Process.Start(info);
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                _logger.LogError(
                    exception,
                    "docker could not be started, so the backup tool is unavailable. Install Docker " +
                    "Desktop and make sure docker.exe is on the PATH of the process running this API.");

                return null;
            }
        }

        /// <summary>Waits for the process to exit, under the run's own timeout.</summary>
        private static async Task<int> WaitAsync(Process process, CancellationToken cancellationToken)
        {
            await process.WaitForExitAsync(cancellationToken);

            return process.ExitCode;
        }

        /// <summary>
        /// Classifies a non-zero exit as "the tool is not usable" or "the run failed".
        /// </summary>
        /// <remarks>
        /// Docker's own refusals come out with a small set of exit codes (125 for a daemon or
        /// invocation failure, 126/127 for a missing command) and a recognisable sentence. A
        /// container that is not running, an unreachable daemon and a missing image are all
        /// deployment faults; a non-zero <c>pg_dump</c>/<c>pg_restore</c> exit is the tool running
        /// and saying no. The wording of the stderr is never logged whole: it can carry a
        /// connection string, and a backup log is not the place for one.
        /// </remarks>
        private BackupToolResult Refused(int exitCode, string? stderr, string what)
        {
            var reason = stderr?.Trim() ?? string.Empty;

            if (exitCode is 125 or 126 or 127
                || reason.Contains("Cannot connect to the Docker daemon", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("error during connect", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("No such container", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("is not running", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("command not found", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(
                    "The backup tool refused {What} in container {Container} (docker exit {ExitCode}). " +
                    "Docker or the container is not usable.",
                    what,
                    _options.ContainerName,
                    exitCode);

                return BackupToolResult.Unavailable;
            }

            _logger.LogError(
                "The backup tool failed while {What} in container {Container} (exit {ExitCode}): {Reason}",
                what,
                _options.ContainerName,
                exitCode,
                Shorten(reason));

            return BackupToolResult.Failed;
        }

        /// <summary>
        /// The first line of a tool's stderr, cut short - enough to identify the failure and short
        /// enough not to carry a connection string into the log.
        /// </summary>
        private static string Shorten(string reason)
        {
            var line = reason.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            if (string.IsNullOrWhiteSpace(line))
            {
                return "no message";
            }

            var trimmed = line.Trim();

            return trimmed.Length <= 200 ? trimmed : string.Concat(trimmed.AsSpan(0, 200), "…");
        }
    }
}
