using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <summary>
    /// Widens <c>user_group_permissions.permission</c> from <c>integer</c> to <c>bigint</c>.
    /// </summary>
    /// <remarks>
    /// The transcoding permission pair needs <c>1 &lt;&lt; 30</c> and <c>1 &lt;&lt; 31</c>, and an
    /// <see cref="int"/> holds only the first of those. The widening is lossless for every value
    /// already stored - no row is rewritten with a different number - so no grant is
    /// reinterpreted. Data loss could only appear on <c>Down</c>, if a 64-bit grant had already
    /// been written; that is why the comment on the reversal is a warning rather than a promise.
    /// </remarks>
    public partial class WidenUserGroupPermissionToBigint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "permission",
                table: "user_group_permissions",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "permission",
                table: "user_group_permissions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
