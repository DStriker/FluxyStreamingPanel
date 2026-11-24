using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddServerReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "archive_server_id",
                table: "video_streams",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "transcoding_server_id",
                table: "video_streams",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "preferred_server_id",
                table: "subscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_video_streams_archive_server_id",
                table: "video_streams",
                column: "archive_server_id");

            migrationBuilder.CreateIndex(
                name: "IX_video_streams_transcoding_server_id",
                table: "video_streams",
                column: "transcoding_server_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_preferred_server_id",
                table: "subscriptions",
                column: "preferred_server_id");

            migrationBuilder.AddForeignKey(
                name: "FK_subscriptions_servers_preferred_server_id",
                table: "subscriptions",
                column: "preferred_server_id",
                principalTable: "servers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_video_streams_servers_archive_server_id",
                table: "video_streams",
                column: "archive_server_id",
                principalTable: "servers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_video_streams_servers_transcoding_server_id",
                table: "video_streams",
                column: "transcoding_server_id",
                principalTable: "servers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_subscriptions_servers_preferred_server_id",
                table: "subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_video_streams_servers_archive_server_id",
                table: "video_streams");

            migrationBuilder.DropForeignKey(
                name: "FK_video_streams_servers_transcoding_server_id",
                table: "video_streams");

            migrationBuilder.DropIndex(
                name: "IX_video_streams_archive_server_id",
                table: "video_streams");

            migrationBuilder.DropIndex(
                name: "IX_video_streams_transcoding_server_id",
                table: "video_streams");

            migrationBuilder.DropIndex(
                name: "IX_subscriptions_preferred_server_id",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "archive_server_id",
                table: "video_streams");

            migrationBuilder.DropColumn(
                name: "transcoding_server_id",
                table: "video_streams");

            migrationBuilder.DropColumn(
                name: "preferred_server_id",
                table: "subscriptions");
        }
    }
}
