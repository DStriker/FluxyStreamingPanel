using Fluxy.Core.Settings.Enums;
using Fluxy.Core.Settings.Models;

namespace Fluxy.Core.Settings.Utils
{
    /// <summary>
    /// The values a fresh installation starts with, and the answer whenever the settings row is
    /// absent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This table is mandatory rather than a convenience.</b> The migration seeds the row and
    /// the startup seeder creates it if it was removed, but the integration fixture truncates the
    /// database and re-seeds only the base groups and the main server - so a repository that
    /// returned null would leave a test (and an installation whose row was deleted by hand)
    /// without the defaults the contract promises. The provider asks this table instead.
    /// </para>
    /// <para>
    /// The identifier is fixed so that the migration, the seeder and any later read all name the
    /// same single row. <c>HasDefaultValue</c> is used nowhere in this project, which is why
    /// every value is stated here in code and written explicitly on insert.
    /// </para>
    /// </remarks>
    public static class ApplicationSettingsDefaults
    {
        /// <summary>Identifier of the single settings row, the same on every installation.</summary>
        public static readonly Guid Id = new("00000000-0000-0000-0000-000000000501");

        /// <summary>Name the panel presents itself with.</summary>
        public const string ServiceName = "Fluxy Streaming panel";

        /// <summary>IANA identifier of the default display time zone.</summary>
        public const string DefaultTimeZone = "Europe/Berlin";

        /// <summary>Name of the Enigma2 bouquet.</summary>
        public const string Enigma2BouquetName = "Fluxy IPTV";

        /// <summary>Empty site key: reCAPTCHA is not configured until an operator sets it.</summary>
        public const string RecaptchaSiteKey = "";

        /// <summary>Empty private key: reCAPTCHA is skipped until an operator sets one.</summary>
        public const string RecaptchaSecretKey = "";

        /// <summary>Whether a subscription may be issued as a trial.</summary>
        public const bool EnableTrials = true;

        /// <summary>Requests one public-API client may make in a window.</summary>
        public const int PublicApiFloodLimit = 20;

        /// <summary>Whether a public-API request has to present a user agent.</summary>
        public const bool RequireUserAgent = false;

        /// <summary>Days a user log row is kept.</summary>
        public const int RetentionUserLogs = 30;

        /// <summary>Days a credit log row is kept.</summary>
        public const int RetentionCreditLogs = 30;

        /// <summary>Days a connection log row is kept.</summary>
        public const int RetentionConnectionLogs = 30;

        /// <summary>Days a refused-request row is kept.</summary>
        public const int RetentionApiRequestFailures = 30;

        /// <summary>Days a reported stream problem is kept.</summary>
        public const int RetentionStreamProblems = 30;

        /// <summary>Days a MAG event row is kept.</summary>
        public const int RetentionMagEvents = 30;

        /// <summary>Whether a scheduled backup would run.</summary>
        public const bool BackupEnabled = false;

        /// <summary>How often the backup would run.</summary>
        public const BackupPeriod BackupPeriod = Enums.BackupPeriod.Daily;

        /// <summary>How many backups are kept; <c>0</c> means infinite.</summary>
        public const int BackupRotationCount = 10;

        /// <summary>Address shown when the stream is down.</summary>
        public const string FallbackStreamDownUrl = "";

        /// <summary>Address shown when the viewer is banned.</summary>
        public const string FallbackUserBannedUrl = "";

        /// <summary>Address shown when the viewer's subscription expired.</summary>
        public const string FallbackUserExpiredUrl = "";

        /// <summary>Address shown when the viewer's connection ceiling is reached.</summary>
        public const string FallbackMaxConnectionsUrl = "";

        /// <summary>The whole default row, with the fixed identifier and an empty whitelist.</summary>
        public static ApplicationSettings Create() => new()
        {
            Id = Id,
            ServiceName = ServiceName,
            DefaultTimeZone = DefaultTimeZone,
            Enigma2BouquetName = Enigma2BouquetName,
            RecaptchaSiteKey = RecaptchaSiteKey,
            RecaptchaSecretKey = RecaptchaSecretKey,
            EnableTrials = EnableTrials,
            PublicApiFloodLimit = PublicApiFloodLimit,
            PublicApiFloodWhitelist = [],
            RequireUserAgent = RequireUserAgent,
            RetentionUserLogs = RetentionUserLogs,
            RetentionCreditLogs = RetentionCreditLogs,
            RetentionConnectionLogs = RetentionConnectionLogs,
            RetentionApiRequestFailures = RetentionApiRequestFailures,
            RetentionStreamProblems = RetentionStreamProblems,
            RetentionMagEvents = RetentionMagEvents,
            BackupEnabled = BackupEnabled,
            BackupPeriod = BackupPeriod,
            BackupRotationCount = BackupRotationCount,
            FallbackStreamDownUrl = FallbackStreamDownUrl,
            FallbackUserBannedUrl = FallbackUserBannedUrl,
            FallbackUserExpiredUrl = FallbackUserExpiredUrl,
            FallbackMaxConnectionsUrl = FallbackMaxConnectionsUrl
        };
    }
}
