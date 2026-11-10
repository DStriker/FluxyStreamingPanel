using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Utils
{
    /// <summary>
    /// The choices a stream problem query names, resolved from the text the caller sent, or the
    /// map of parameters that could not be resolved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this is a rule and not transport code.</b> Resolving the page bounds, the search,
    /// the stream filter, the two date bounds, <c>sortBy</c> and <c>sortOrder</c> is a contract
    /// about what a client may ask for, and it has one answer: a value that names nothing is
    /// refused under its own parameter, never treated as "no filter". The rule lives here, beside
    /// the list limits it reads, so it can be tested without a running host; the controller keeps
    /// the HTTP half - the status code and the body.
    /// </para>
    /// <para>
    /// <b>The search covers the message and nothing else.</b> A problem row carries a stream
    /// reference and a message; the stream's name is resolved by a join on read and is not part of
    /// the stored predicate. Searching it would need a join in the count as well, which is a shape
    /// this phase deliberately does not own.
    /// </para>
    /// <para>
    /// <b>Both date bounds are inclusive and independent.</b> <c>dateFrom</c> keeps rows whose
    /// <c>created_at</c> is greater than or equal to it and <c>dateTo</c> keeps rows whose
    /// <c>created_at</c> is less than or equal to it; either may be absent, which means no bound
    /// on that side. Both ends are inclusive because the screen's picker names whole days: an end
    /// the operator picked as "the 1st" that dropped everything written during the 1st would
    /// answer a list narrower than the one the control shows. A later <c>dateFrom</c> than
    /// <c>dateTo</c> is a valid query describing an interval with nothing in it - the answer is an
    /// empty page, not a refusal, because swapping the bounds would answer a different question
    /// from the one the caller asked.
    /// </para>
    /// <para>
    /// <b>Nothing is clamped.</b> The page bounds are the caller's to break and this type's to
    /// refuse; it does not silently narrow a request.
    /// </para>
    /// </remarks>
    public sealed class StreamProblemQuery
    {
        private StreamProblemQuery(
            int page,
            int pageSize,
            string? search,
            Guid? streamId,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            StreamProblemSortField sortBy,
            StreamProblemSortOrder sortOrder)
        {
            Page = page;
            PageSize = pageSize;
            Search = search;
            StreamId = streamId;
            DateFrom = dateFrom;
            DateTo = dateTo;
            SortBy = sortBy;
            SortOrder = sortOrder;
        }

        /// <summary>One-based page wanted. One when the caller named none.</summary>
        public int Page { get; }

        /// <summary>How many entries one page holds. The shared default when the caller named none.</summary>
        public int PageSize { get; }

        /// <summary>Case-insensitive substring of the message, or null for no search.</summary>
        public string? Search { get; }

        /// <summary>Stream to keep, or null for every stream.</summary>
        public Guid? StreamId { get; }

        /// <summary>Earliest <c>created_at</c> to keep, inclusive, or null for no lower bound.</summary>
        public DateTimeOffset? DateFrom { get; }

        /// <summary>Latest <c>created_at</c> to keep, inclusive, or null for no upper bound.</summary>
        public DateTimeOffset? DateTo { get; }

        /// <summary>Column to order by. <c>createdAt</c> when the caller named none.</summary>
        public StreamProblemSortField SortBy { get; }

        /// <summary>Direction. Descending when the caller named none.</summary>
        public StreamProblemSortOrder SortOrder { get; }

        /// <summary>
        /// Resolves the text of the query into the choices it names, or reports every parameter
        /// that named nothing.
        /// </summary>
        /// <param name="page">One-based page wanted; one when null.</param>
        /// <param name="pageSize">How many entries one page holds; the shared default when null.</param>
        /// <param name="search">Substring of the message, or null.</param>
        /// <param name="streamId">Stream to keep, as typed, or null.</param>
        /// <param name="dateFrom">Earliest creation instant, inclusive, or null.</param>
        /// <param name="dateTo">Latest creation instant, inclusive, or null.</param>
        /// <param name="sortBy">Column as typed; <c>createdAt</c> when absent or blank.</param>
        /// <param name="sortOrder">Direction as typed; <c>desc</c> when absent or blank.</param>
        /// <param name="errors">Rejected parameters, keyed by the query-string name.</param>
        /// <returns>The resolved query, or null when any parameter was refused.</returns>
        /// <remarks>
        /// Every parameter is judged before the answer is given, so one request that broke two of
        /// them is told about both at once. The key of each entry is the query-string name, which is
        /// what the transport hands back to the client unchanged.
        /// </remarks>
        public static StreamProblemQuery? TryParse(
            int? page,
            int? pageSize,
            string? search,
            string? streamId,
            string? dateFrom,
            string? dateTo,
            string? sortBy,
            string? sortOrder,
            out Dictionary<string, string[]> errors)
        {
            errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var wantedPage = page ?? 1;

            if (wantedPage < 1)
            {
                errors["page"] = ["The page must be at least 1."];
            }

            var wantedPageSize = pageSize ?? StreamProblemListLimits.DefaultPageSize;

            if (wantedPageSize < 1 || wantedPageSize > StreamProblemListLimits.MaxPageSize)
            {
                errors["pageSize"] =
                [
                    $"The page size must be between 1 and {StreamProblemListLimits.MaxPageSize}."
                ];
            }

            var wantedSearch = Trimmed(search);

            if ((wantedSearch?.Length ?? 0) > StreamProblemListLimits.MaxSearchLength)
            {
                errors["search"] =
                [
                    $"The search must be at most {StreamProblemListLimits.MaxSearchLength} characters."
                ];
            }

            var wantedField = IsBlank(sortBy)
                ? StreamProblemSortField.CreatedAt
                : StreamProblemQueryText.ParseSortField(sortBy);

            if (wantedField is null)
            {
                errors["sortBy"] = ["The sort field must be createdAt."];
            }

            var wantedOrder = IsBlank(sortOrder)
                ? StreamProblemSortOrder.Descending
                : StreamProblemQueryText.ParseSortOrder(sortOrder);

            if (wantedOrder is null)
            {
                errors["sortOrder"] = ["The sort order must be either asc or desc."];
            }

            var wantedStream = ParseIdentifier(streamId, "streamId", errors);

            var from = ParseInstant(dateFrom, "dateFrom", errors);
            var to = ParseInstant(dateTo, "dateTo", errors);

            if (errors.Count > 0)
            {
                return null;
            }

            return new StreamProblemQuery(
                wantedPage,
                wantedPageSize,
                wantedSearch,
                wantedStream,
                from,
                to,
                wantedField!.Value,
                wantedOrder!.Value);
        }

        /// <summary>Whether a query value is absent, empty or only whitespace.</summary>
        /// <param name="value">Value from a query string.</param>
        public static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);

        /// <summary>Reads an optional uuid, recording a value that is not one.</summary>
        private static Guid? ParseIdentifier(
            string? value,
            string parameter,
            Dictionary<string, string[]> errors)
        {
            if (IsBlank(value))
            {
                return null;
            }

            if (Guid.TryParse(value, out var parsed))
            {
                return parsed;
            }

            errors[parameter] =
            [
                "That value must be the identifier of the stream it names, as a uuid."
            ];

            return null;
        }

        /// <summary>
        /// Reads an optional creation-time bound: blank is "no bound", a value must be an ISO 8601
        /// date or date-time.
        /// </summary>
        /// <remarks>
        /// A blank value is no bound and an unparseable one is a refusal - never the other way
        /// round, because a bound read as absent would answer a list wider than the caller asked
        /// for.
        /// </remarks>
        private static DateTimeOffset? ParseInstant(
            string? value,
            string parameter,
            Dictionary<string, string[]> errors)
        {
            if (IsBlank(value))
            {
                return null;
            }

            var parsed = StreamProblemQueryText.ParseInstant(value);

            if (parsed is null)
            {
                errors[parameter] =
                [
                    "The bound must be an ISO 8601 date or date-time, for example 2026-05-01T10:00:00Z."
                ];
            }

            return parsed;
        }

        /// <summary>Trims a value, answering null when it is blank.</summary>
        private static string? Trimmed(string? value)
            => IsBlank(value) ? null : value!.Trim();
    }
}
