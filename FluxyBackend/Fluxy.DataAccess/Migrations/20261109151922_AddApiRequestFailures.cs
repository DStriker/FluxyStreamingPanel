using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddApiRequestFailures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "api_request_failures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_data = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    content_kind = table.Column<short>(type: "smallint", nullable: true),
                    content_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    reason = table.Column<short>(type: "smallint", nullable: false),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    isp = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_request_failures", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_request_failures_country_code",
                table: "api_request_failures",
                column: "country_code");

            migrationBuilder.CreateIndex(
                name: "IX_api_request_failures_created_at_id",
                table: "api_request_failures",
                columns: new[] { "created_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_api_request_failures_ip",
                table: "api_request_failures",
                column: "ip");

            migrationBuilder.CreateIndex(
                name: "IX_api_request_failures_isp",
                table: "api_request_failures",
                column: "isp");

            migrationBuilder.CreateIndex(
                name: "IX_api_request_failures_reason",
                table: "api_request_failures",
                column: "reason");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_request_failures");
        }
    }
}
