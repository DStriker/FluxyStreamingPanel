namespace Fluxy.API.Contracts.Backups
{
    /// <summary>
    /// The answer to one create, restore or delete.
    /// </summary>
    /// <remarks>
    /// <see cref="FileName"/> is the archive the operation concerned, or null when a create named
    /// none. It travels beside the code because the screen names the file a toast is about, and
    /// the code alone would leave the client reading its own request back.
    /// </remarks>
    /// <param name="Code">Stable machine-readable outcome.</param>
    /// <param name="Message">English fallback sentence.</param>
    /// <param name="FileName">The archive the operation concerned, when it named one.</param>
    public sealed record BackupActionResponse(string Code, string Message, string? FileName);
}
