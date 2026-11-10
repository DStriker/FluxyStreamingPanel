namespace Fluxy.API.Contracts.Streams
{
    /// <summary>
    /// One page of the stream problem journal, with the count the pager is built from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is the size of the filtered list, not of the table: the repository
    /// counts it under the very predicate the page is cut from, before paging, which is what lets
    /// the pager describe the list on screen. The count is taken on <c>stream_problems</c> alone -
    /// the join that fills the stream's name cannot change it, because a problem names exactly one
    /// stream.
    /// </remarks>
    public sealed record AdminStreamProblemPageResponse
    {
        /// <summary>The records this page holds, in the requested order.</summary>
        public required IReadOnlyList<AdminStreamProblemItemResponse> Items { get; init; }

        /// <summary>How many records the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many records one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
