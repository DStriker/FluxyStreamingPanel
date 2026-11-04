namespace Fluxy.Core.Subscriptions.Enums
{
    /// <summary>
    /// The two stored columns the connection log table may be ordered by.
    /// </summary>
    /// <remarks>
    /// Deliberately two, and both are columns of <c>connection_logs</c> itself. The table also
    /// draws the owner's name and the stream's title, but neither is a column here - they are
    /// joined and resolved for the page - so ordering by them would order a page the server had
    /// already cut, which is the same lie a filter applied in the browser tells. The controller's
    /// remarks record that refusal.
    /// </remarks>
    public enum ConnectionLogSortField
    {
        /// <summary>When the session started. The default, and the newest first.</summary>
        StartedAt,

        /// <summary>
        /// When it finished. A null end - a session that is still live - groups last in both
        /// directions, so "most recently ended first" never opens with sessions that have not
        /// ended.
        /// </summary>
        EndedAt
    }
}
