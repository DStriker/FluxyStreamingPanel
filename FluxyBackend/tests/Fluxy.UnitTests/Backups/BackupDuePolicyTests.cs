using Fluxy.Application.Backups.Utils;
using Fluxy.Core.Settings.Enums;

namespace Fluxy.UnitTests.Backups
{
    /// <summary>
    /// The four period buckets, and the ISO-week boundary a calendar week would get wrong.
    /// </summary>
    public sealed class BackupDuePolicyTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 23, 10, 15, 0, TimeSpan.Zero);

        [Fact]
        public void NoArchiveAtAllMeansADumpIsDue()
        {
            foreach (var period in Enum.GetValues<BackupPeriod>())
            {
                Assert.True(BackupDuePolicy.IsDue(Now, period, null));
            }
        }

        [Fact]
        public void HourlyIsTheSameUtcDateAndHour()
        {
            Assert.False(BackupDuePolicy.IsDue(Now, BackupPeriod.Hourly, Now.AddMinutes(-14)));
            Assert.True(BackupDuePolicy.IsDue(Now, BackupPeriod.Hourly, Now.AddMinutes(-59)));
            Assert.True(BackupDuePolicy.IsDue(Now, BackupPeriod.Hourly, Now.AddDays(-1)));
        }

        [Fact]
        public void DailyIsTheSameUtcDate()
        {
            Assert.False(BackupDuePolicy.IsDue(Now, BackupPeriod.Daily, Now.AddMinutes(-5)));
            Assert.False(
                BackupDuePolicy.IsDue(
                    Now,
                    BackupPeriod.Daily,
                    new DateTimeOffset(2026, 11, 23, 0, 0, 1, TimeSpan.Zero)));
            Assert.True(
                BackupDuePolicy.IsDue(
                    Now,
                    BackupPeriod.Daily,
                    new DateTimeOffset(2026, 11, 22, 23, 59, 59, TimeSpan.Zero)));
        }

        [Fact]
        public void WeeklyIsTheSameIsoWeek()
        {
            // 23 November 2026 is a Monday; the Friday of that week is the same bucket.
            Assert.False(
                BackupDuePolicy.IsDue(
                    Now,
                    BackupPeriod.Weekly,
                    new DateTimeOffset(2026, 11, 27, 8, 0, 0, TimeSpan.Zero)));

            Assert.True(
                BackupDuePolicy.IsDue(
                    Now,
                    BackupPeriod.Weekly,
                    new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void TheDecJanBoundaryFollowsTheIsoWeekYear()
        {
            // 29 December 2025 is ISO week 1 of 2026; 30 December 2024 is ISO week 1 of 2025.
            var endOfDecember = new DateTimeOffset(2025, 12, 29, 12, 0, 0, TimeSpan.Zero);
            var sameWeek = new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero);
            var previousYearSameWeekNumber = new DateTimeOffset(2024, 12, 30, 12, 0, 0, TimeSpan.Zero);

            Assert.False(BackupDuePolicy.IsDue(endOfDecember, BackupPeriod.Weekly, sameWeek));
            Assert.True(
                BackupDuePolicy.IsDue(
                    endOfDecember,
                    BackupPeriod.Weekly,
                    previousYearSameWeekNumber));
        }

        [Fact]
        public void MonthlyIsTheSameYearAndMonth()
        {
            Assert.False(
                BackupDuePolicy.IsDue(
                    Now,
                    BackupPeriod.Monthly,
                    new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero)));

            Assert.True(
                BackupDuePolicy.IsDue(
                    Now,
                    BackupPeriod.Monthly,
                    new DateTimeOffset(2026, 10, 31, 23, 59, 59, TimeSpan.Zero)));

            Assert.True(
                BackupDuePolicy.IsDue(
                    Now,
                    BackupPeriod.Monthly,
                    new DateTimeOffset(2025, 11, 23, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void ALaterInstantInsideTheBucketDoesNotScheduleAnotherDump()
        {
            // The archive's own instant may lie inside the bucket even when it was created after
            // "now" was read; the bucket, not the interval, is the rule.
            Assert.False(BackupDuePolicy.IsDue(Now, BackupPeriod.Daily, Now.AddHours(1)));
        }
    }
}
