using Fluxy.Core.Backups.Outcomes;
using Fluxy.Core.Backups.ReadModels;

namespace Fluxy.Core.Backups.Interfaces
{
    /// <summary>
    /// Lists, creates, restores and deletes the database archives kept on the API host.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>There is no metadata row.</b> An archive is a file whose name carries the instant it was
    /// taken; "the latest backup" and "is a backup due" are derived from the directory, not from a
    /// table. That is why the list is the whole of the read side - there is nothing else to
    /// reconcile, and a database restore cannot leave the record of itself behind.
    /// </para>
    /// <para>
    /// All three writes are serialized by one gate: a restore replaces the database and a dump
    /// that ran beside it would capture a half-restored state. The read is not gated - it touches
    /// only the directory - and never returns a partially written file, because a dump is moved
    /// into its final name only once the tool has finished.
    /// </para>
    /// </remarks>
    public interface IBackupService
    {
        /// <summary>Lists the archives, newest first.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The archives whose names are pattern-matching; empty when there are none.</returns>
        Task<IReadOnlyList<BackupFileInfo>> ListAsync(CancellationToken cancellationToken);

        /// <summary>Takes a dump and rotates the directory afterwards.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, naming the file that was created.</returns>
        Task<BackupOutcome> CreateAsync(CancellationToken cancellationToken);

        /// <summary>Replaces the database with the named archive, then migrates it.</summary>
        /// <param name="fileName">Name of the archive inside the backup directory.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, naming the file that was restored.</returns>
        Task<BackupOutcome> RestoreAsync(string fileName, CancellationToken cancellationToken);

        /// <summary>Deletes one archive.</summary>
        /// <param name="fileName">Name of the archive inside the backup directory.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, naming the file that was deleted.</returns>
        Task<BackupOutcome> DeleteAsync(string fileName, CancellationToken cancellationToken);
    }
}
