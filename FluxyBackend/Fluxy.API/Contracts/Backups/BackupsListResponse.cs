namespace Fluxy.API.Contracts.Backups
{
    /// <summary>
    /// The whole backup directory, newest first and unpaged.
    /// </summary>
    /// <remarks>
    /// There is no pager because the collection is a directory, not a table: the rotation keeps a
    /// bounded number of files and the screen shows all of them. A pager over a list the server
    /// already holds in full would add a request nobody needs.
    /// </remarks>
    /// <param name="Items">The archives, newest first.</param>
    public sealed record BackupsListResponse(IReadOnlyList<BackupFileInfoResponse> Items);
}
