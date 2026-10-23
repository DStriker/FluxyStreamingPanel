using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class DropVideoStreamSortOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_video_streams_category_id_sort_order",
                table: "video_streams");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "video_streams");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "video_streams",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_video_streams_category_id_sort_order",
                table: "video_streams",
                columns: new[] { "category_id", "sort_order" });
        }
    }
}
