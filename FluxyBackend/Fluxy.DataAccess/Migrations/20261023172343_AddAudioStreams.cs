using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioStreams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audio_streams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    stream_type = table.Column<short>(type: "smallint", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    active_source = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    enigma2_sid = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    restream_user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    restream_proxy = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    restream_cookies = table.Column<string>(type: "text", nullable: true),
                    restream_headers = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audio_streams", x => x.id);
                    table.ForeignKey(
                        name: "FK_audio_streams_content_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "content_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audio_stream_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stream_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audio_stream_sources", x => x.id);
                    table.ForeignKey(
                        name: "FK_audio_stream_sources_audio_streams_stream_id",
                        column: x => x.stream_id,
                        principalTable: "audio_streams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audio_stream_sources_stream_id_position",
                table: "audio_stream_sources",
                columns: new[] { "stream_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audio_streams_category_id_name",
                table: "audio_streams",
                columns: new[] { "category_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audio_streams_status",
                table: "audio_streams",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_audio_streams_stream_type",
                table: "audio_streams",
                column: "stream_type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audio_stream_sources");

            migrationBuilder.DropTable(
                name: "audio_streams");
        }
    }
}
