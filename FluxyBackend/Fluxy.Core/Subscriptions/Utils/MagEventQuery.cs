using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.Utils
{
    /// <summary>
    /// The choices a MAG event query names, resolved from the text the caller sent, or the map of
    /// parameters that could not be resolved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this is a rule and not transport code.</b> Resolving the page bounds, the search, the
    /// event filter, the subscription, the two date bounds, <c>sortBy</c> and <c>sortOrder</c> is a
    /// contract about what a client may ask for, and it has one answer: a value that names nothing
    /// is refused under its own parameter, never treated as "no filter". A mistyped event silently
    /// widening the list to every operation is the failure this type exists to prevent, so the rule
    /// is written once, here, where it can be tested without a running host; the controller keeps
    /// the HTTP half - the status code and the body.
    /// </para>
    /// <para>
    /// <b>Both date bounds are inclusive, independent, and an inverted range is valid.</b>
    /// <c>dateFrom</c> keeps rows whose <c>created_at</c> is greater than or equal to it and
    /// <c>dateTo</c> keeps rows whose <c>created_at</c> is less than or equal to it; either may be
    /// absent. Both ends are inclusive because the screen's picker names whole days. A later
    /// <c>dateFrom</c> than <c>dateTo</c> describes an interval with nothing in it: the answer is an
    /// empty page, not a refusal, because swapping the bounds would answer a different question.
    /// </para>
    /// <para>
    /// <b>Nothing is clamped.</b> The page bounds are the caller's to break and this type's to
    /// refuse; it does not silently narrow a request.
    /// </para>
    /// </remarks>
    public sealed class MagEventQuery
    {
        private MagEventQuery(
            int page,
            int pageSize,
            string? search,
            MagEventType? eventType,
            Guid? subscriptionId,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            MagEventSortField sortBy,
            MagEventSortOrder sortOrder)
        {
            Page = page;
            PageSize = pageSize;
            Search = search;
            EventType = eventType;
            SubscriptionId = subscriptionId;
            DateFrom = dateFrom;
            DateTo = dateTo;
            SortBy = sortBy;
            SortOrder = sortOrder;
        }

        /// <summary>One-based page wanted. One when the caller named none.</summary>
        public int Page { get; }

        /// <summary>How many events one page holds. The shared default when the caller named none.</summary>
        public int PageSize { get; }

        /// <summary>Case-insensitive substring of the message or the device MAC, or null.</summary>
        public string? Search { get; }

        /// <summary>Operation to keep, or null for every operation.</summary>
        public MagEventType? EventType { get; }

        /// <summary>Subscription to keep, or null for every subscription.</summary>
        public Guid? SubscriptionId { get; }

        /// <summary>Earliest <c>created_at</c> to keep, inclusive, or null for no lower bound.</summary>
        public DateTimeOffset? DateFrom { get; }

        /// <summary>Latest <c>created_at</c> to keep, inclusive, or null for no upper bound.</summary>
        public DateTimeOffset? DateTo { get; }

        /// <summary>Column to order by. <c>createdAt</c> when the caller named none.</summary>
        public MagEventSortField SortBy { get; }

        /// <summary>Direction. Descending when the caller named none.</summary>
        public MagEventSortOrder SortOrder { get; }

        /// <summary>
        /// Resolves the text of the query into the choices it names, or reports every parameter
        /// that named nothing.
        /// </summary>
        /// <param name="page">One-based page wanted; one when null.</param>
        /// <param name="pageSize">How many events one page holds; the shared default when null.</param>
        /// <param name="search">Substring of the message or the device MAC, or null.</param>
        /// <param name="eventType">Named operation, as typed, or null.</param>
        /// <param name="subscriptionId">Subscription to keep, as typed, or null.</param>
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
        public static MagEventQuery? TryParse(
            int? page,
            int? pageSize,
            string? search,
            string? eventType,
            string? subscriptionId,
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

            var wantedPageSize = pageSize ?? MagEventListLimits.DefaultPageSize;

            if (wantedPageSize < 1 || wantedPageSize > MagEventListLimits.MaxPageSize)
            {
                errors["pageSize"] =
                [
                    $"The page size must be between 1 and {MagEventListLimits.MaxPageSize}."
                ];
            }

            var wantedSearch = Trimmed(search);

            if ((wantedSearch?.Length ?? 0) > MagEventListLimits.MaxSearchLength)
            {
                errors["search"] =
                [
                    $"The search must be at most {MagEventListLimits.MaxSearchLength} characters."
                ];
            }

            MagEventType? wantedEvent = null;

            if (!IsBlank(eventType))
            {
                wantedEvent = MagEventQueryText.ParseEventType(eventType);

                if (wantedEvent is null)
                {
                    errors["event"] =
                    [
                        "The event must be one of: playChannel, closePortal, reloadPortal, " +
                        "rebootDevice, sendMessage, resetStbLock."
                    ];
                }
            }

            var wantedField = IsBlank(sortBy)
                ? MagEventSortField.CreatedAt
                : MagEventQueryText.ParseSortField(sortBy);

            if (wantedField is null)
            {
                errors["sortBy"] = ["The sort field must be createdAt."];
            }

            var wantedOrder = IsBlank(sortOrder)
                ? MagEventSortOrder.Desc
                : MagEventQueryText.ParseSortOrder(sortOrder);

            if (wantedOrder is null)
            {
                errors["sortOrder"] = ["The sort order must be either asc or desc."];
            }

            var wantedSubscription = ParseIdentifier(subscriptionId, "subscriptionId", errors);

            var from = ParseInstant(dateFrom, "dateFrom", errors);
            var to = ParseInstant(dateTo, "dateTo", errors);

            if (errors.Count > 0)
            {
                return null;
            }

            return new MagEventQuery(
                wantedPage,
                wantedPageSize,
                wantedSearch,
                wantedEvent,
                wantedSubscription,
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

            var parsed = MagEventQueryText.ParseInstant(value);

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
