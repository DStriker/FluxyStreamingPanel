namespace Fluxy.Core.Backups.Utils
{
    /// <summary>
    /// The six log tables whose data a dump leaves out, and the arguments that say so.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Their rows are the largest and the least valuable part of the database.</b> A log row
    /// describes a connection, a credit movement or a refused request that has already been
    /// handled; losing the rows written since a backup costs an audit trail, while losing the
    /// catalogues it sits beside would cost the installation. The tables themselves stay in the
    /// dump - only their data is excluded - so a restore recreates every one of them empty.
    /// </para>
    /// <para>
    /// <b>The catalog is the only source of the arguments, and a test pins it to exactly six.</b>
    /// <c>pg_dump</c> silently ignores an <c>--exclude-table-data</c> name that matches no table
    /// and still exits zero, so a typo would be invisible in the exit code and visible only as a
    /// backup that quietly carries the log it promised to leave out. An integration test therefore
    /// asserts every name here exists in the live schema.
    /// </para>
    /// </remarks>
    public static class BackupTableExclusions
    {
        /// <summary>The six tables whose rows are left out of every dump.</summary>
        public static readonly IReadOnlyList<string> Tables =
        [
            "user_logs",
            "credit_logs",
            "connection_logs",
            "api_request_failures",
            "stream_problems",
            "mag_events"
        ];

        /// <summary>The six <c>--exclude-table-data</c> arguments built from <see cref="Tables"/>.</summary>
        public static readonly IReadOnlyList<string> Arguments =
            Tables.Select(table => $"--exclude-table-data={table}").ToArray();
    }
}
