namespace Fluxy.Core.Models.Authentication
{
    /// <summary>
    /// Every session an account currently holds, newest first.
    /// </summary>
    /// <remarks>
    /// No paging and no <c>total</c> to draw a pager from, unlike
    /// <see cref="SessionHistoryPage"/>: an account holds a handful of sessions at most, so a
    /// page number would be one more parameter with nothing behind it - and the "end all but
    /// this one" action needs the whole list anyway, since it revokes everything the response
    /// does not contain.
    /// </remarks>
    public sealed record ActiveSessionList
    {
        /// <summary>The account's live sessions, newest first.</summary>
        public required IReadOnlyList<ActiveSession> Sessions { get; init; }
    }
}
