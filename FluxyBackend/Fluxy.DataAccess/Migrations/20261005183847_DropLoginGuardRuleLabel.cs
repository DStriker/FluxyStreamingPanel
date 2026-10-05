using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class DropLoginGuardRuleLabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "label",
                table: "user_login_guard_rules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "label",
                table: "user_login_guard_rules",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }
    }
}
