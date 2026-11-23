using Fluxy.Core.Backups.Utils;

namespace Fluxy.UnitTests.Backups
{
    /// <summary>
    /// The exclusion catalogue, pinned to exactly the six log tables.
    /// </summary>
    /// <remarks>
    /// <b>This test is the guard for a silent failure.</b> <c>pg_dump</c> ignores an
    /// <c>--exclude-table-data</c> name that matches no table and still exits zero, so a typo here
    /// would produce a backup that quietly carries the log it promised to leave out - and nothing
    /// in the run would say so. The integration suite asserts the same six names exist in the live
    /// schema, which is the other half of the check.
    /// </remarks>
    public sealed class BackupTableExclusionsTests
    {
        [Fact]
        public void ExactlySixTablesAreExcluded()
        {
            Assert.Equal(6, BackupTableExclusions.Tables.Count);
        }

        [Fact]
        public void TheTablesAreTheSixLogs()
        {
            Assert.Equal(
                [
                    "user_logs",
                    "credit_logs",
                    "connection_logs",
                    "api_request_failures",
                    "stream_problems",
                    "mag_events"
                ],
                BackupTableExclusions.Tables);
        }

        [Fact]
        public void TheArgumentsAreExactlyOnePerTable()
        {
            Assert.Equal(
                [
                    "--exclude-table-data=user_logs",
                    "--exclude-table-data=credit_logs",
                    "--exclude-table-data=connection_logs",
                    "--exclude-table-data=api_request_failures",
                    "--exclude-table-data=stream_problems",
                    "--exclude-table-data=mag_events"
                ],
                BackupTableExclusions.Arguments);
        }

        [Fact]
        public void EveryArgumentIsBuiltFromTheTableItNames()
        {
            Assert.Equal(
                BackupTableExclusions.Tables.Count,
                BackupTableExclusions.Arguments.Count);

            for (var index = 0; index < BackupTableExclusions.Tables.Count; index++)
            {
                Assert.Equal(
                    $"--exclude-table-data={BackupTableExclusions.Tables[index]}",
                    BackupTableExclusions.Arguments[index]);
            }
        }
    }
}
