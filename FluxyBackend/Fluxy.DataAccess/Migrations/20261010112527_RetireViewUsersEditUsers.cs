using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <summary>
    /// Removes the grants of the two permissions this system retired.
    /// </summary>
    /// <remarks>
    /// Data only, and the scaffolding is empty on purpose: the model did not change, because
    /// <c>user_group_permissions.permission</c> is a <c>smallint</c> and the six new values were
    /// added in code, where the catalog lives. What the model cannot see is that two of the
    /// numbers in that column now mean nothing - <c>viewUsers</c> (1) and <c>editUsers</c> (2)
    /// were replaced by one permission per target role, and the new values deliberately start at
    /// 16 so that an old row can never be read as a new grant.
    ///
    /// Left alone, those rows would be inert: <c>UserGroupService</c> reads grants back by
    /// asking the catalog which members exist, so a bit the catalog does not list is dropped on
    /// the first read. They would still be written, counted in <c>permissions_count</c> and
    /// returned by the group endpoints, which is three different ways to say something that is
    /// not true. So they are deleted rather than left to rot.
    /// </remarks>
    public partial class RetireViewUsersEditUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The complement of the catalog rather than "permission IN (1, 2)": a row carrying
            // a bit no member has ever had is just as dead as the two retired ones, and this is
            // the one chance to remove it before the group endpoints start reporting counts.
            // Every value the catalog holds today is listed, so the rows this installation
            // actually uses survive - and any permission added later arrives after this
            // migration has already run, which is why a future value does not need a place in
            // the list.
            migrationBuilder.Sql(
                """
                DELETE FROM user_group_permissions
                WHERE permission NOT IN (4, 8, 16, 32, 64, 128, 256, 512);
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// There is nothing to restore, and that is a decision rather than an omission. The two
        /// retired grants cannot be put back: no code reads them any more, so a restored row
        /// would again be counted and again mean nothing. Their replacements are re-granted by
        /// the startup seeder, which reconciles the three base groups against the catalog -
        /// an installation whose Administrators group should hold
        /// <c>viewClients</c>...<c>editAdmins</c> gets them without anybody restating which
        /// rows those are. A custom group's old grants are gone with the data, which was agreed
        /// when the change was agreed: the owner's instruction was that the stored grants of
        /// the two old permissions are not worth keeping.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
