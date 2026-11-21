using Fluxy.Application.Users.Utils;
using Fluxy.Core.Settings.Commands;
using Fluxy.Core.Settings.Enums;
using Fluxy.Core.Settings.Models;
using Fluxy.Core.Settings.Utils;

namespace Fluxy.Application.Settings.Utils
{
    /// <summary>
    /// The rules and sentences every write to the application settings shares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One row is written by one request, so the rules live here once and both the service's
    /// validation phase and its normalisation phase read them. Validation runs first and judges
    /// the values as they arrived; normalisation runs only once nothing was refused, so a value
    /// that is measured without its surrounding spaces is still the value the operator typed.
    /// </para>
    /// <para>
    /// <b>Several bounds describe a sentinel rather than a floor.</b>
    /// <c>publicApiFloodLimit = 0</c> is "unlimited" and <c>backupRotationCount = 0</c> is "keep
    /// everything"; neither is a missing value. The whitelist reuses the login guard's own
    /// canonicaliser rather than restating that rule - an address means the same thing wherever
    /// this application accepts one.
    /// </para>
    /// <para>
    /// The keys of the refused map are the C# property names of
    /// <see cref="ApplicationSettingsPatch"/>; the transport rewrites them to camelCase.
    /// </para>
    /// </remarks>
    public static class SettingsRules
    {
        /// <summary>The sentence a service name that is blank or too long gets.</summary>
        public static readonly string[] ServiceNameRule =
        [
            "The service name must be between " + SettingsLimits.ServiceNameMinLength + " and " +
                SettingsLimits.ServiceNameMaxLength + " characters."
        ];

        /// <summary>The sentence a time zone the platform cannot resolve gets.</summary>
        public static readonly string[] DefaultTimeZoneRule =
        [
            "The time zone must be an identifier this installation can resolve, of at most " +
                SettingsLimits.DefaultTimeZoneMaxLength + " characters."
        ];

        /// <summary>The sentence an Enigma2 bouquet name that is blank or too long gets.</summary>
        public static readonly string[] Enigma2BouquetNameRule =
        [
            "The bouquet name must be between " +
                SettingsLimits.Enigma2BouquetNameMinLength + " and " +
                SettingsLimits.Enigma2BouquetNameMaxLength + " characters."
        ];

        /// <summary>The sentence a reCAPTCHA key that is too long gets.</summary>
        public static readonly string[] RecaptchaKeyRule =
        [
            "A reCAPTCHA key must be at most " + SettingsLimits.RecaptchaKeyMaxLength +
                " characters."
        ];

        /// <summary>The sentence a flood limit outside its range gets.</summary>
        public static readonly string[] FloodLimitRule =
        [
            "The flood limit must be between " + SettingsLimits.FloodLimitMin + " and " +
                SettingsLimits.FloodLimitMax + "; 0 means unlimited."
        ];

        /// <summary>The sentence a whitelist with a malformed value gets.</summary>
        public static readonly string[] WhitelistRule =
        [
            "Every entry must be an IP address or a CIDR range, IPv4 or IPv6, and no more than " +
                SettingsLimits.WhitelistMaxEntries + " addresses may be listed."
        ];

        /// <summary>The sentence a retention window outside its range gets.</summary>
        public static readonly string[] RetentionRule =
        [
            "A retention window must be between " + SettingsLimits.RetentionMinDays + " and " +
                SettingsLimits.RetentionMaxDays + " days."
        ];

        /// <summary>The sentence a backup period that names no member gets.</summary>
        public static readonly string[] BackupPeriodRule =
        [
            "The backup period must be one of: hourly, daily, weekly, monthly."
        ];

        /// <summary>The sentence a rotation count outside its range gets.</summary>
        public static readonly string[] BackupRotationRule =
        [
            "The rotation count must be between " + SettingsLimits.BackupRotationMin + " and " +
                SettingsLimits.BackupRotationMax + "; 0 keeps every backup."
        ];

        /// <summary>The sentence a fallback address that is not an http(s) URL gets.</summary>
        public static readonly string[] FallbackUrlRule =
        [
            "A fallback address must be an absolute http or https address of at most " +
                SettingsLimits.FallbackUrlMaxLength + " characters, or empty for none."
        ];

        /// <summary>
        /// Checks every value of the patch that can be judged without the database.
        /// </summary>
        /// <param name="patch">Values as they arrived.</param>
        /// <returns>The rejected fields, empty when the row may be stored.</returns>
        public static Dictionary<string, string[]> Validate(ApplicationSettingsPatch patch)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            CheckText(
                errors,
                nameof(ApplicationSettingsPatch.ServiceName),
                patch.ServiceName,
                SettingsLimits.ServiceNameMinLength,
                SettingsLimits.ServiceNameMaxLength,
                ServiceNameRule);

            CheckTimeZone(errors, patch.DefaultTimeZone);

