namespace Fluxy.Core.Models.Authentication
{
    /// <summary>
    /// One page of an account's visit history, newest first.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> counts every entry the account has, not the rows of this page, because
    /// that is what a pager needs to draw itself. It is reported alongside the items rather than
    /// fetched separately by a caller: a count read and a page read that do not happen in the
    /// same request can disagree while new visits are being written between them, and a pager
    /// that was told 27 and is holding 20 rows of a different 27 is worse than one that simply
    /// answers for the moment it read.
    /// </remarks>
    public sealed record SessionHistoryPage
    {
        /// <summary>The visits on this page, newest first.</summary>
        public required IReadOnlyList<SessionVisit> Visits { get; init; }

        /// <summary>How many visits the account has in total.</summary>
        public required int Total { get; init; }

        /// <summary>The page these visits are, one based.</summary>
        public required int Page { get; init; }

        /// <summary>How many visits one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
