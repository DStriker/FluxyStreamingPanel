using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddBouquets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bouquets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bouquets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bouquet_audio_streams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bouquet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    audio_stream_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bouquet_audio_streams", x => x.id);
                    table.ForeignKey(
                        name: "FK_bouquet_audio_streams_audio_streams_audio_stream_id",
                        column: x => x.audio_stream_id,
                        principalTable: "audio_streams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bouquet_audio_streams_bouquets_bouquet_id",
                        column: x => x.bouquet_id,
                        principalTable: "bouquets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bouquet_series",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bouquet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    series_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bouquet_series", x => x.id);
                    table.ForeignKey(
                        name: "FK_bouquet_series_bouquets_bouquet_id",
                        column: x => x.bouquet_id,
                        principalTable: "bouquets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bouquet_series_series_series_id",
                        column: x => x.series_id,
                        principalTable: "series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bouquet_video_streams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bouquet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    video_stream_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bouquet_video_streams", x => x.id);
                    table.ForeignKey(
                        name: "FK_bouquet_video_streams_bouquets_bouquet_id",
                        column: x => x.bouquet_id,
                        principalTable: "bouquets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bouquet_video_streams_video_streams_video_stream_id",
                        column: x => x.video_stream_id,
                        principalTable: "video_streams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bouquet_vod_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bouquet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vod_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bouquet_vod_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_bouquet_vod_items_bouquets_bouquet_id",
                        column: x => x.bouquet_id,
                        principalTable: "bouquets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bouquet_vod_items_vod_items_vod_item_id",
                        column: x => x.vod_item_id,
                        principalTable: "vod_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bouquet_audio_streams_audio_stream_id",
                table: "bouquet_audio_streams",
                column: "audio_stream_id");

            migrationBuilder.CreateIndex(
                name: "IX_bouquet_audio_streams_bouquet_id_audio_stream_id",
                table: "bouquet_audio_streams",
                columns: new[] { "bouquet_id", "audio_stream_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bouquet_audio_streams_bouquet_id_sort_order",
                table: "bouquet_audio_streams",
                columns: new[] { "bouquet_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_bouquet_series_bouquet_id_series_id",
                table: "bouquet_series",
                columns: new[] { "bouquet_id", "series_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bouquet_series_bouquet_id_sort_order",
                table: "bouquet_series",
                columns: new[] { "bouquet_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_bouquet_series_series_id",
                table: "bouquet_series",
                column: "series_id");

            migrationBuilder.CreateIndex(
                name: "IX_bouquet_video_streams_bouquet_id_sort_order",
                table: "bouquet_video_streams",
                columns: new[] { "bouquet_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_bouquet_video_streams_bouquet_id_video_stream_id",
                table: "bouquet_video_streams",
                columns: new[] { "bouquet_id", "video_stream_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bouquet_video_streams_video_stream_id",
                table: "bouquet_video_streams",
                column: "video_stream_id");

            migrationBuilder.CreateIndex(
                name: "IX_bouquet_vod_items_bouquet_id_sort_order",
                table: "bouquet_vod_items",
                columns: new[] { "bouquet_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_bouquet_vod_items_bouquet_id_vod_item_id",
                table: "bouquet_vod_items",
                columns: new[] { "bouquet_id", "vod_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bouquet_vod_items_vod_item_id",
                table: "bouquet_vod_items",
                column: "vod_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_bouquets_sort_order",
                table: "bouquets",
                column: "sort_order");

            // A bouquet's name is unique for the installation **without regard to case**, so
            // "Кино" and "кино" are one bouquet. EF Core cannot express an expression index as a
            // model index, so the rule is written here as raw SQL under the name the service
            // matches a unique violation against (BouquetConfiguration.NameLowerIndexName). The
            // service probes for a duplicate first and this index answers the race between two
            // operators with the same map to `bouquet_already_exists`.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_bouquets_name_lower\" ON bouquets (lower(name));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropped explicitly before the table it indexes, so the Down path is honest about
            // the one object this migration creates outside the model.
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_bouquets_name_lower\";");

            migrationBuilder.DropTable(
                name: "bouquet_audio_streams");

            migrationBuilder.DropTable(
                name: "bouquet_series");

            migrationBuilder.DropTable(
                name: "bouquet_video_streams");

            migrationBuilder.DropTable(
                name: "bouquet_vod_items");

            migrationBuilder.DropTable(
                name: "bouquets");
        }
    }
}
