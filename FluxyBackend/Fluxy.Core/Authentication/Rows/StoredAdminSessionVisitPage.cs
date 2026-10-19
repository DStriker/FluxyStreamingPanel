namespace Fluxy.Core.Authentication.Rows
{
    /// <summary>
    /// One page of the installation's visit history, with the total the pager must show.
    /// </summary>
    public sealed record StoredAdminSessionVisitPage
    {
        /// <summary>Number of rows matching the filter, before the page was taken.</summary>
        public required int Total { get; init; }

        /// <summary>The rows of this page, in the requested order.</summary>
        public required IReadOnlyList<StoredAdminSessionVisit> Items { get; init; }
    }
}
