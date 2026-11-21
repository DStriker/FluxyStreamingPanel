using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <summary>
    /// Creates <c>application_settings</c> and <c>mag_settings</c>, and seeds the single row each
    /// installation starts with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both tables hold exactly one row. The migration writes them with the values
    /// <c>ApplicationSettingsDefaults</c> and <c>MagSettingsDefaults</c> declare, because the
    /// project forbids a database default: a column the application forgot would be invented by
    /// the provider rather than noticed. The startup seeder is the second guard, for an
    /// installation whose rows were removed by hand and for the integration fixture that
    /// truncates the database.
    /// </para>
    /// <para>
    /// Each insert is guarded by <c>WHERE NOT EXISTS</c>, so re-running the statement - or a
    /// database where the startup seeder wrote the row first - cannot produce a duplicate or fail
    /// the migration. The identifiers are literals because a migration is a historical record:
    /// it must keep naming the rows it inserted even if a later change moves a constant.
    /// </para>
    /// </remarks>
    public partial class AddApplicationSettings : Migration
    {
        /// <summary>Identifier of the application settings row.</summary>
        private const string ApplicationSettingsId = "00000000-0000-0000-0000-000000000501";

        /// <summary>Identifier of the MAG settings row.</summary>
        private const string MagSettingsId = "00000000-0000-0000-0000-000000000502";

        /// <summary><c>BackupPeriod.Daily</c>.</summary>
        private const short BackupPeriodDaily = 1;

        /// <summary><c>MagContainer.MpegTs</c>.</summary>
        private const short MagContainerMpegTs = 1;

        /// <summary><c>MagTheme.Default</c>.</summary>
        private const short MagThemeDefault = 0;

        /// <summary>
        /// Every set-top-box model the installation knows, spelled as the catalogue spells them
        /// and in its order.
        /// </summary>
        private const string AllStbTypes =
            "{\"Aura HD\",\"Aura HD2\",\"Aura HD3\",\"Aura HD4\",\"Aura HD5\",\"Aura HD6\"," +
            "\"Aura HD7\",\"Aura HD8\",\"Aura HD9\",\"MAG200\",\"MAG245\",\"MAG245D\",\"MAG250\"," +
            "\"MAG254\",\"MAG255\",\"MAG256\",\"MAG257\",\"MAG260\",\"MAG270\",\"MAG275\"," +
            "\"MAG322\",\"MAG323\",\"MAG324\",\"MAG325\",\"MAG349\",\"MAG350\",\"MAG351\"," +
            "\"MAG352\",\"MAG420\",\"MAG520\",\"WR320\"}";

        /// <summary>The three models allowed to record.</summary>
        private const string RecordingStbTypes = "{MAG255,MAG256,MAG257}";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "application_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    default_timezone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    enigma2_bouquet_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    recaptcha_site_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    recaptcha_secret_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    enable_trials = table.Column<bool>(type: "boolean", nullable: false),
                    public_api_flood_limit = table.Column<int>(type: "integer", nullable: false),
                    public_api_flood_whitelist = table.Column<string[]>(type: "text[]", nullable: false),
                    require_user_agent = table.Column<bool>(type: "boolean", nullable: false),
                    retention_user_logs = table.Column<int>(type: "integer", nullable: false),
                    retention_credit_logs = table.Column<int>(type: "integer", nullable: false),
                    retention_connection_logs = table.Column<int>(type: "integer", nullable: false),
                    retention_api_request_failures = table.Column<int>(type: "integer", nullable: false),
                    retention_stream_problems = table.Column<int>(type: "integer", nullable: false),
                    retention_mag_events = table.Column<int>(type: "integer", nullable: false),
                    backup_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    backup_period = table.Column<short>(type: "smallint", nullable: false),
                    backup_rotation_count = table.Column<int>(type: "integer", nullable: false),
                    fallback_stream_down_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    fallback_user_banned_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    fallback_user_expired_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    fallback_max_connections_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mag_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    show_all_categories = table.Column<bool>(type: "boolean", nullable: false),
                    strict_security = table.Column<bool>(type: "boolean", nullable: false),
                    show_channel_logos = table.Column<bool>(type: "boolean", nullable: false),
                    show_preview_logos = table.Column<bool>(type: "boolean", nullable: false),
                    default_container = table.Column<short>(type: "smallint", nullable: false),
                    default_theme = table.Column<short>(type: "smallint", nullable: false),
                    recording_max_length_minutes = table.Column<int>(type: "integer", nullable: false),
                    max_local_recordings = table.Column<int>(type: "integer", nullable: false),
                    playback_limit = table.Column<int>(type: "integer", nullable: false),
                    allowed_stb_types = table.Column<string[]>(type: "text[]", nullable: false),
                    allowed_recording_stb_types = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mag_settings", x => x.id);
                });

            // The one application settings row a fresh installation starts with. The reCAPTCHA
            // pair is empty: the check is deliberately disabled until an operator configures it,
            // and the startup seeder copies a configured secret into the row only on its first
            // creation.
            migrationBuilder.Sql(
                $"""
                INSERT INTO application_settings (
                    id, service_name, default_timezone, enigma2_bouquet_name,
                    recaptcha_site_key, recaptcha_secret_key, enable_trials,
                    public_api_flood_limit, public_api_flood_whitelist, require_user_agent,
                    retention_user_logs, retention_credit_logs, retention_connection_logs,
                    retention_api_request_failures, retention_stream_problems,
                    retention_mag_events, backup_enabled, backup_period, backup_rotation_count,
                    fallback_stream_down_url, fallback_user_banned_url,
                    fallback_user_expired_url, fallback_max_connections_url,
                    created_at, updated_at)
                SELECT
                    '{ApplicationSettingsId}'::uuid, 'Fluxy Streaming panel', 'Europe/Berlin',
                    'Fluxy IPTV',
                    '', '', TRUE,
                    20, ARRAY[]::text[], FALSE,
                    30, 30, 30,
                    30, 30,
                    30, FALSE, {BackupPeriodDaily}, 10,
                    '', '',
                    '', '',
                    now(), now()
                WHERE NOT EXISTS (
                    SELECT 1 FROM application_settings
                    WHERE id = '{ApplicationSettingsId}'::uuid
                );
                """);

            // The one MAG settings row. Every model is allowed to connect; only the three that
            // can record are allowed to record.
            migrationBuilder.Sql(
                $"""
                INSERT INTO mag_settings (
                    id, show_all_categories, strict_security, show_channel_logos,
                    show_preview_logos, default_container, default_theme,
                    recording_max_length_minutes, max_local_recordings, playback_limit,
                    allowed_stb_types, allowed_recording_stb_types, created_at, updated_at)
                SELECT
                    '{MagSettingsId}'::uuid, TRUE, TRUE, TRUE,
                    TRUE, {MagContainerMpegTs}, {MagThemeDefault},
                    180, 10, 3,
                    '{AllStbTypes}'::text[], '{RecordingStbTypes}'::text[], now(), now()
                WHERE NOT EXISTS (
                    SELECT 1 FROM mag_settings
                    WHERE id = '{MagSettingsId}'::uuid
                );
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Both tables go with their seeded rows. No permission rows are touched: the pair reaches
        /// the Administrators base group through the catalog seeder on the next boot, and the
        /// separate <c>GrantSettingsPermissions</c> migration covers an installation that has
        /// migrated without restarting.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_settings");

            migrationBuilder.DropTable(
                name: "mag_settings");
        }
    }
}