            CheckText(
                errors,
                nameof(ApplicationSettingsPatch.Enigma2BouquetName),
                patch.Enigma2BouquetName,
                SettingsLimits.Enigma2BouquetNameMinLength,
                SettingsLimits.Enigma2BouquetNameMaxLength,
                Enigma2BouquetNameRule);

            CheckOptionalText(
                errors,
                nameof(ApplicationSettingsPatch.RecaptchaSiteKey),
                patch.RecaptchaSiteKey,
                SettingsLimits.RecaptchaKeyMaxLength,
                RecaptchaKeyRule);

            CheckOptionalText(
                errors,
                nameof(ApplicationSettingsPatch.RecaptchaSecretKey),
                patch.RecaptchaSecretKey,
                SettingsLimits.RecaptchaKeyMaxLength,
                RecaptchaKeyRule);

            if (patch.PublicApiFloodLimit is < SettingsLimits.FloodLimitMin
                or > SettingsLimits.FloodLimitMax)
            {
                errors[nameof(ApplicationSettingsPatch.PublicApiFloodLimit)] = FloodLimitRule;
            }

            CheckWhitelist(errors, patch.PublicApiFloodWhitelist);

            CheckRetention(
                errors,
                nameof(ApplicationSettingsPatch.RetentionUserLogs),
                patch.RetentionUserLogs);

            CheckRetention(
                errors,
                nameof(ApplicationSettingsPatch.RetentionCreditLogs),
                patch.RetentionCreditLogs);

            CheckRetention(
                errors,
                nameof(ApplicationSettingsPatch.RetentionConnectionLogs),
                patch.RetentionConnectionLogs);

            CheckRetention(
                errors,
                nameof(ApplicationSettingsPatch.RetentionApiRequestFailures),
                patch.RetentionApiRequestFailures);

            CheckRetention(
                errors,
                nameof(ApplicationSettingsPatch.RetentionStreamProblems),
                patch.RetentionStreamProblems);

            CheckRetention(
                errors,
                nameof(ApplicationSettingsPatch.RetentionMagEvents),
                patch.RetentionMagEvents);

            if (ParseBackupPeriod(patch.BackupPeriod) is null)
            {
                errors[nameof(ApplicationSettingsPatch.BackupPeriod)] = BackupPeriodRule;
            }

            if (patch.BackupRotationCount is < SettingsLimits.BackupRotationMin
                or > SettingsLimits.BackupRotationMax)
            {
                errors[nameof(ApplicationSettingsPatch.BackupRotationCount)] = BackupRotationRule;
            }

            CheckFallbackUrl(
                errors,
                nameof(ApplicationSettingsPatch.FallbackStreamDownUrl),
                patch.FallbackStreamDownUrl);

            CheckFallbackUrl(
                errors,
                nameof(ApplicationSettingsPatch.FallbackUserBannedUrl),
                patch.FallbackUserBannedUrl);

            CheckFallbackUrl(
                errors,
                nameof(ApplicationSettingsPatch.FallbackUserExpiredUrl),
                patch.FallbackUserExpiredUrl);

            CheckFallbackUrl(
                errors,
                nameof(ApplicationSettingsPatch.FallbackMaxConnectionsUrl),
                patch.FallbackMaxConnectionsUrl);

            return errors;
        }

        /// <summary>
        /// Builds the row a validated patch describes.
        /// </summary>
        /// <param name="patch">Values that already passed validation.</param>
        /// <param name="id">Identifier of the row.</param>
        /// <param name="recaptchaSecretKey">
        /// Private key the row must carry - the request's new one, or the one it already held.
        /// </param>
        /// <returns>The values as they are about to be stored.</returns>
        public static ApplicationSettings Normalize(
            ApplicationSettingsPatch patch,
            Guid id,
            string recaptchaSecretKey) => new()
        {
            Id = id,
            ServiceName = patch.ServiceName.Trim(),
            DefaultTimeZone = patch.DefaultTimeZone.Trim(),
            Enigma2BouquetName = patch.Enigma2BouquetName.Trim(),
            RecaptchaSiteKey = patch.RecaptchaSiteKey.Trim(),
            RecaptchaSecretKey = recaptchaSecretKey,
            EnableTrials = patch.EnableTrials,
            PublicApiFloodLimit = patch.PublicApiFloodLimit,
            PublicApiFloodWhitelist = NormalizeWhitelist(patch.PublicApiFloodWhitelist),
            RequireUserAgent = patch.RequireUserAgent,
            RetentionUserLogs = patch.RetentionUserLogs,
            RetentionCreditLogs = patch.RetentionCreditLogs,
            RetentionConnectionLogs = patch.RetentionConnectionLogs,
            RetentionApiRequestFailures = patch.RetentionApiRequestFailures,
            RetentionStreamProblems = patch.RetentionStreamProblems,
            RetentionMagEvents = patch.RetentionMagEvents,
            BackupEnabled = patch.BackupEnabled,
            BackupPeriod = ParseBackupPeriod(patch.BackupPeriod)!.Value,
            BackupRotationCount = patch.BackupRotationCount,
            FallbackStreamDownUrl = patch.FallbackStreamDownUrl.Trim(),
            FallbackUserBannedUrl = patch.FallbackUserBannedUrl.Trim(),
            FallbackUserExpiredUrl = patch.FallbackUserExpiredUrl.Trim(),
            FallbackMaxConnectionsUrl = patch.FallbackMaxConnectionsUrl.Trim()
        };

