namespace Fluxy.Core.Users.Enums
{
    /// <summary>
    /// The two stored columns the user log may be ordered by.
    /// </summary>
    /// <remarks>
    /// Both are columns of <c>user_logs</c> itself. The table also draws the account's login name,
    /// but that is joined for the page, so an order built on it would order a page the server had
    /// already cut - the same lie a filter applied in the browser tells. The record type is
    /// sortable because grouping one kind of event is how an operator reads the log.
    /// </remarks>
    public enum UserLogSortField
    {
        /// <summary>When the entry was written. The default, and the newest first.</summary>
        CreatedAt = 0,

        /// <summary>Which kind of event the entry records.</summary>
        RecordType = 1
    }
}
