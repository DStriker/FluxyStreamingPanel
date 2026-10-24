using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class WidenUserGroupPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "permission",
                table: "user_group_permissions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<short>(
                name: "permission",
                table: "user_group_permissions",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
