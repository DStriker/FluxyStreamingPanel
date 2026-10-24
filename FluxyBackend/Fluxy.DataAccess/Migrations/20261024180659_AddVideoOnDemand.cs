using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoOnDemand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vod_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    vod_type = table.Column<short>(type: "smallint", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    subtitle_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    imdb_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    poster_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    backdrop_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    plot = table.Column<string>(type: "text", nullable: true),
                    cast = table.Column<string[]>(type: "text[]", nullable: false),
                    director = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    genres = table.Column<string[]>(type: "text[]", nullable: false),
                    release_date = table.Column<DateOnly>(type: "date", nullable: true),
                    rating = table.Column<decimal>(type: "numeric(3,1)", nullable: true),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vod_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_vod_items_content_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "content_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vod_items_category_id_name",
                table: "vod_items",
                columns: new[] { "category_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vod_items_rating",
                table: "vod_items",
                column: "rating");

            migrationBuilder.CreateIndex(
                name: "IX_vod_items_release_date",
                table: "vod_items",
                column: "release_date");

            migrationBuilder.CreateIndex(
                name: "IX_vod_items_status",
                table: "vod_items",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_vod_items_vod_type",
                table: "vod_items",
                column: "vod_type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vod_items");
        }
    }
}
