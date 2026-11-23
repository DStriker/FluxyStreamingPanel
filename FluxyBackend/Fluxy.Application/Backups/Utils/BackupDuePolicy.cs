using Fluxy.Core.Settings.Enums;

namespace Fluxy.Application.Backups.Utils
{
    /// <summary>
    /// Whether a scheduled backup is due, derived from the last file's own instant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The period is a bucket, not an interval.</b> A daily backup is due when no archive
    /// exists for today's UTC date, not when twenty-four hours have elapsed since the last one:
    /// a host that was down over midnight takes today's backup as soon as it comes back, and a
    /// host that restarts twice does not take two. The same reading applies to the hour, the ISO
    /// week and the month.
    /// </para>
    /// <para>
    /// <b>The week is an ISO week-year plus an ISO week number.</b> Taking the calendar year with
    /// <c>ISOWeek.GetWeekOfYear</c> would make 29 December 2025 (ISO week 1 of 2026) collide with
    /// 30 December 2024 (ISO week 1 of 2025); <c>ISOWeek.GetYear</c> is what makes the Dec/Jan
    /// boundary correct, and the unit test pins it.
    /// </para>
    /// <para>
    /// Every instant is UTC. A local bucket would make "today" depend on the host's zone, and two
    /// hosts in different zones would disagree about whether the day's backup had happened.
    /// </para>
    /// </remarks>
    public static class BackupDuePolicy
    {
        /// <summary>Whether a backup is due at the given instant.</summary>
        /// <param name="nowUtc">The current instant, in UTC.</param>
        /// <param name="period">The configured period.</param>
        /// <param name="latestFileUtc">The newest archive's instant, or null when none exists.</param>
        /// <returns>True when the current bucket holds no archive.</returns>
        public static bool IsDue(DateTimeOffset nowUtc, BackupPeriod period, DateTimeOffset? latestFileUtc)
        {
            if (latestFileUtc is null)
            {
                return true;
            }

            var now = nowUtc.UtcDateTime;
            var latest = latestFileUtc.Value.UtcDateTime;

            return !SameBucket(now, latest, period);
        }

        /// <summary>Whether two instants fall in the same bucket of the given period.</summary>
        private static bool SameBucket(DateTime now, DateTime latest, BackupPeriod period) => period switch
        {
            BackupPeriod.Hourly =>
                now.Year == latest.Year && now.Month == latest.Month
                && now.Day == latest.Day && now.Hour == latest.Hour,

            BackupPeriod.Daily => now.Date == latest.Date,

            BackupPeriod.Weekly =>
                System.Globalization.ISOWeek.GetYear(now) == System.Globalization.ISOWeek.GetYear(latest)
                && System.Globalization.ISOWeek.GetWeekOfYear(now)
                    == System.Globalization.ISOWeek.GetWeekOfYear(latest),

            BackupPeriod.Monthly => now.Year == latest.Year && now.Month == latest.Month,

            _ => false
        };
    }
}
