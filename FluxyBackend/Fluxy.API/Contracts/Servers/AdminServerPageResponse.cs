namespace Fluxy.API.Contracts.Servers
{
    /// <summary>
    /// One page of the server table, with the count the pager is built from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is the size of the filtered list, not of the table. That is the whole
    /// reason search and order are answered by the server.
    /// </remarks>
    public sealed record AdminServerPageResponse
    {
        /// <summary>The servers this page holds, in the requested order.</summary>
        public required IReadOnlyList<AdminServerResponse> Items { get; init; }

        /// <summary>How many servers the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many servers one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
