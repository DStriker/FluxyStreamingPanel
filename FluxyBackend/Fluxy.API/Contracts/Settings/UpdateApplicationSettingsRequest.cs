namespace Fluxy.API.Contracts.Settings
{
    /// <summary>
    /// What the settings form sends to change the application settings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is a full write, and only the private key is optional.</b> Every other value is
    /// required and replaces the stored one; <see cref="RecaptchaSecretKey"/> absent or empty
    /// keeps the stored secret and a non-empty value replaces it. That is the one asymmetry,
    /// because the response never carries the secret for a form to send back.
    /// </para>
    /// <para>
    /// No data annotations, for the reason every admin write here gives: the antiforgery check
    /// has to run before anything judges the body.
    /// </para>
    /// </remarks>
    public sealed record UpdateApplicationSettingsRequest
    {
        /// <summary>Name the panel presents itself with.</summary>
        public string? ServiceName { get; init; }

        /// <summary>IANA identifier of the installation's display time zone.</summary>
        public string? DefaultTimeZone { get; init; }

        /// <summary>Name of the Enigma2 bouquet.</summary>
        public string? Enigma2BouquetName { get; init; }

        /// <summary>Public reCAPTCHA site key; empty allowed.</summary>
        public string? RecaptchaSiteKey { get; init; }

        /// <summary>
        /// Private reCAPTCHA key, or absent / empty to keep the stored one.
        /// </summary>
        public string? RecaptchaSecretKey { get; init; }

        /// <summary>Whether a subscription may be issued as a trial.</summary>
        public bool? EnableTrials { get; init; }

        /// <summary>Requests one public-API client may make in a window; <c>0</c> means unlimited.</summary>
        public int? PublicApiFloodLimit { get; init; }

        /// <summary>Addresses exempt from the public-API flood limit.</summary>
        public IReadOnlyList<string>? PublicApiFloodWhitelist { get; init; }

        /// <summary>Whether a public-API request has to present a user agent.</summary>
        public bool? RequireUserAgent { get; init; }

        /// <summary>Days a user log row is kept.</summary>
        public int? RetentionUserLogs { get; init; }

        /// <summary>Days a credit log row is kept.</summary>
        public int? RetentionCreditLogs { get; init; }

        /// <summary>Days a connection log row is kept.</summary>
        public int? RetentionConnectionLogs { get; init; }

        /// <summary>Days a refused-request row is kept.</summary>
        public int? RetentionApiRequestFailures { get; init; }

        /// <summary>Days a reported stream problem is kept.</summary>
        public int? RetentionStreamProblems { get; init; }

        /// <summary>Days a MAG event row is kept.</summary>
        public int? RetentionMagEvents { get; init; }

        /// <summary>Whether a scheduled backup would run.</summary>
        public bool? BackupEnabled { get; init; }

        /// <summary>How often the backup would run, as the owner's lowercase word.</summary>
        public string? BackupPeriod { get; init; }

        /// <summary>How many backups are kept; <c>0</c> means infinite.</summary>
        public int? BackupRotationCount { get; init; }

        /// <summary>Address shown when the stream is down; empty for none.</summary>
        public string? FallbackStreamDownUrl { get; init; }

        /// <summary>Address shown when the viewer is banned; empty for none.</summary>
        public string? FallbackUserBannedUrl { get; init; }

        /// <summary>Address shown when the viewer's subscription expired; empty for none.</summary>
        public string? FallbackUserExpiredUrl { get; init; }

        /// <summary>Address shown when the viewer's connection ceiling is reached; empty for none.</summary>
        public string? FallbackMaxConnectionsUrl { get; init; }
    }
}
