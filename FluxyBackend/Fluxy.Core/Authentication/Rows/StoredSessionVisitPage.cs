namespace Fluxy.Core.Authentication.Rows
{
    /// <summary>
    /// One page of an account's visit history, with the total the pager must show.
    /// </summary>
    /// <remarks>
    /// The total is counted under the same filter as the page. A count that knew nothing about
    /// the search would advertise pages the caller was never going to be shown.
    /// </remarks>
    public sealed record StoredSessionVisitPage
    {
        /// <summary>Number of rows matching the filter, before the page was taken.</summary>
        public required int Total { get; init; }

        /// <summary>The rows of this page, in the requested order.</summary>
        public required IReadOnlyList<StoredSessionVisit> Items { get; init; }
    }
}
