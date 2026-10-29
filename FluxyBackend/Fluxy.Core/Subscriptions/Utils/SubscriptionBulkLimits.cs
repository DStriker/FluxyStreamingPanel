namespace Fluxy.Core.Subscriptions.Utils
{
    /// <summary>
    /// The numbers a bulk operation over subscriptions accepts.
    /// </summary>
    /// <remarks>
    /// One number, and it is <see cref="SubscriptionLimits.MaxPageSize"/> rather than a figure of
    /// its own: the selection this operation exists to serve is the rows of one page, so a
    /// ceiling of its own would be a second number to keep in step with the table's pager. The
    /// bound is refused rather than truncated, the same decision the list endpoint makes.
    /// </remarks>
    public static class SubscriptionBulkLimits
    {
        /// <summary>Most identifiers one bulk operation may name.</summary>
        public const int MaxItems = SubscriptionLimits.MaxPageSize;
    }
}
