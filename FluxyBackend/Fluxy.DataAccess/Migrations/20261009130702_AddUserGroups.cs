using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <summary>
    /// Replaces the role column on <c>users</c> with a group, and creates the three groups the
    /// installation cannot operate without.
    /// </summary>
    /// <remarks>
    /// EF scaffolds this as "drop the role column, then add a group id", which is the right
    /// answer for an empty table and the wrong one for a real one: every account would lose its
    /// level before anything existed to point at. So the order below is the reverse of that on
    /// the way in - the tables first, then the three base groups, then the column, then the
    /// backfill, and only then the old column and the constraint.
    ///
    /// The backfill updates the three levels in three separate statements rather than with a
    /// <c>CASE</c> that falls back to a default. A level outside Client, Reseller and Admin
    /// cannot be mapped onto a group without inventing one, and the
    /// <c>ALTER COLUMN ... SET NOT NULL</c> that follows is what reports it - loudly, and
    /// before the drop - rather than quietly moving a row somewhere it was never meant to be.
    /// The same rule the model follows about defaults: a value the database invents when the
    /// application does not know is how a mistake becomes a fact.
    ///
    /// The base groups are written here rather than left to the startup seeder for one reason:
    /// the backfill has to have something to point at, and the seeder runs after migrations.
    /// It is idempotent anyway and reconciles them on every start, so the two never disagree
    /// - this migration is what makes the first run possible, not what keeps them alive.
    /// </remarks>
    public partial class AddUserGroups : Migration
    {
        /// <summary>The group every client is a member of by default.</summary>
        private const string ClientsId = "00000000-0000-0000-0000-000000000001";

        /// <summary>The group every reseller is a member of by default.</summary>
        private const string ResellersId = "00000000-0000-0000-0000-000000000002";

        /// <summary>The group every administrator is a member of by default.</summary>
        private const string AdministratorsId = "00000000-0000-0000-0000-000000000003";

        /// <summary>Registered, the state a group starts in.</summary>
        private const short Registered = 1;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    role = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_group_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission = table.Column<short>(type: "smallint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_group_permissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_group_permissions_user_groups_user_group_id",
                        column: x => x.user_group_id,
                        principalTable: "user_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_groups_name",
                table: "user_groups",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_groups_role",
                table: "user_groups",
                column: "role");

            migrationBuilder.CreateIndex(
                name: "IX_user_group_permissions_user_group_id_permission",
                table: "user_group_permissions",
                columns: new[] { "user_group_id", "permission" },
                unique: true);

            // The three rows the backfill below depends on. Their identifiers are constants
            // rather than values this statement invents, so the seeder, the service that
            // refuses to delete them and this migration all name the same three rows without
            // ever having seen each other.
            migrationBuilder.Sql($@"
INSERT INTO user_groups (id, name, role, status, created_at, updated_at)
VALUES
    ('{ClientsId}', 'Clients', 1, {Registered}, now(), now()),
    ('{ResellersId}', 'Resellers', 2, {Registered}, now(), now()),
    ('{AdministratorsId}', 'Administrators', 3, {Registered}, now(), now());");

            // Only the administrator base group carries permissions, and it carries all four,
            // because a permission belongs to the role that owns it and no other role owns one
            // yet. Written here so that a fresh installation is usable before the first
            // startup reconciliation runs; that reconciliation keeps them in step with the
            // catalog afterwards, in both directions.
            migrationBuilder.Sql($@"
INSERT INTO user_group_permissions (id, user_group_id, permission, created_at, updated_at)
VALUES
    ('00000000-0000-0000-0000-000000000101', '{AdministratorsId}', 1, now(), now()),
    ('00000000-0000-0000-0000-000000000102', '{AdministratorsId}', 2, now(), now()),
    ('00000000-0000-0000-0000-000000000103', '{AdministratorsId}', 4, now(), now()),
    ('00000000-0000-0000-0000-000000000104', '{AdministratorsId}', 8, now(), now());");

            // Added nullable, and narrowed to NOT NULL only once every row has a value: a
            // constraint that arrives first would refuse the very backfill that satisfies it.
            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql($"UPDATE users SET group_id = '{ClientsId}' WHERE role = 1;");
            migrationBuilder.Sql($"UPDATE users SET group_id = '{ResellersId}' WHERE role = 2;");
            migrationBuilder.Sql($"UPDATE users SET group_id = '{AdministratorsId}' WHERE role = 3;");

            migrationBuilder.AlterColumn<Guid>(
                name: "group_id",
                table: "users",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "role",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "IX_users_group_id",
                table: "users",
                column: "group_id");

            migrationBuilder.AddForeignKey(
                name: "FK_users_user_groups_group_id",
                table: "users",
                column: "group_id",
                principalTable: "user_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Reads the level back out of the group before the group is dropped, for the same
        /// reason <see cref="Up"/> does it in the other order: the two columns never coexist
        /// as a fact, only as a statement about one, and the statement has to be made while
        /// the column it reads is still there. The level comes back nullable and is narrowed
        /// afterwards, so an account whose group names no level is refused by the constraint
        /// instead of being given one.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_user_groups_group_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_group_id",
                table: "users");

            migrationBuilder.AddColumn<short>(
                name: "role",
                table: "users",
                type: "smallint",
                nullable: true);

            migrationBuilder.Sql($@"
UPDATE users SET role = CASE group_id
    WHEN '{ClientsId}' THEN 1
    WHEN '{ResellersId}' THEN 2
    WHEN '{AdministratorsId}' THEN 3
END;");

            migrationBuilder.AlterColumn<short>(
                name: "role",
                table: "users",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "user_group_permissions");

            migrationBuilder.DropTable(
                name: "user_groups");
        }
    }
}
