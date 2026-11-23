using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Outcomes;

namespace Fluxy.IntegrationTests.Infrastructure.Fakes
{
    /// <summary>
    /// The dump/restore tool as the API's tests see it: it writes a placeholder archive instead
    /// of talking to Docker, and the tests decide how each run ends.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Never the real tool.</b> The integration suite runs against Testcontainers' PostgreSQL,
    /// whose container name the host does not know and whose database the other tests share - a
    /// real restore would replace the shared schema mid-run. The fake writes the file the service
    /// then renames, so the durability, listing and rotation rules are exercised for real; only
    /// the bytes are not PostgreSQL's.
    /// </para>
    /// <para>
    /// <b>The gate is observable through <see cref="Hold"/> and <see cref="Entered"/>.</b> A test
    /// that wants to see <c>409 backup_busy</c> holds a create in flight on the service's own
    /// semaphore and fires a second request; nothing has to reach into the service's internals.
    /// </para>
    /// </remarks>
    public sealed class FakeDatabaseBackupTool : IDatabaseBackupTool
    {
        private TaskCompletionSource? _hold;
        private TaskCompletionSource? _entered;

        /// <summary>How the next create ends; success by default.</summary>
        public BackupToolResult CreateResult { get; set; } = BackupToolResult.Success;

        /// <summary>How the next restore ends; success by default.</summary>
        public BackupToolResult RestoreResult { get; set; } = BackupToolResult.Success;

        /// <summary>How many dumps have been requested since the last reset.</summary>
        public int CreateCalls => Volatile.Read(ref _createCalls);

        /// <summary>How many restores have been requested since the last reset.</summary>
        public int RestoreCalls => Volatile.Read(ref _restoreCalls);

        private int _createCalls;
        private int _restoreCalls;

        /// <summary>
        /// Makes the next create wait until the returned source is completed, so a second request
        /// finds the service's gate held.
        /// </summary>
        /// <param name="entered">Completes once the tool has actually been called.</param>
        /// <returns>The source that releases the held run.</returns>
        public TaskCompletionSource Hold(out Task entered)
        {
            var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var seen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            _hold = hold;
            _entered = seen;
            entered = seen.Task;

            return hold;
        }

        /// <summary>Puts the counters and the answers back to their defaults.</summary>
        public void Reset()
        {
            CreateResult = BackupToolResult.Success;
            RestoreResult = BackupToolResult.Success;
            Interlocked.Exchange(ref _createCalls, 0);
            Interlocked.Exchange(ref _restoreCalls, 0);
            _hold = null;
            _entered = null;
        }

        /// <inheritdoc />
        public async Task<BackupToolResult> CreateDumpAsync(
            string destinationFilePath,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _createCalls);

            var hold = _hold;
            var entered = _entered;

            _hold = null;
            _entered = null;

            entered?.TrySetResult();

            if (hold is not null)
            {
                await hold.Task.WaitAsync(cancellationToken);
            }

            if (!CreateResult.Succeeded)
            {
                return CreateResult;
            }

            await File.WriteAllBytesAsync(
                destinationFilePath,
                new byte[1024],
                cancellationToken);

            return BackupToolResult.Success;
        }

        /// <inheritdoc />
        public Task<BackupToolResult> RestoreDumpAsync(
            string dumpFilePath,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _restoreCalls);

            return Task.FromResult(RestoreResult);
        }
    }
}
