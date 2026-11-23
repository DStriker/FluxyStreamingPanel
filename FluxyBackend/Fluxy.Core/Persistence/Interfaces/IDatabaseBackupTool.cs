using Fluxy.Core.Persistence.Outcomes;

namespace Fluxy.Core.Persistence.Interfaces
{
    /// <summary>
    /// Runs the external PostgreSQL dump and restore tools against the database this host uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The mechanism is the database lifecycle, not a repository.</b> It sits beside
    /// <see cref="IDatabaseInitializer"/> for that reason: it creates and replaces the database
    /// rather than reading or writing rows of it, and the implementation lives in the data access
    /// layer, which is the only layer allowed to know how the database is reached.
    /// </para>
    /// <para>
    /// <b>The application layer never names the tool.</b> It knows this interface, a destination
    /// path and a result; that the archive is produced by <c>docker exec … pg_dump</c> is a detail
    /// of the implementation, and the architecture test that keeps Npgsql out of
    /// <c>Fluxy.Application</c> is what makes that boundary structural rather than remembered.
    /// </para>
    /// </remarks>
    public interface IDatabaseBackupTool
    {
        /// <summary>
        /// Writes a dump of the database to the given path.
        /// </summary>
        /// <param name="destinationFilePath">Absolute path the archive is written to.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Whether the dump was written, and whether the tool was usable at all.</returns>
        Task<BackupToolResult> CreateDumpAsync(string destinationFilePath, CancellationToken cancellationToken);

        /// <summary>
        /// Replaces the database with the archive at the given path.
        /// </summary>
        /// <param name="dumpFilePath">Absolute path of the archive to restore.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Whether the restore completed, and whether the tool was usable at all.</returns>
        Task<BackupToolResult> RestoreDumpAsync(string dumpFilePath, CancellationToken cancellationToken);
    }
}
