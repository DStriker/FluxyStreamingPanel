using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    record_type = table.Column<short>(type: "smallint", nullable: false),
                    is_success = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    isp = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_logs_created_at_id",
                table: "user_logs",
                columns: new[] { "created_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_user_logs_record_type",
                table: "user_logs",
                column: "record_type");

            migrationBuilder.CreateIndex(
                name: "IX_user_logs_user_id_created_at",
                table: "user_logs",
                columns: new[] { "user_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_logs");
        }
    }
}
