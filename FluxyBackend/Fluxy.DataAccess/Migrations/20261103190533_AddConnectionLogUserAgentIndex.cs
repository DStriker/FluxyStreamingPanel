using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <summary>
    /// Adds the index behind the connection log's exact <c>userAgent</c> filter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One index, no column and no data change.</b> <c>connection_logs.user_agent</c> has been
    /// stored since <c>AddConnectionLogs</c> - it is captured with the session and read back in the
    /// row - but nothing filtered by it exactly: the search box was a case-insensitive substring.
    /// The screen now offers the user agent as its own click-through, and that filter is an
    /// equality, so the index is what keeps it one read rather than a scan of a table that grows
    /// forever.
    /// </para>
    /// <para>
    /// <b>It is EF's own scaffolded index, not a hand-written one.</b> The partial live index in
    /// <c>AddConnectionLogs</c> had to be written by hand because EF's model index has no filter;
    /// this one is a plain b-tree the model can describe, so the configuration declares it and the
    /// snapshot carries it. That is why a second model change here must still be scaffolded rather
    /// than hand-edited.
    /// </para>
    /// </remarks>
    public partial class AddConnectionLogUserAgentIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_connection_logs_user_agent",
                table: "connection_logs",
                column: "user_agent");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_connection_logs_user_agent",
                table: "connection_logs");
        }
    }
}
