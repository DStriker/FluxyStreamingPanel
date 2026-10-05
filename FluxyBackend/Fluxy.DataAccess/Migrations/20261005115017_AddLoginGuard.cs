using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "bind_session_to_ip",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "geo_protection_enabled",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "payload",
                table: "pending_changes",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_login_guard_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<short>(type: "smallint", nullable: false),
                    value = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    label = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_login_guard_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_login_guard_rules_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_login_guard_rules_user_id",
                table: "user_login_guard_rules",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_login_guard_rules_user_id_kind_value",
                table: "user_login_guard_rules",
                columns: new[] { "user_id", "kind", "value" },
                unique: true);

            // Rows whose account is already gone would refuse the foreign keys below, so
            // they go first. A code that outlives its account confirms nothing, and a session
            // that signs in as nobody signs in as nobody - deleting them is the cleanup the
            // keys would have done all along had they existed.
            migrationBuilder.Sql(
                """DELETE FROM "pending_changes" WHERE "user_id" NOT IN (SELECT "id" FROM "users")""");
            migrationBuilder.Sql(
                """DELETE FROM "refresh_tokens" WHERE "user_id" NOT IN (SELECT "id" FROM "users")""");

            migrationBuilder.AddForeignKey(
                name: "FK_pending_changes_users_user_id",
                table: "pending_changes",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_refresh_tokens_users_user_id",
                table: "refresh_tokens",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pending_changes_users_user_id",
                table: "pending_changes");

            migrationBuilder.DropForeignKey(
                name: "FK_refresh_tokens_users_user_id",
                table: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "user_login_guard_rules");

            migrationBuilder.DropColumn(
                name: "bind_session_to_ip",
                table: "users");

            migrationBuilder.DropColumn(
                name: "geo_protection_enabled",
                table: "users");

            migrationBuilder.DropColumn(
                name: "payload",
                table: "pending_changes");
        }
    }
}
