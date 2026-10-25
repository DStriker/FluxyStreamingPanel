namespace Fluxy.Core.Series.Enums
{
    /// <summary>Whether a series is published, or disabled and kept.</summary>
    /// <remarks>
    /// Two states of one series, not two levels of anything, so nothing anywhere may compare
    /// two of them with <c>&gt;</c> or <c>&lt;</c>. The numeric values are fixed because they
    /// are what the <c>status</c> column stores.
    /// </remarks>
    public enum SeriesStatus
    {
        /// <summary>The series is disabled: kept, but not offered to clients.</summary>
        Inactive = 0,

        /// <summary>The series is published.</summary>
        Active = 1
    }
}
