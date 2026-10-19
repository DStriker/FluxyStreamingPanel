namespace Fluxy.Core.Authentication.ReadModels
{
    /// <summary>
    /// One page of the installation's visit history, newest first.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> counts every visit the operator may see, not the rows of this page,
    /// because that is what a pager needs to draw itself - the same reason
    /// <see cref="SessionHistoryPage"/> carries one. It is read under the same filter the page
    /// is taken under: a total over every row of the table would advertise pages whose rows the
    /// caller was never going to be shown, which is the one lie a pager cannot survive.
    /// </remarks>
    public sealed record AdminSessionHistoryPage
    {
        /// <summary>The visits on this page, newest first.</summary>
        public required IReadOnlyList<AdminSessionVisit> Visits { get; init; }

        /// <summary>How many visits the operator may see in total.</summary>
        public required int Total { get; init; }

        /// <summary>The page these visits are, one based.</summary>
        public required int Page { get; init; }

        /// <summary>How many visits one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
