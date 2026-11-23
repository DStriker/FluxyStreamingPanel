using Fluxy.Core.Backups.Enums;

namespace Fluxy.Core.Backups.Outcomes
{
    /// <summary>
    /// How one create, restore or delete ended, and which file it concerned.
    /// </summary>
    /// <param name="Failure">The refusal, or <see cref="BackupFailure.None"/> on success.</param>
    /// <param name="FileName">The file created, restored or deleted, or null when no file was named.</param>
    public sealed record BackupOutcome(BackupFailure Failure, string? FileName)
    {
        /// <summary>Whether the operation completed.</summary>
        public bool Succeeded => Failure == BackupFailure.None;
    }
}
