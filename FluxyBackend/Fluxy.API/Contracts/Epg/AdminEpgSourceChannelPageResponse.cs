namespace Fluxy.API.Contracts.Epg
{
    /// <summary>
    /// One page of the channels that use a guide source, with the count the pager is built
    /// from.
    /// </summary>
    public sealed record AdminEpgSourceChannelPageResponse
    {
        /// <summary>The channels this page holds, in the requested order.</summary>
        public required IReadOnlyList<AdminEpgSourceChannelResponse> Items { get; init; }

        /// <summary>How many channels the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many channels one page holds.</summary>
        public required int PageSize { get; init; }
    }
}
