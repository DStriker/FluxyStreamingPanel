namespace Fluxy.Core.Settings.Enums
{
    /// <summary>
    /// How often a scheduled backup would run.
    /// </summary>
    /// <remarks>
    /// Nothing in this pass executes a backup: the value is stored, validated and read by an
    /// operator only. The numbering is the stored <c>smallint</c>, and <see cref="Daily"/> is the
    /// default a fresh installation starts with.
    /// </remarks>
    public enum BackupPeriod
    {
        /// <summary>Once an hour.</summary>
        Hourly = 0,

        /// <summary>Once a day.</summary>
        Daily = 1,

        /// <summary>Once a week.</summary>
        Weekly = 2,

        /// <summary>Once a month.</summary>
        Monthly = 3
    }
}
