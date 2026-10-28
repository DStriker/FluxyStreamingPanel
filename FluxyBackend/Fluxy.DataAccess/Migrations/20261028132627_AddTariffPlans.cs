using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTariffPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tariff_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_trial = table.Column<bool>(type: "boolean", nullable: false),
                    cost = table.Column<int>(type: "integer", nullable: false),
                    duration_value = table.Column<int>(type: "integer", nullable: false),
                    duration_unit = table.Column<short>(type: "smallint", nullable: false),
                    supports_m3u = table.Column<bool>(type: "boolean", nullable: false),
                    supports_mag = table.Column<bool>(type: "boolean", nullable: false),
                    supports_enigma2 = table.Column<bool>(type: "boolean", nullable: false),
                    output_mpeg_ts = table.Column<bool>(type: "boolean", nullable: false),
                    output_hls = table.Column<bool>(type: "boolean", nullable: false),
                    strict_stb_device_id = table.Column<bool>(type: "boolean", nullable: false),
                    allow_restream = table.Column<bool>(type: "boolean", nullable: false),
                    max_connections = table.Column<int>(type: "integer", nullable: false),
                    allow_multiple_countries = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tariff_plans", x => x.id);
                    table.CheckConstraint("ck_tariff_plans_cost", "cost >= 0");
                    table.CheckConstraint("ck_tariff_plans_duration_value", "duration_value >= 1");
                    table.CheckConstraint("ck_tariff_plans_max_connections", "max_connections BETWEEN 1 AND 10000");
                });

            migrationBuilder.CreateTable(
                name: "tariff_plan_bouquets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tariff_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bouquet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tariff_plan_bouquets", x => x.id);
                    table.ForeignKey(
                        name: "FK_tariff_plan_bouquets_bouquets_bouquet_id",
                        column: x => x.bouquet_id,
                        principalTable: "bouquets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tariff_plan_bouquets_tariff_plans_tariff_plan_id",
                        column: x => x.tariff_plan_id,
                        principalTable: "tariff_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tariff_plan_user_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tariff_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tariff_plan_user_groups", x => x.id);
                    table.ForeignKey(
                        name: "FK_tariff_plan_user_groups_tariff_plans_tariff_plan_id",
                        column: x => x.tariff_plan_id,
                        principalTable: "tariff_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tariff_plan_user_groups_user_groups_user_group_id",
                        column: x => x.user_group_id,
                        principalTable: "user_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tariff_plan_bouquets_bouquet_id",
                table: "tariff_plan_bouquets",
                column: "bouquet_id");

            migrationBuilder.CreateIndex(
                name: "IX_tariff_plan_bouquets_tariff_plan_id_bouquet_id",
                table: "tariff_plan_bouquets",
                columns: new[] { "tariff_plan_id", "bouquet_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tariff_plan_user_groups_tariff_plan_id_user_group_id",
                table: "tariff_plan_user_groups",
                columns: new[] { "tariff_plan_id", "user_group_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tariff_plan_user_groups_user_group_id",
                table: "tariff_plan_user_groups",
                column: "user_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_tariff_plans_cost",
                table: "tariff_plans",
                column: "cost");

            migrationBuilder.CreateIndex(
                name: "IX_tariff_plans_created_at",
                table: "tariff_plans",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_tariff_plans_name",
                table: "tariff_plans",
                column: "name");

            // The rule the table exists to state: two plans whose names differ only in case are
            // one plan. EF Core cannot express an expression index over lower(name) as a model
            // index, so it is created here by raw SQL under the fixed name the service matches a
            // unique violation against. The plain index on name above stays because a case
            // sensitive prefix search uses it; this one is what refuses a duplicate spelling.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_tariff_plans_name_lower\" ON tariff_plans (lower(name));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_tariff_plans_name_lower\";");

            migrationBuilder.DropTable(
                name: "tariff_plan_bouquets");

            migrationBuilder.DropTable(
                name: "tariff_plan_user_groups");

            migrationBuilder.DropTable(
                name: "tariff_plans");
        }
    }
}
