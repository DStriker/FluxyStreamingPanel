using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectionLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "connection_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stream_kind = table.Column<short>(type: "smallint", nullable: false),
                    stream_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    isp = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    broadcast_pid = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connection_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_connection_logs_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_connection_logs_country_code",
                table: "connection_logs",
                column: "country_code");

            migrationBuilder.CreateIndex(
                name: "IX_connection_logs_ip",
                table: "connection_logs",
                column: "ip");

            migrationBuilder.CreateIndex(
                name: "IX_connection_logs_isp",
                table: "connection_logs",
                column: "isp");

            migrationBuilder.CreateIndex(
                name: "IX_connection_logs_started_at_id",
                table: "connection_logs",
                columns: new[] { "started_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_connection_logs_stream_kind_stream_id",
                table: "connection_logs",
                columns: new[] { "stream_kind", "stream_id" });

            migrationBuilder.CreateIndex(
                name: "IX_connection_logs_subscription_id_started_at",
                table: "connection_logs",
                columns: new[] { "subscription_id", "started_at" });

            // The live view is a partial index over a small tail of a table that grows forever.
            // EF Core's model index has no filter, so this one is written by hand under a fixed
            // name - the same treatment the subscriptions' three partial unique indexes get.
            // "Live" is exactly ended_at being null, which is the predicate both the filter and
            // the index share; (started_at DESC, id DESC) is the default order and its tie-break.
            migrationBuilder.Sql(
                "CREATE INDEX \"IX_connection_logs_live_started_at\" " +
                "ON connection_logs (started_at DESC, id DESC) WHERE ended_at IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_connection_logs_live_started_at\";");

            migrationBuilder.DropTable(
                name: "connection_logs");
        }
    }
}