        /// <summary>
        /// Reads a backup period from the text a client sent.
        /// </summary>
        /// <param name="value">Value as sent, possibly null.</param>
        /// <returns>The member it names, or null.</returns>
        public static BackupPeriod? ParseBackupPeriod(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "hourly" => BackupPeriod.Hourly,
                "daily" => BackupPeriod.Daily,
                "weekly" => BackupPeriod.Weekly,
                "monthly" => BackupPeriod.Monthly,
                _ => null
            };

        /// <summary>The text a client reads a stored backup period by.</summary>
        /// <param name="period">Stored period.</param>
        public static string NameOf(BackupPeriod period) => period switch
        {
            BackupPeriod.Hourly => "hourly",
            BackupPeriod.Daily => "daily",
            BackupPeriod.Weekly => "weekly",
            BackupPeriod.Monthly => "monthly",
            _ => throw new ArgumentOutOfRangeException(
                nameof(period),
                period,
                "Unknown backup period.")
        };

        /// <summary>
        /// Canonical form of the whitelist: normalised addresses, de-duplicated in
        /// first-occurrence order, with nothing that names no address left in it.
        /// </summary>
        /// <param name="entries">Entries as they arrived.</param>
        /// <returns>The stored list; empty when nothing was listed.</returns>
        public static string[] NormalizeWhitelist(IReadOnlyList<string>? entries)
        {
            if (entries is null || entries.Count == 0)
            {
                return [];
            }

            var normalized = new List<string>(entries.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in entries)
            {
                var address = LoginGuardPolicy.NormalizeIpEntry(entry);

                if (address is not null && seen.Add(address))
                {
                    normalized.Add(address);
                }
            }

            return [.. normalized];
        }

        private static void CheckText(
            Dictionary<string, string[]> errors,
            string field,
            string? value,
            int min,
            int max,
            string[] rule)
        {
            var trimmed = value?.Trim() ?? string.Empty;

            if (trimmed.Length < min || trimmed.Length > max)
            {
                errors[field] = rule;
            }
        }

        private static void CheckOptionalText(
            Dictionary<string, string[]> errors,
            string field,
            string? value,
            int max,
            string[] rule)
        {
            if ((value?.Trim().Length ?? 0) > max)
            {
                errors[field] = rule;
            }
        }

        private static void CheckTimeZone(
            Dictionary<string, string[]> errors,
            string? value)
        {
            var trimmed = value?.Trim() ?? string.Empty;

            if (trimmed.Length == 0
                || trimmed.Length > SettingsLimits.DefaultTimeZoneMaxLength
                || !TimeZonePolicy.TryNormalize(trimmed, out _))
            {
                errors[nameof(ApplicationSettingsPatch.DefaultTimeZone)] = DefaultTimeZoneRule;
            }
        }

        private static void CheckWhitelist(
            Dictionary<string, string[]> errors,
            IReadOnlyList<string>? entries)
        {
            if (entries is null)
            {
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in entries)
            {
                var address = LoginGuardPolicy.NormalizeIpEntry(entry);

                if (address is null)
                {
                    errors[nameof(ApplicationSettingsPatch.PublicApiFloodWhitelist)] =
                        WhitelistRule;

                    return;
                }

                seen.Add(address);
            }

            if (seen.Count > SettingsLimits.WhitelistMaxEntries)
            {
                errors[nameof(ApplicationSettingsPatch.PublicApiFloodWhitelist)] = WhitelistRule;
            }
        }

        private static void CheckRetention(
            Dictionary<string, string[]> errors,
            string field,
            int value)
        {
            if (value is < SettingsLimits.RetentionMinDays or > SettingsLimits.RetentionMaxDays)
            {
                errors[field] = RetentionRule;
            }
        }

        private static void CheckFallbackUrl(
            Dictionary<string, string[]> errors,
            string field,
            string? value)
        {
            var trimmed = value?.Trim() ?? string.Empty;

            if (trimmed.Length == 0)
            {
                return;
            }

            if (trimmed.Length > SettingsLimits.FallbackUrlMaxLength
                || !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                errors[field] = FallbackUrlRule;
            }
        }
    }
}
