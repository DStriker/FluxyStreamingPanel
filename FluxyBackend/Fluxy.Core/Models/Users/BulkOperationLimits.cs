namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// The numbers a bulk operation accepts.
    /// </summary>
    /// <remarks>
    /// One number, and it is <see cref="AdminUserListLimits.MaxPageSize"/> rather than a
    /// figure of its own, for a reason worth writing down: the selection this endpoint exists
    /// to serve is the rows of one page, and a page holds at most that many rows. A ceiling
    /// of its own would be a second number to keep in step with the first, and the day they
    /// disagreed the table's own "select everything" header would hand the endpoint more rows
    /// than it would accept - a failure the operator could not explain and the UI would show
    /// as a mystery 400.
    ///
    /// The bound is refused rather than truncated, the same decision the list endpoint makes:
    /// a caller who asked for 500 identifiers deserves to be told it will not get them instead
    /// of quietly receiving 100 and being answered about a subset it did not know it had sent.
    /// </remarks>
    public static class BulkOperationLimits
    {
        /// <summary>Most identifiers one bulk operation may name.</summary>
        public const int MaxItems = AdminUserListLimits.MaxPageSize;
    }
}
