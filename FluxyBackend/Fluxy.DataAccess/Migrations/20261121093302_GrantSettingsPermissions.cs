using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <summary>
    /// Grants the two settings permissions to the Administrators base group.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Data only, and the model is untouched: <c>user_group_permissions.permission</c> is a
    /// <c>bigint</c> and the two new values were added in code, where the catalog lives. The
    /// startup seeder reconciles the three base groups against that catalog on every start, so an
    /// installation that has already been restarted has these rows. This migration is for the one
    /// that has just been migrated and not yet restarted: without it an administrator holding the
    /// base group would open the settings screen and be refused by the permission gate, which
    /// reads exactly these rows and knows nothing about the catalog's intentions.
    /// </para>
    /// <para>
    /// The insert is guarded rather than written as a plain <c>INSERT</c>: the seeder may already
    /// have written the pair, and the unique index over (user_group_id, permission) would refuse a
    /// second copy and fail the migration. The identifiers and the group are literals because a
    /// migration is a historical record - it must keep meaning what it meant when it ran.
    /// </para>
    /// <para>
    /// <b>The row identifiers are <c>...231</c> and <c>...232</c>, not the <c>...221</c> /
    /// <c>...222</c> pair named in the specification.</b> Those two identifiers were already used
    /// by <c>AddBlacklistEntries</c> for its own pair of permission rows, and a primary key is
    /// installation-wide: reusing them would fail every fresh migration with
    /// <c>23505 duplicate key value violates unique constraint "PK_user_group_permissions"</c>.
    /// The permission values and the group are exactly the specified ones; only the row
    /// identifiers moved to the next free pair.
    /// </para>
    /// </remarks>
    public partial class GrantSettingsPermissions : Migration
    {
        /// <summary>The Administrators base group every administrator is a member of.</summary>
        private const string AdministratorsGroupId = "00000000-0000-0000-0000-000000000003";

        /// <summary><c>UserPermission.ViewSettings</c>.</summary>
        private const long ViewSettings = 17179869184;

        /// <summary><c>UserPermission.EditSettings</c>.</summary>
        private const long EditSettings = 34359738368;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO user_group_permissions (id, user_group_id, permission, created_at, updated_at)
                SELECT seeded.id, seeded.group_id, seeded.permission, now(), now()
                FROM (VALUES
                    ('00000000-0000-0000-0000-000000000231'::uuid, '00000000-0000-0000-0000-000000000003'::uuid, 17179869184::bigint),
                    ('00000000-0000-0000-0000-000000000232'::uuid, '00000000-0000-0000-0000-000000000003'::uuid, 34359738368::bigint)
                ) AS seeded(id, group_id, permission)
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM user_group_permissions existing
                    WHERE existing.user_group_id = seeded.group_id
                      AND existing.permission = seeded.permission
                );
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// The two grants are removed, and that is the whole of the reversal: a permission the
        /// catalog no longer defines would be a row nothing can name, counted by the group
        /// endpoints and dropped by the first read that asks the catalog about it.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                DELETE FROM user_group_permissions
                WHERE user_group_id = '{AdministratorsGroupId}'
                  AND permission IN ({ViewSettings}, {EditSettings});
                """);
        }
    }
}
