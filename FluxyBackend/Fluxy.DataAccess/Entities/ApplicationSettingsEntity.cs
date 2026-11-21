using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Settings.Enums;
using Fluxy.Core.Settings.Models;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of the single application settings row. It is mutable, has no
    /// behaviour beyond mapping to <see cref="ApplicationSettings"/>, and knows nothing about HTTP.
    /// </summary>
    /// <remarks>
    /// Every column is non-nullable and declares no database default, for the project-wide reason:
    /// a value the database invents when the application forgot a column hides the mistake. The
    /// list column is a PostgreSQL <c>text[]</c>, the shape the server country lists use.
    /// </remarks>
    public sealed class ApplicationSettingsEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationSettingsEntity"/> class for the
        /// row whose identifier the defaults statics fix.
        /// </summary>
        /// <remarks>
        /// The two timestamps are placeholders: both are overwritten for an inserted row by
        /// <c>FluxyDbContext.ApplyAudit</c>, which is the only place audit values are ever
        /// written.
        /// </remarks>
        /// <param name="id">The identifier the row must carry.</param>
        public ApplicationSettingsEntity(Guid id)
            : base(id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        {
        }

        /// <summary>Name the panel presents itself with.</summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>IANA identifier of the installation's display time zone.</summary>
        public string DefaultTimeZone { get; set; } = string.Empty;

        /// <summary>Name of the Enigma2 bouquet.</summary>
        public string Enigma2BouquetName { get; set; } = string.Empty;

        /// <summary>Public reCAPTCHA site key; empty when not configured.</summary>
        public string RecaptchaSiteKey { get; set; } = string.Empty;

        /// <summary>Private reCAPTCHA key; empty when the check is disabled.</summary>
        public string RecaptchaSecretKey { get; set; } = string.Empty;

        /// <summary>Whether a subscription may be issued as a trial.</summary>
        public bool EnableTrials { get; set; }

        /// <summary>Requests one public-API client may make in a window.</summary>
        public int PublicApiFloodLimit { get; set; }

        /// <summary>Addresses exempt from the public-API flood limit.</summary>
        public string[] PublicApiFloodWhitelist { get; set; } = [];

        /// <summary>Whether a public-API request has to present a user agent.</summary>
        public bool RequireUserAgent { get; set; }

        /// <summary>Days a user log row is kept.</summary>
        public int RetentionUserLogs { get; set; }

        /// <summary>Days a credit log row is kept.</summary>
        public int RetentionCreditLogs { get; set; }

        /// <summary>Days a connection log row is kept.</summary>
        public int RetentionConnectionLogs { get; set; }

        /// <summary>Days a refused-request row is kept.</summary>
        public int RetentionApiRequestFailures { get; set; }

        /// <summary>Days a reported stream problem is kept.</summary>
        public int RetentionStreamProblems { get; set; }

        /// <summary>Days a MAG event row is kept.</summary>
        public int RetentionMagEvents { get; set; }

        /// <summary>Whether a scheduled backup would run.</summary>
        public bool BackupEnabled { get; set; }

        /// <summary>How often the backup would run.</summary>
        public BackupPeriod BackupPeriod { get; set; }

        /// <summary>How many backups are kept; <c>0</c> means infinite.</summary>
        public int BackupRotationCount { get; set; }

        /// <summary>Address shown when the stream is down.</summary>
        public string FallbackStreamDownUrl { get; set; } = string.Empty;

        /// <summary>Address shown when the viewer is banned.</summary>
        public string FallbackUserBannedUrl { get; set; } = string.Empty;

        /// <summary>Address shown when the viewer's subscription expired.</summary>
        public string FallbackUserExpiredUrl { get; set; } = string.Empty;

        /// <summary>Address shown when the viewer's connection ceiling is reached.</summary>
        public string FallbackMaxConnectionsUrl { get; set; } = string.Empty;

        /// <summary>Builds the business model out of this row.</summary>
        public ApplicationSettings ToModel() => new()
        {
            Id = Id,
            ServiceName = ServiceName,
            DefaultTimeZone = DefaultTimeZone,
            Enigma2BouquetName = Enigma2BouquetName,
            RecaptchaSiteKey = RecaptchaSiteKey,
            RecaptchaSecretKey = RecaptchaSecretKey,
            EnableTrials = EnableTrials,
            PublicApiFloodLimit = PublicApiFloodLimit,
            PublicApiFloodWhitelist = PublicApiFloodWhitelist,
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

        /// <summary>Copies every editable field of the business model onto this row.</summary>
        /// <remarks>
        /// The identifier and the audit stamps are deliberately not copied: the first is the
        /// row's identity and the second pair belongs to the context. The list is copied into a
        /// fresh array so a later change to the model cannot reach into a tracked row behind the
        /// repository's back.
        /// </remarks>
        /// <param name="model">Model whose values the row must carry.</param>
        public void FromModel(ApplicationSettings model)
        {
            ServiceName = model.ServiceName;
            DefaultTimeZone = model.DefaultTimeZone;
            Enigma2BouquetName = model.Enigma2BouquetName;
            RecaptchaSiteKey = model.RecaptchaSiteKey;
            RecaptchaSecretKey = model.RecaptchaSecretKey;
            EnableTrials = model.EnableTrials;
            PublicApiFloodLimit = model.PublicApiFloodLimit;
            PublicApiFloodWhitelist = model.PublicApiFloodWhitelist.ToArray();
            RequireUserAgent = model.RequireUserAgent;
            RetentionUserLogs = model.RetentionUserLogs;
            RetentionCreditLogs = model.RetentionCreditLogs;
            RetentionConnectionLogs = model.RetentionConnectionLogs;
            RetentionApiRequestFailures = model.RetentionApiRequestFailures;
            RetentionStreamProblems = model.RetentionStreamProblems;
            RetentionMagEvents = model.RetentionMagEvents;
            BackupEnabled = model.BackupEnabled;
            BackupPeriod = model.BackupPeriod;
            BackupRotationCount = model.BackupRotationCount;
            FallbackStreamDownUrl = model.FallbackStreamDownUrl;
            FallbackUserBannedUrl = model.FallbackUserBannedUrl;
            FallbackUserExpiredUrl = model.FallbackUserExpiredUrl;
            FallbackMaxConnectionsUrl = model.FallbackMaxConnectionsUrl;
        }
    }
}
