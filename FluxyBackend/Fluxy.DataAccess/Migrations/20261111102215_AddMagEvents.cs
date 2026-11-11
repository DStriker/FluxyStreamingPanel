using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddMagEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mag_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<short>(type: "smallint", nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    need_confirm = table.Column<bool>(type: "boolean", nullable: false),
                    reboot_after_ok = table.Column<bool>(type: "boolean", nullable: false),
                    auto_hide_timeout = table.Column<bool>(type: "boolean", nullable: false),
                    additional_services_on = table.Column<bool>(type: "boolean", nullable: false),
                    anec = table.Column<bool>(type: "boolean", nullable: false),
                    vclub = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mag_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_mag_events_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mag_events_created_at_id",
                table: "mag_events",
                columns: new[] { "created_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_mag_events_subscription_id_created_at",
                table: "mag_events",
                columns: new[] { "subscription_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mag_events");
        }
    }
}
