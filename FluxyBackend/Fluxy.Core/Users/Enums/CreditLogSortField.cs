namespace Fluxy.Core.Users.Enums
{
    /// <summary>
    /// The two stored columns the credit log may be ordered by.
    /// </summary>
    /// <remarks>
    /// Both are columns of <c>credit_logs</c> itself. The table also draws the reseller's and the
    /// issuer's names, but those are joined for the page, so an order built on either would order
    /// a page the server had already cut - the same lie a filter applied in the browser tells.
    /// </remarks>
    public enum CreditLogSortField
    {
        /// <summary>When the entry was written. The default, and the newest first.</summary>
        CreatedAt,

        /// <summary>The signed change itself.</summary>
        Amount
    }
}
