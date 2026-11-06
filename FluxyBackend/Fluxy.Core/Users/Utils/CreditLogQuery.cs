using Fluxy.Core.Users.Enums;

namespace Fluxy.Core.Users.Utils
{
    /// <summary>
    /// The choices a credit log query names, resolved from the text the caller sent, or the map of
    /// parameters that could not be resolved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this is a rule and not transport code.</b> Resolving <c>resellerId</c>, the two date
    /// bounds, <c>sortBy</c> and <c>sortOrder</c> is a contract about what a client may ask for,
    /// and it has one answer: a value that names no member is refused under its own parameter,
    /// never treated as "no filter". The rule lives here, beside the list it describes, so it can
    /// be tested without a running host; the controller keeps the HTTP half - the shared
    /// page/pageSize/search bounds, the status code and the body.
    /// </para>
    /// <para>
    /// <b>Both bounds are inclusive.</b> <c>dateFrom</c> keeps rows whose <c>created_at</c> is
    /// greater than or equal to it and <c>dateTo</c> keeps rows whose <c>created_at</c> is less
    /// than or equal to it; either may be absent, which means no bound on that side. Both ends are
    /// inclusive because the screen's picker names whole days: an end the operator picked as "the
    /// 1st" that dropped everything written during the 1st would answer a list narrower than the
    /// one the control shows. A bound that is blank is "no bound"; one that is present but not an
    /// ISO 8601 instant is refused under its own parameter.
    /// </para>
    /// <para>
    /// <b>Nothing is clamped.</b> The page bounds are the caller's to break and the transport's to
    /// refuse; this type does not silently narrow a request.
    /// </para>
    /// </remarks>
    public sealed class CreditLogQuery
    {
        private CreditLogQuery(
            Guid? resellerId,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            CreditLogSortField sortBy,
            CreditLogSortOrder sortOrder)
        {
            ResellerId = resellerId;
            DateFrom = dateFrom;
            DateTo = dateTo;
            SortBy = sortBy;
            SortOrder = sortOrder;
        }

        /// <summary>Reseller to keep, or null for every reseller.</summary>
        public Guid? ResellerId { get; }

        /// <summary>Earliest <c>created_at</c> to keep, inclusive, or null for no lower bound.</summary>
        public DateTimeOffset? DateFrom { get; }

        /// <summary>Latest <c>created_at</c> to keep, inclusive, or null for no upper bound.</summary>
        public DateTimeOffset? DateTo { get; }

        /// <summary>Column to order by. <c>createdAt</c> when the caller named none.</summary>
        public CreditLogSortField SortBy { get; }

        /// <summary>Direction. Descending when the caller named none.</summary>
        public CreditLogSortOrder SortOrder { get; }

        /// <summary>
        /// Resolves the text of the query into the choices it names, or reports every parameter
        /// that named nothing.
        /// </summary>
        /// <param name="resellerId">Reseller to keep, as typed, or null.</param>
        /// <param name="dateFrom">Earliest creation instant, inclusive, or null.</param>
        /// <param name="dateTo">Latest creation instant, inclusive, or null.</param>
        /// <param name="sortBy">Column as typed; <c>createdAt</c> when absent or blank.</param>
        /// <param name="sortOrder">Direction as typed; <c>desc</c> when absent or blank.</param>
        /// <param name="errors">Rejected parameters, keyed by the query-string name.</param>
        /// <returns>The resolved query, or null when any parameter was refused.</returns>
        /// <remarks>
        /// Every parameter is judged before the answer is given, so one request that broke two of
        /// them is told about both at once. The key of each entry is the query-string name, which
        /// is what the transport hands back to the client unchanged.
        /// </remarks>
        public static CreditLogQuery? TryParse(
            string? resellerId,
            string? dateFrom,
            string? dateTo,
            string? sortBy,
            string? sortOrder,
            out Dictionary<string, string[]> errors)
        {
            errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var wantedField = IsBlank(sortBy)
                ? CreditLogSortField.CreatedAt
                : CreditLogQueryText.ParseSortField(sortBy);

            if (wantedField is null)
            {
                errors["sortBy"] = ["The sort field must be one of: createdAt, amount."];
            }

            var wantedOrder = IsBlank(sortOrder)
                ? CreditLogSortOrder.Descending
                : CreditLogQueryText.ParseSortOrder(sortOrder);

            if (wantedOrder is null)
            {
                errors["sortOrder"] = ["The sort order must be either asc or desc."];
            }

            var reseller = ParseIdentifier(resellerId, errors);
            var from = ParseInstant(dateFrom, "dateFrom", errors);
            var to = ParseInstant(dateTo, "dateTo", errors);

            if (errors.Count > 0)
            {
                return null;
            }

            return new CreditLogQuery(
                reseller,
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

            errors["resellerId"] =
            [
                "That value must be the identifier of the item it names, as a uuid."
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

            var parsed = CreditLogQueryText.ParseInstant(value);

            if (parsed is null)
            {
                errors[parameter] =
                [
                    "The bound must be an ISO 8601 date or date-time, for example 2026-05-01T10:00:00Z."
                ];
            }

            return parsed;
        }
    }
}
