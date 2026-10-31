using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <summary>
    /// Creates the <c>blacklist_entries</c> table and grants the two blacklist permissions to
    /// the Administrators base group.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One table holds the entries of all three lists - IP, ISP and user-agent - because the
    /// entry has the same shape whichever list it belongs to and the kind is a column of the
    /// row. The unique index over <c>(type, value)</c> is named by hand rather than generated,
    /// because the repository matches a refused write against that exact name.
    /// </para>
    /// <para>
    /// The permission rows are data only: the two values were added in code, where the catalog
    /// lives, and the startup seeder reconciles the three base groups against it on every
    /// start. This migration is for the installation that has just been migrated and not yet
    /// restarted - without it the administrator holding the base group would open a blacklist
    /// and be refused by the permission gate, which reads exactly these rows and knows nothing
    /// about the catalog's intentions. The insert is guarded because the seeder may already
    /// have written the pair, and the unique index over (user_group_id, permission) would
    /// refuse a second copy and fail the migration. The identifiers and the group are literals
    /// because a migration is a historical record - it must keep meaning what it meant when it
    /// ran, not follow a constant that a later change might move.
    /// </para>
    /// </remarks>
    public partial class AddBlacklistEntries : Migration
    {
        /// <summary>The Administrators base group every administrator is a member of.</summary>
        private const string AdministratorsGroupId = "00000000-0000-0000-0000-000000000003";

        /// <summary><c>UserPermission.ViewBlacklists</c>.</summary>
        private const int ViewBlacklists = 67108864;

        /// <summary><c>UserPermission.EditBlacklists</c>.</summary>
        private const int EditBlacklists = 134217728;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blacklist_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<short>(type: "smallint", nullable: false),
                    value = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    blocked_attempts = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blacklist_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_blacklist_entries_type_created_at",
                table: "blacklist_entries",
                columns: new[] { "type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_blacklist_entries_type_value",
                table: "blacklist_entries",
                columns: new[] { "type", "value" },
                unique: true);

            migrationBuilder.Sql(
                $"""
                INSERT INTO user_group_permissions (id, user_group_id, permission, created_at, updated_at)
                SELECT seeded.id, seeded.group_id, seeded.permission, now(), now()
                FROM (VALUES
                    ('00000000-0000-0000-0000-000000000221'::uuid, '{AdministratorsGroupId}'::uuid, {ViewBlacklists}),
                    ('00000000-0000-0000-0000-000000000222'::uuid, '{AdministratorsGroupId}'::uuid, {EditBlacklists})
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
        /// The table and the two grants go together. A permission the catalog no longer defines
        /// would be a row nothing can name, counted by the group endpoints and dropped by the
        /// first read that asks the catalog about it.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                DELETE FROM user_group_permissions
                WHERE user_group_id = '{AdministratorsGroupId}'
                  AND permission IN ({ViewBlacklists}, {EditBlacklists});
                """);

            migrationBuilder.DropTable(
                name: "blacklist_entries");
        }
    }
}
