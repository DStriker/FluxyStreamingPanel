using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectionLogServerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "server_id",
                table: "connection_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_connection_logs_server_id_ended_at",
                table: "connection_logs",
                columns: new[] { "server_id", "ended_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_connection_logs_server_id_ended_at",
                table: "connection_logs");

            migrationBuilder.DropColumn(
                name: "server_id",
                table: "connection_logs");
        }
    }
}
