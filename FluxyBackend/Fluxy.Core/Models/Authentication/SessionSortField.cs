namespace Fluxy.Core.Models.Authentication
{
    /// <summary>
    /// What a page of the visit history may be ordered by.
    /// </summary>
    /// <remarks>
    /// Two members, because only two of the things on the page are columns. The moment and the
    /// address are stored; country and provider are resolved from GeoIP while the page is being
    /// read, so ordering by them would mean resolving every row an account has before the first
    /// one could be shown - and a sort that answers honestly for one page and dishonestly for the
    /// next is worse than a sort the menu does not offer at all.
    ///
    /// Values start at 1 so that `default(SessionSortField)` is not a member. The endpoint spells
    /// its default out (<c>visitedAt</c>) rather than relying on an absent parameter landing on
    /// the zero value, and starting at 1 means a value that never went through that code cannot
    /// be mistaken for it.
    ///
    /// The name a client sends is text - <c>visitedAt</c> or <c>ip</c> - and is turned into a
    /// member by <c>ProfileController</c>; nothing here is parsed by the framework.
    /// </remarks>
    public enum SessionSortField
    {
        /// <summary>When the token was issued or rotated. The default, newest first.</summary>
        VisitedAt = 1,

        /// <summary>The address the request arrived from. Addresses that are unknown sort last.</summary>
        Ip = 2
    }
}
