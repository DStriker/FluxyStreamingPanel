using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddEpgSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "epg_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    days_keep = table.Column<int>(type: "integer", nullable: false),
                    last_epg_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    raw_data = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_epg_sources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "epg_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    language = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true),
                    start_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_epg_data", x => x.id);
                    table.ForeignKey(
                        name: "FK_epg_data_epg_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "epg_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_video_streams_epg_source_id",
                table: "video_streams",
                column: "epg_source_id");

            migrationBuilder.CreateIndex(
                name: "IX_epg_data_channel_id",
                table: "epg_data",
                column: "channel_id");

            migrationBuilder.CreateIndex(
                name: "IX_epg_data_end_date",
                table: "epg_data",
                column: "end_date");

            migrationBuilder.CreateIndex(
                name: "IX_epg_data_language",
                table: "epg_data",
                column: "language");

            migrationBuilder.CreateIndex(
                name: "IX_epg_data_source_id",
                table: "epg_data",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "IX_epg_data_start_date",
                table: "epg_data",
                column: "start_date");

            migrationBuilder.CreateIndex(
                name: "IX_epg_sources_name",
                table: "epg_sources",
                column: "name",
                unique: true);

            // Before this table existed a stream could name any uuid as its guide, and the
            // foreign key below would refuse the migration on the first row that did. The pair
            // rule says a guide is a source *and* a channel key, so both halves are cleared
            // together: a key left behind would be a name nothing can ever look up. This is
            // hand-written because no scaffold can know that the column it is about to
            // constrain was unconstrained until now.
            migrationBuilder.Sql(
                "UPDATE video_streams " +
                "SET epg_source_id = NULL, epg_channel_key = NULL " +
                "WHERE epg_source_id IS NOT NULL " +
                "AND NOT EXISTS (SELECT 1 FROM epg_sources " +
                "WHERE epg_sources.id = video_streams.epg_source_id);");

            migrationBuilder.AddForeignKey(
                name: "FK_video_streams_epg_sources_epg_source_id",
                table: "video_streams",
                column: "epg_source_id",
                principalTable: "epg_sources",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_video_streams_epg_sources_epg_source_id",
                table: "video_streams");

            migrationBuilder.DropTable(
                name: "epg_data");

            migrationBuilder.DropTable(
                name: "epg_sources");

            migrationBuilder.DropIndex(
                name: "IX_video_streams_epg_source_id",
                table: "video_streams");
        }
    }
}
