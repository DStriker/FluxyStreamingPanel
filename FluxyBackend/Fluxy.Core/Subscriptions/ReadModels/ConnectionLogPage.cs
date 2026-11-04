namespace Fluxy.Core.Subscriptions.ReadModels
{
    /// <summary>
    /// One page of the connection log, with the count the pager is built from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is the size of the filtered list, not of the table. That is the whole
    /// reason every filter, the search and the order are answered by the server: a pager that
    /// counted the table while the screen filtered one page would advertise pages that hold
    /// nothing the caller asked for.
    /// </remarks>
    public sealed record ConnectionLogPage
    {
        /// <summary>The connections this page holds, in the requested order.</summary>
        public required IReadOnlyList<ConnectionLogListItem> Items { get; init; }

        /// <summary>How many connections the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many connections one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
