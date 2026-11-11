namespace Fluxy.Core.Subscriptions.Enums
{
    /// <summary>
    /// The stored column a MAG event page may be ordered by.
    /// </summary>
    /// <remarks>
    /// Deliberately one member and a column of <c>mag_events</c> itself. The MAG address is joined
    /// from the subscription for the page, so ordering by it would order a page the server had
    /// already cut - the same lie a filter applied in the browser tells.
    /// </remarks>
    public enum MagEventSortField
    {
        /// <summary>When the event was recorded. The default, and the newest first.</summary>
        CreatedAt
    }
}
