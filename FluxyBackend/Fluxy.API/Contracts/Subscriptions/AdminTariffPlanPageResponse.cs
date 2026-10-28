namespace Fluxy.API.Contracts.Subscriptions
{
    /// <summary>
    /// One page of the tariff plan table, with the count the pager is built from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is the size of the filtered list, not of the table. That is the whole
    /// reason search and order are answered by the server.
    /// </remarks>
    public sealed record AdminTariffPlanPageResponse
    {
        /// <summary>The plans this page holds, in the requested order.</summary>
        public required IReadOnlyList<AdminTariffPlanResponse> Items { get; init; }

        /// <summary>How many plans the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many plans one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
