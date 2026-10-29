using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    playlist_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    expire_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tariff_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    max_connections = table.Column<int>(type: "integer", nullable: true),
                    is_restreamer = table.Column<bool>(type: "boolean", nullable: false),
                    ip_whitelist = table.Column<string[]>(type: "text[]", nullable: false),
                    user_agent_whitelist = table.Column<string[]>(type: "text[]", nullable: false),
                    is_isp_lock = table.Column<bool>(type: "boolean", nullable: false),
                    is_country_lock = table.Column<bool>(type: "boolean", nullable: false),
                    is_trial = table.Column<bool>(type: "boolean", nullable: false),
                    reseller_id = table.Column<Guid>(type: "uuid", nullable: true),
                    output_formats = table.Column<short>(type: "smallint", nullable: false),
                    mag_mac = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: true),
                    is_stb_lock = table.Column<bool>(type: "boolean", nullable: false),
                    enigma2_mac = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: true),
                    play_token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriptions", x => x.id);
                    table.CheckConstraint("ck_subscriptions_max_connections", "max_connections IS NULL OR max_connections BETWEEN 1 AND 10000");
                    table.CheckConstraint("ck_subscriptions_output_formats", "output_formats <> 0");
                    table.ForeignKey(
                        name: "FK_subscriptions_tariff_plans_tariff_plan_id",
                        column: x => x.tariff_plan_id,
                        principalTable: "tariff_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_subscriptions_users_reseller_id",
                        column: x => x.reseller_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_subscriptions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscription_bouquets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bouquet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_bouquets", x => x.id);
                    table.ForeignKey(
                        name: "FK_subscription_bouquets_bouquets_bouquet_id",
                        column: x => x.bouquet_id,
                        principalTable: "bouquets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subscription_bouquets_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subscription_bouquets_bouquet_id",
                table: "subscription_bouquets",
                column: "bouquet_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscription_bouquets_subscription_id_bouquet_id",
                table: "subscription_bouquets",
                columns: new[] { "subscription_id", "bouquet_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_created_at",
                table: "subscriptions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_expire_at",
                table: "subscriptions",
                column: "expire_at");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_reseller_id",
                table: "subscriptions",
                column: "reseller_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_tariff_plan_id",
                table: "subscriptions",
                column: "tariff_plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_user_id",
                table: "subscriptions",
                column: "user_id");

            // The three uniqueness rules are partial indexes, which EF Core's model cannot
            // express: a playlist id and either MAC are unique across subscriptions *when they
            // are present*, and a subscription with no playlist or no device is the ordinary
            // case rather than a collision. Each is created here under the fixed name the
            // service matches a unique violation against, which is what lets a duplicate be
            // answered as `subscription_already_exists` naming the field instead of as a
            // provider error.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_subscriptions_playlist_id\" " +
                "ON subscriptions (playlist_id) WHERE playlist_id IS NOT NULL;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_subscriptions_mag_mac\" " +
                "ON subscriptions (mag_mac) WHERE mag_mac IS NOT NULL;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_subscriptions_enigma2_mac\" " +
                "ON subscriptions (enigma2_mac) WHERE enigma2_mac IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_subscriptions_enigma2_mac\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_subscriptions_mag_mac\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_subscriptions_playlist_id\";");

            migrationBuilder.DropTable(
                name: "subscription_bouquets");

            migrationBuilder.DropTable(
                name: "subscriptions");
        }
    }
}
