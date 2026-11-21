using Fluxy.Core.Settings.Enums;

namespace Fluxy.Core.Settings.Models
{
    /// <summary>
    /// The installation-wide settings an operator owns: the panel's name, the reCAPTCHA pair, the
    /// trial switch, the public-API flood bounds, the retention windows, the backup schedule and
    /// the four fallback addresses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One row per installation, carrying the fixed identifier of
    /// <see cref="Utils.ApplicationSettingsDefaults.Id"/>. The row is seeded by the startup
    /// seeder and fetched by the repository; a missing row is answered from the defaults statics
    /// so that an installation whose row was removed by hand still serves every default.
    /// </para>
    /// <para>
    /// <b>The private reCAPTCHA key is part of this model.</b> It is stored and never answered by
    /// the admin response - that one reports <c>recaptchaConfigured</c> instead - but the server
    /// itself needs the value, so it travels in the snapshot.
    /// </para>
    /// </remarks>
    public sealed record ApplicationSettings
    {
        /// <summary>Identifier of the single row.</summary>
        public required Guid Id { get; init; }

        /// <summary>Name the panel presents itself with, in email subjects and elsewhere.</summary>
        public required string ServiceName { get; init; }

        /// <summary>IANA identifier of the installation's display time zone.</summary>
        public required string DefaultTimeZone { get; init; }

        /// <summary>Name of the Enigma2 bouquet a producer would write.</summary>
        public required string Enigma2BouquetName { get; init; }

        /// <summary>Public reCAPTCHA site key handed to the browser; empty when not configured.</summary>
        public required string RecaptchaSiteKey { get; init; }

        /// <summary>Private reCAPTCHA key; empty when the check is disabled.</summary>
        public required string RecaptchaSecretKey { get; init; }

        /// <summary>Whether a subscription may be issued as a trial.</summary>
        public required bool EnableTrials { get; init; }

        /// <summary>Requests one public-API client may make in a window; <c>0</c> means unlimited.</summary>
        public required int PublicApiFloodLimit { get; init; }

        /// <summary>Addresses exempt from the public-API flood limit.</summary>
        public required string[] PublicApiFloodWhitelist { get; init; }

        /// <summary>Whether a public-API request has to present a user agent.</summary>
        public required bool RequireUserAgent { get; init; }

        /// <summary>Days a user log row is kept; <c>0</c> is not a valid value.</summary>
        public required int RetentionUserLogs { get; init; }

        /// <summary>Days a credit log row is kept.</summary>
        public required int RetentionCreditLogs { get; init; }

        /// <summary>Days a connection log row is kept.</summary>
        public required int RetentionConnectionLogs { get; init; }

        /// <summary>Days a refused-request row is kept.</summary>
        public required int RetentionApiRequestFailures { get; init; }

        /// <summary>Days a reported stream problem is kept.</summary>
        public required int RetentionStreamProblems { get; init; }

        /// <summary>Days a MAG event row is kept.</summary>
        public required int RetentionMagEvents { get; init; }

        /// <summary>Whether a scheduled backup would run.</summary>
        public required bool BackupEnabled { get; init; }

        /// <summary>How often the backup would run.</summary>
        public required BackupPeriod BackupPeriod { get; init; }

        /// <summary>How many backups are kept; <c>0</c> means infinite.</summary>
        public required int BackupRotationCount { get; init; }

        /// <summary>Address shown when the stream is down; empty for none.</summary>
        public required string FallbackStreamDownUrl { get; init; }

        /// <summary>Address shown when the viewer is banned; empty for none.</summary>
        public required string FallbackUserBannedUrl { get; init; }

        /// <summary>Address shown when the viewer's subscription expired; empty for none.</summary>
        public required string FallbackUserExpiredUrl { get; init; }

        /// <summary>Address shown when the viewer's connection ceiling is reached; empty for none.</summary>
        public required string FallbackMaxConnectionsUrl { get; init; }
    }
}
