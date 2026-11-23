namespace Fluxy.Core.Backups.ReadModels
{
    /// <summary>
    /// One archive on disk, as the backups screen reads it.
    /// </summary>
    /// <remarks>
    /// <b>The instant is parsed from the file name, never read from the filesystem.</b> There is
    /// no metadata table: the name is the record of when the dump was taken, and
    /// <c>FileInfo.CreationTime</c> describes when the file last arrived on this volume - a copy,
    /// a restore of the directory or a container mount all rewrite it. A name is what survives
    /// every one of those, and it is what the rotation keeps its order by.
    /// </remarks>
    /// <param name="FileName">Name of the file inside the backup directory.</param>
    /// <param name="SizeBytes">Length of the file on disk, in bytes.</param>
    /// <param name="CreatedAtUtc">Instant encoded in the file name.</param>
    public sealed record BackupFileInfo(string FileName, long SizeBytes, DateTimeOffset CreatedAtUtc);
}
