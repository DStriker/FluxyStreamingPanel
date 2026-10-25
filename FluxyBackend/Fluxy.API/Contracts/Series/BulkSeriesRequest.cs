namespace Fluxy.API.Contracts.Series
{
    /// <summary>
    /// The body of <c>POST /admin/series/bulk</c>.
    /// </summary>
    public sealed record BulkSeriesRequest
    {
        /// <summary>Operation name: <c>change-status</c>, <c>move-category</c> or <c>delete</c>.</summary>
        public string? Action { get; init; }

        /// <summary>The series to act on, as text identifiers.</summary>
        public IReadOnlyList<string>? Ids { get; init; }

        /// <summary>Destination of a move, as text, or null for the other operations.</summary>
        public string? CategoryId { get; init; }

        /// <summary>State a status change writes, as text, or null for the other operations.</summary>
        public string? Status { get; init; }
    }
}
