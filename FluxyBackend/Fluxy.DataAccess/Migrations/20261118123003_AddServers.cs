using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddServers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "servers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    port = table.Column<int>(type: "integer", nullable: false),
                    client_count = table.Column<int>(type: "integer", nullable: false),
                    os_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    network_interface = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    latency = table.Column<double>(type: "double precision", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    guaranteed_speed_kbps = table.Column<int>(type: "integer", nullable: false),
                    timeshift_only = table.Column<bool>(type: "boolean", nullable: false),
                    hostname = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: true),
                    vpn_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    country_codes = table.Column<string[]>(type: "text[]", nullable: false),
                    is_main = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_servers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_servers_name",
                table: "servers",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "servers");
        }
    }
}
