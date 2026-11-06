using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCreditsAndCreditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The default exists only to fill the rows that already exist, and is dropped
            // immediately afterwards. Keeping it would let a future insert that forgot the
            // column be answered with a 0 the application never chose - the reason no model
            // mapping carries HasDefaultValue.
            migrationBuilder.AddColumn<int>(
                name: "credits",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("ALTER TABLE users ALTER COLUMN credits DROP DEFAULT;");

            migrationBuilder.CreateTable(
                name: "credit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reseller_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_credit_logs_users_issued_by_id",
                        column: x => x.issued_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_credit_logs_users_reseller_id",
                        column: x => x.reseller_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_credit_logs_created_at_id",
                table: "credit_logs",
                columns: new[] { "created_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_credit_logs_issued_by_id",
                table: "credit_logs",
                column: "issued_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_logs_reseller_id_created_at",
                table: "credit_logs",
                columns: new[] { "reseller_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "credit_logs");

            migrationBuilder.DropColumn(
                name: "credits",
                table: "users");
        }
    }
}
