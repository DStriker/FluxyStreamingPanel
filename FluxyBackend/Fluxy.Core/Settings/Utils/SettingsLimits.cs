namespace Fluxy.Core.Settings.Utils
{
    /// <summary>
    /// The bounds, lengths and the one duration that every write and read of the settings shares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The numbers mirror the columns the two tables declare, and they are stated here rather
    /// than imported from the configurations for the reason the registration rules keep their own
    /// copies: a limit belongs to the value being accepted, and a column is what happens to agree
    /// with it.
    /// </para>
    /// <para>
    /// <b>Several bounds describe a sentinel rather than a ceiling.</b>
    /// <c>publicApiFloodLimit = 0</c> is "unlimited", <c>backupRotationCount = 0</c> is "keep
    /// everything", and <c>maxLocalRecordings = 0</c> is "no local recording". None of them is a
    /// missing value, which is why the lower bound of each is inclusive.
    /// </para>
    /// </remarks>
    public static class SettingsLimits
    {
        /// <summary>Shortest accepted service name, matching the non-empty rule.</summary>
        public const int ServiceNameMinLength = 1;

        /// <summary>Longest accepted service name, matching <c>varchar(100)</c>.</summary>
        public const int ServiceNameMaxLength = 100;

        /// <summary>
        /// Longest accepted time zone identifier, matching <c>varchar(64)</c> and the bound the
        /// profile's own time zone rule already declares.
        /// </summary>
        public const int DefaultTimeZoneMaxLength = 64;

        /// <summary>Shortest accepted Enigma2 bouquet name, matching the non-empty rule.</summary>
        public const int Enigma2BouquetNameMinLength = 1;

        /// <summary>Longest accepted Enigma2 bouquet name, matching <c>varchar(100)</c>.</summary>
        public const int Enigma2BouquetNameMaxLength = 100;

        /// <summary>Longest accepted reCAPTCHA site or private key, matching <c>varchar(255)</c>.</summary>
        public const int RecaptchaKeyMaxLength = 255;

        /// <summary>Lowest public-API flood limit; <c>0</c> means unlimited.</summary>
        public const int FloodLimitMin = 0;

        /// <summary>Highest public-API flood limit.</summary>
        public const int FloodLimitMax = 10000;

        /// <summary>Most addresses the public-API flood whitelist may hold.</summary>
        public const int WhitelistMaxEntries = 100;

        /// <summary>Shortest retention window, in days.</summary>
        public const int RetentionMinDays = 1;

        /// <summary>Longest retention window, in days.</summary>
        public const int RetentionMaxDays = 3650;

        /// <summary>Lowest backup rotation count; <c>0</c> keeps every backup.</summary>
        public const int BackupRotationMin = 0;

        /// <summary>Highest backup rotation count.</summary>
        public const int BackupRotationMax = 1000;

        /// <summary>Longest accepted fallback address, matching <c>varchar(2048)</c>.</summary>
        public const int FallbackUrlMaxLength = 2048;

        /// <summary>Shortest accepted recording length, in minutes.</summary>
        public const int RecordingMaxLengthMinutesMin = 1;

        /// <summary>Longest accepted recording length, in minutes.</summary>
        public const int RecordingMaxLengthMinutesMax = 1440;

        /// <summary>Lowest local recording count; <c>0</c> allows none.</summary>
        public const int MaxLocalRecordingsMin = 0;

        /// <summary>Highest local recording count.</summary>
        public const int MaxLocalRecordingsMax = 1000;

        /// <summary>
        /// Lowest playback limit. The ceiling is the subscription's own connection maximum, so
        /// the two cannot drift apart.
        /// </summary>
        public const int PlaybackLimitMin = 1;

        /// <summary>
        /// Highest playback limit, the same ceiling a subscription's own connection count uses.
        /// </summary>
        public const int PlaybackLimitMax =
            Fluxy.Core.Subscriptions.Utils.SubscriptionLimits.ConnectionsMax;

        /// <summary>
        /// How long a loaded snapshot is served before the next read refreshes it.
        /// </summary>
        /// <remarks>
        /// A write refreshes the snapshot eagerly, so the window only bounds how long another
        /// process's change may take to be seen. Thirty seconds keeps that invisible in practice
        /// while keeping the number of reads on the settings table negligible.
        /// </remarks>
        public static readonly TimeSpan SnapshotTtl = TimeSpan.FromSeconds(30);
    }
}
