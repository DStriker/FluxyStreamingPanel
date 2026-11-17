using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxy.DataAccess.Migrations
{
    /// <summary>
    /// Creates the <c>transcoding_profiles</c> table and seeds the profile every installation
    /// starts with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The table is a bag of optional ffmpeg options around a name. Every option column is
    /// nullable on purpose: null is the state "do not pass this flag to ffmpeg", not a missing
    /// value. The three columns a row always has are the two codecs and the preset, and the
    /// service writes their do-nothing members when a create names none.
    /// </para>
    /// <para>
    /// The seed is data only and guarded, so a second run cannot insert a duplicate and a
    /// failed run leaves nothing behind: "Standard H264 AAC" is the profile an operator reaches
    /// for first, and a fresh installation that had no profile at all would make every content
    /// form's picker look broken. It is <b>not</b> a startup seeder - the row is written once by
    /// this migration, and from then on it is an ordinary profile that may be renamed or
    /// deleted like any other.
    /// </para>
    /// </remarks>
    public partial class AddTranscodingProfiles : Migration
    {
        /// <summary>
        /// Identifier of the seeded profile. A literal rather than a constant read from the
        /// application, because a migration is a historical record: it must keep naming the row
        /// it inserted even if a later change moves a constant.
        /// </summary>
        private const string StandardProfileId = "00000000-0000-0000-0000-000000000301";

        /// <summary><c>VideoCodec.Libx264</c>.</summary>
        private const short VideoCodecLibx264 = 1;

        /// <summary><c>AudioCodec.Aac</c>.</summary>
        private const short AudioCodecAac = 1;

        /// <summary><c>TranscodingPreset.Default</c>.</summary>
        private const short PresetDefault = 0;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transcoding_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    video_codec = table.Column<short>(type: "smallint", nullable: false),
                    audio_codec = table.Column<short>(type: "smallint", nullable: false),
                    preset = table.Column<short>(type: "smallint", nullable: false),
                    video_profile = table.Column<short>(type: "smallint", nullable: true),
                    video_bitrate_avg = table.Column<int>(type: "integer", nullable: true),
                    audio_bitrate_avg = table.Column<int>(type: "integer", nullable: true),
                    min_bitrate_tolerance = table.Column<int>(type: "integer", nullable: true),
                    max_bitrate_tolerance = table.Column<int>(type: "integer", nullable: true),
                    buffer_size = table.Column<int>(type: "integer", nullable: true),
                    crf = table.Column<int>(type: "integer", nullable: true),
                    width = table.Column<int>(type: "integer", nullable: true),
                    height = table.Column<int>(type: "integer", nullable: true),
                    aspect_ratio = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    framerate = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: true),
                    audio_sample_rate = table.Column<int>(type: "integer", nullable: true),
                    audio_channels = table.Column<int>(type: "integer", nullable: true),
                    threads = table.Column<int>(type: "integer", nullable: true),
                    logo_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    redaction_x = table.Column<int>(type: "integer", nullable: true),
                    redaction_y = table.Column<int>(type: "integer", nullable: true),
                    redaction_width = table.Column<int>(type: "integer", nullable: true),
                    redaction_height = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transcoding_profiles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transcoding_profiles_name",
                table: "transcoding_profiles",
                column: "name",
                unique: true);

            // The one profile a fresh installation starts with. Guarded by NOT EXISTS so the
            // statement is idempotent against a database where the row somehow already exists -
            // and the unique index over `name` would refuse a second copy and fail the whole
            // migration, taking the table with it.
            migrationBuilder.Sql(
                $"""
                INSERT INTO transcoding_profiles (
                    id, name, video_codec, audio_codec, preset, video_profile,
                    video_bitrate_avg, audio_bitrate_avg, min_bitrate_tolerance,
                    max_bitrate_tolerance, buffer_size, crf, width, height, aspect_ratio,
                    framerate, audio_sample_rate, audio_channels, threads, logo_url,
                    redaction_x, redaction_y, redaction_width, redaction_height,
                    created_at, updated_at)
                SELECT
                    '{StandardProfileId}'::uuid, 'Standard H264 AAC', {VideoCodecLibx264},
                    {AudioCodecAac}, {PresetDefault}, NULL,
                    NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
                    NULL, NULL, NULL, NULL, NULL,
                    NULL, NULL, NULL, NULL,
                    now(), now()
                WHERE NOT EXISTS (
                    SELECT 1 FROM transcoding_profiles WHERE name = 'Standard H264 AAC'
                );
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// The table goes with the seed. No permission rows are touched: the pair reaches the
        /// Administrators base group through the catalog seeder on the next boot, which is the
        /// mechanism every permission addition uses.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transcoding_profiles");
        }
    }
}
