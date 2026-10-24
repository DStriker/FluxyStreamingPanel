namespace Fluxy.API.Contracts.Vod
{
    /// <summary>
    /// What a bulk operation over on-demand videos carries: which operation, over which
    /// videos, and the one value that operation writes.
    /// </summary>
    /// <remarks>
    /// No data annotations on purpose, for the reason the streams bulk request gives: every
    /// field is parsed in the action, after the antiforgery pair has been accepted and before
    /// a row is read.
    /// </remarks>
    public sealed class BulkVodRequest
    {
        /// <summary>
        /// The operation to run, as text: <c>change-status</c>, <c>move-category</c>,
        /// <c>change-type</c> or <c>delete</c>.
        /// </summary>
        public string? Action { get; init; }

        /// <summary>The videos to act on, as uuids. At most one page of them.</summary>
        public IReadOnlyList<string>? Ids { get; init; }

        /// <summary>Destination of a <c>move-category</c>, as the uuid of a category.</summary>
        public string? CategoryId { get; init; }

        /// <summary>State a <c>change-status</c> writes: <c>active</c> or <c>inactive</c>.</summary>
        public string? Status { get; init; }

        /// <summary>Delivery kind a <c>change-type</c> writes: <c>relay</c> or <c>redirect</c>.</summary>
        public string? Type { get; init; }
    }
}
