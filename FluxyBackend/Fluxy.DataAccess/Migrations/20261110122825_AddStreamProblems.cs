using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddStreamProblems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stream_problems",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stream_id = table.Column<Guid>(type: "uuid", nullable: false),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stream_problems", x => x.id);
                    table.ForeignKey(
                        name: "FK_stream_problems_video_streams_stream_id",
                        column: x => x.stream_id,
                        principalTable: "video_streams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stream_problems_created_at_id",
                table: "stream_problems",
                columns: new[] { "created_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_stream_problems_stream_id_created_at",
                table: "stream_problems",
                columns: new[] { "stream_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stream_problems");
        }
    }
}
