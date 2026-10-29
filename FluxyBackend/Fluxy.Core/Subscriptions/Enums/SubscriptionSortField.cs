namespace Fluxy.Core.Subscriptions.Enums
{
    /// <summary>
    /// The two columns the subscriptions table may be ordered by.
    /// </summary>
    /// <remarks>
    /// Deliberately two. The table is read to answer "which of these expire, and which were
    /// issued when", and both are stored columns; an order over a value that has to be
    /// computed or joined would order a page the server had already cut, which is the same
    /// lie a filter applied in the browser tells.
    /// </remarks>
    public enum SubscriptionSortField
    {
        /// <summary>When the subscription was issued. The default, and the newest first.</summary>
        CreatedAt,

        /// <summary>When it lapses. A null expiry groups last in both directions.</summary>
        ExpireAt
    }
}
