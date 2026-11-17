using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTranscodingProfileReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "transcoding_profile_id",
                table: "vod_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "transcoding_profile_id",
                table: "video_streams",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "transcoding_profile_id",
                table: "series",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "transcoding_profile_id",
                table: "audio_streams",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_vod_items_transcoding_profile_id",
                table: "vod_items",
                column: "transcoding_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_video_streams_transcoding_profile_id",
                table: "video_streams",
                column: "transcoding_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_series_transcoding_profile_id",
                table: "series",
                column: "transcoding_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_audio_streams_transcoding_profile_id",
                table: "audio_streams",
                column: "transcoding_profile_id");

            migrationBuilder.AddForeignKey(
                name: "FK_audio_streams_transcoding_profiles_transcoding_profile_id",
                table: "audio_streams",
                column: "transcoding_profile_id",
                principalTable: "transcoding_profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_series_transcoding_profiles_transcoding_profile_id",
                table: "series",
                column: "transcoding_profile_id",
                principalTable: "transcoding_profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_video_streams_transcoding_profiles_transcoding_profile_id",
                table: "video_streams",
                column: "transcoding_profile_id",
                principalTable: "transcoding_profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_vod_items_transcoding_profiles_transcoding_profile_id",
                table: "vod_items",
                column: "transcoding_profile_id",
                principalTable: "transcoding_profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audio_streams_transcoding_profiles_transcoding_profile_id",
                table: "audio_streams");

            migrationBuilder.DropForeignKey(
                name: "FK_series_transcoding_profiles_transcoding_profile_id",
                table: "series");

            migrationBuilder.DropForeignKey(
                name: "FK_video_streams_transcoding_profiles_transcoding_profile_id",
                table: "video_streams");

            migrationBuilder.DropForeignKey(
                name: "FK_vod_items_transcoding_profiles_transcoding_profile_id",
                table: "vod_items");

            migrationBuilder.DropIndex(
                name: "IX_vod_items_transcoding_profile_id",
                table: "vod_items");

            migrationBuilder.DropIndex(
                name: "IX_video_streams_transcoding_profile_id",
                table: "video_streams");

            migrationBuilder.DropIndex(
                name: "IX_series_transcoding_profile_id",
                table: "series");

            migrationBuilder.DropIndex(
                name: "IX_audio_streams_transcoding_profile_id",
                table: "audio_streams");

            migrationBuilder.DropColumn(
                name: "transcoding_profile_id",
                table: "vod_items");

            migrationBuilder.DropColumn(
                name: "transcoding_profile_id",
                table: "video_streams");

            migrationBuilder.DropColumn(
                name: "transcoding_profile_id",
                table: "series");

            migrationBuilder.DropColumn(
                name: "transcoding_profile_id",
                table: "audio_streams");
        }
    }
}
