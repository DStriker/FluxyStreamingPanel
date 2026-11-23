namespace Fluxy.API.Contracts.Backups
{
    /// <summary>
    /// One archive on disk, as the backups screen reads it.
    /// </summary>
    /// <remarks>
    /// <see cref="CreatedAtUtc"/> is the instant the file name encodes, not the filesystem's
    /// creation time: a copy or a remount rewrites the latter, and the name is the only record
    /// this feature keeps of when a dump was taken.
    /// </remarks>
    /// <param name="FileName">Name of the file inside the backup directory.</param>
    /// <param name="SizeBytes">Length of the file on disk, in bytes.</param>
    /// <param name="CreatedAtUtc">Instant encoded in the file name.</param>
    public sealed record BackupFileInfoResponse(string FileName, long SizeBytes, DateTimeOffset CreatedAtUtc);
}
