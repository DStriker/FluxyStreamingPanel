namespace Fluxy.Core.Settings.Commands
{
    /// <summary>
    /// The whole application settings row as the admin form sends it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The patch is a full write, and only the private key is write-only.</b> Every other
    /// value is required and replaces the stored one; <see cref="RecaptchaSecretKey"/> absent or
    /// empty means "keep the stored secret", and a non-empty value replaces it. That is the one
    /// asymmetry, because the response never carries the secret for a form to send back.
    /// </para>
    /// <para>
    /// <see cref="RecaptchaConfigured"/> is absent on purpose: it is derived from the secret the
    /// row holds and is never something a caller sets.
    /// </para>
    /// </remarks>
    public sealed record ApplicationSettingsPatch
    {
        /// <summary>Name the panel presents itself with.</summary>
        public required string ServiceName { get; init; }

        /// <summary>IANA identifier of the installation's display time zone.</summary>
        public required string DefaultTimeZone { get; init; }

        /// <summary>Name of the Enigma2 bouquet.</summary>
        public required string Enigma2BouquetName { get; init; }

        /// <summary>Public reCAPTCHA site key; empty allowed.</summary>
        public required string RecaptchaSiteKey { get; init; }

        /// <summary>
        /// Private reCAPTCHA key, or null / empty to keep the stored one; at most 255 characters
        /// when present.
        /// </summary>
        public string? RecaptchaSecretKey { get; init; }

        /// <summary>Whether a subscription may be issued as a trial.</summary>
        public required bool EnableTrials { get; init; }

        /// <summary>Requests one public-API client may make in a window; <c>0</c> means unlimited.</summary>
        public required int PublicApiFloodLimit { get; init; }

        /// <summary>Addresses exempt from the public-API flood limit.</summary>
        public required IReadOnlyList<string> PublicApiFloodWhitelist { get; init; }

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

        /// <summary>How often the backup would run, as text.</summary>
        public required string? BackupPeriod { get; init; }

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
