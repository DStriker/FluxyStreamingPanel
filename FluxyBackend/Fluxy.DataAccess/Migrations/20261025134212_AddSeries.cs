using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "series",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    imdb_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    poster_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    backdrop_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    plot = table.Column<string>(type: "text", nullable: true),
                    cast = table.Column<string[]>(type: "text[]", nullable: false),
                    director = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    genres = table.Column<string[]>(type: "text[]", nullable: false),
                    release_date = table.Column<DateOnly>(type: "date", nullable: true),
                    rating = table.Column<decimal>(type: "numeric(3,1)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_series", x => x.id);
                    table.ForeignKey(
                        name: "FK_series_content_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "content_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "series_episodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    series_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_number = table.Column<int>(type: "integer", nullable: false),
                    episode_number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    source = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    episode_type = table.Column<short>(type: "smallint", nullable: false),
                    imdb_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    plot = table.Column<string>(type: "text", nullable: true),
                    release_date = table.Column<DateOnly>(type: "date", nullable: true),
                    rating = table.Column<decimal>(type: "numeric(3,1)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_series_episodes", x => x.id);
                    table.ForeignKey(
                        name: "FK_series_episodes_series_series_id",
                        column: x => x.series_id,
                        principalTable: "series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_series_category_id_name",
                table: "series",
                columns: new[] { "category_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_series_rating",
                table: "series",
                column: "rating");

            migrationBuilder.CreateIndex(
                name: "IX_series_release_date",
                table: "series",
                column: "release_date");

            migrationBuilder.CreateIndex(
                name: "IX_series_status",
                table: "series",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_series_episodes_series_id_episode_type",
                table: "series_episodes",
                columns: new[] { "series_id", "episode_type" });

            migrationBuilder.CreateIndex(
                name: "IX_series_episodes_series_id_rating",
                table: "series_episodes",
                columns: new[] { "series_id", "rating" });

            migrationBuilder.CreateIndex(
                name: "IX_series_episodes_series_id_release_date",
                table: "series_episodes",
                columns: new[] { "series_id", "release_date" });

            migrationBuilder.CreateIndex(
                name: "IX_series_episodes_series_id_season_number_episode_number",
                table: "series_episodes",
                columns: new[] { "series_id", "season_number", "episode_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_series_episodes_series_id_status",
                table: "series_episodes",
                columns: new[] { "series_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "series_episodes");

            migrationBuilder.DropTable(
                name: "series");
        }
    }
}
