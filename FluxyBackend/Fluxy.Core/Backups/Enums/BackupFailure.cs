namespace Fluxy.Core.Backups.Enums
{
    /// <summary>
    /// How a backup operation ended.
    /// </summary>
    /// <remarks>
    /// The four refusals are genuinely different problems and the screen answers each with its own
    /// code: a missing tool is a deployment fault, a held gate is "try again in a moment", a name
    /// that resolves to no file is a stale row on a tab, and a failed run is the tool itself.
    /// Collapsing them into one "failed" would tell an operator nothing about what to fix.
    /// </remarks>
    public enum BackupFailure
    {
        /// <summary>The operation completed.</summary>
        None = 0,

        /// <summary>Docker, the daemon, the container or the timeout refused the run.</summary>
        ToolUnavailable = 1,

        /// <summary>Another write holds the single-flight gate.</summary>
        Busy = 2,

        /// <summary>The name resolves to no file in the backup directory.</summary>
        NotFound = 3,

        /// <summary>The tool ran and failed, or the file could not be written.</summary>
        Failed = 4
    }
}
