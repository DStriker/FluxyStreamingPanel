namespace Fluxy.API.Contracts.Series
{
    /// <summary>
    /// The body of <c>POST /admin/series/{id}/episodes/bulk</c>.
    /// </summary>
    public sealed record BulkEpisodeRequest
    {
        /// <summary>Operation name: <c>change-status</c>, <c>change-type</c> or <c>delete</c>.</summary>
        public string? Action { get; init; }

        /// <summary>The episodes to act on, as text identifiers.</summary>
        public IReadOnlyList<string>? Ids { get; init; }

        /// <summary>State a status change writes, as text, or null for the other operations.</summary>
        public string? Status { get; init; }

        /// <summary>Delivery kind a type change writes, as text, or null for the other operations.</summary>
        public string? Type { get; init; }
    }
}
