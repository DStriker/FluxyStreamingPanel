namespace Fluxy.Core.Persistence.Outcomes
{
    /// <summary>
    /// How one run of the external dump/restore tool ended.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The two flags are the whole of the refusal taxonomy the tool layer can state.</b>
    /// <c>ToolUnavailable</c> means the tool could not be started at all - docker is missing, the
    /// daemon is unreachable, the container is not there, or the run timed out. Everything else,
    /// a non-zero exit and a local IO error included, is a plain failure. The caller turns the
    /// first into <c>backup_tool_unavailable</c> and the second into <c>backup_failed</c>, and the
    /// distinction matters: the first is a deployment fault an operator fixes, the second may be
    /// a bad archive or a transient error worth retrying.
    /// </para>
    /// <para>
    /// A missing tool is never a silent skip. A scheduler that treated "docker is not installed"
    /// as "nothing to do" would leave an operator believing their installation was backed up.
    /// </para>
    /// </remarks>
    /// <param name="Succeeded">Whether the tool ran to completion with a zero exit code.</param>
    /// <param name="ToolUnavailable">Whether the tool could not be started at all.</param>
    public sealed record BackupToolResult(bool Succeeded, bool ToolUnavailable)
    {
        /// <summary>An answer for a run that completed.</summary>
        public static BackupToolResult Success { get; } = new(true, false);

        /// <summary>An answer for a run that could not start.</summary>
        public static BackupToolResult Unavailable { get; } = new(false, true);

        /// <summary>An answer for a run that started and failed.</summary>
        public static BackupToolResult Failed { get; } = new(false, false);
    }
}
