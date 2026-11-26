namespace Fluxy.Core.Subscriptions.Rows
{
    /// <summary>
    /// How many live connections, and how many distinct owners, one server carried.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A projection shaped for one query: the dashboard groups the live log by
    /// <c>server_id</c> and draws one row per installation server from it.
    /// </para>
    /// <para>
    /// <b><see cref="ServerId"/> is nullable and the null group is the unattributed one.</b> A
    /// connection whose capture path could not name a server still counts towards the
    /// installation's totals, and contributes to no per-server row. That is the attribution rule
    /// <c>DashboardServer.UsersWithActiveConnections</c> records.
    /// </para>
    /// </remarks>
    public sealed record ConnectionLogServerCountRow
    {
        /// <summary>Server the group belongs to, or null for the unattributed group.</summary>
        public required Guid? ServerId { get; init; }

        /// <summary>How many live connections the group holds.</summary>
        public required int ConnectionCount { get; init; }

        /// <summary>
        /// How many distinct accounts the group's subscriptions belong to - one viewer with two
        /// connections counts once.
        /// </summary>
        public required int UserCount { get; init; }
    }
}
