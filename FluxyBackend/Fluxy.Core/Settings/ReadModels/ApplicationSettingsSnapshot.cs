using Fluxy.Core.Settings.Enums;

namespace Fluxy.Core.Settings.ReadModels
{
    /// <summary>
    /// One immutable reading of the installation settings, as the server uses them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The snapshot mirrors the admin contract and adds <see cref="RecaptchaSecretKey"/>, which
    /// the admin response deliberately does not carry: the server needs the private key to run
    /// the check, and only the response mapping drops it in favour of
    /// <see cref="RecaptchaConfigured"/>.
    /// </para>
    /// <para>
    /// It is a read model rather than a model because nothing writes it as a row and it is what
    /// the server hands out of its cache. Every array is a copy, so a reader cannot reach into a
    /// cached instance.
    /// </para>
    /// </remarks>
    public sealed record ApplicationSettingsSnapshot
    {
        /// <summary>Name the panel presents itself with.</summary>
        public required string ServiceName { get; init; }

        /// <summary>IANA identifier of the installation's display time zone.</summary>
        public required string DefaultTimeZone { get; init; }

        /// <summary>Name of the Enigma2 bouquet.</summary>
        public required string Enigma2BouquetName { get; init; }

        /// <summary>Public reCAPTCHA site key; empty when not configured.</summary>
        public required string RecaptchaSiteKey { get; init; }

        /// <summary>Private reCAPTCHA key; empty when the check is disabled.</summary>
        public required string RecaptchaSecretKey { get; init; }

        /// <summary>Whether the private key is non-empty, which is the only state a client reads.</summary>
        public bool RecaptchaConfigured => !string.IsNullOrWhiteSpace(RecaptchaSecretKey);

        /// <summary>Whether a subscription may be issued as a trial.</summary>
        public required bool EnableTrials { get; init; }

        /// <summary>Requests one public-API client may make in a window; <c>0</c> means unlimited.</summary>
        public required int PublicApiFloodLimit { get; init; }

        /// <summary>Addresses exempt from the public-API flood limit.</summary>
        public required string[] PublicApiFloodWhitelist { get; init; }

        /// <summary>Whether a public-API request has to present a user agent.</summary>
        public required bool RequireUserAgent { get; init; }

        /// <summary>Days a user log row is kept.</summary>
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
