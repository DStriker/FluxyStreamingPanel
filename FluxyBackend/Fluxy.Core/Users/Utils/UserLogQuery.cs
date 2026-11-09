using Fluxy.Core.Users.Enums;

namespace Fluxy.Core.Users.Utils
{
    /// <summary>
    /// The choices a user log query names, resolved from the text the caller sent, or the map of
    /// parameters that could not be resolved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this is a rule and not transport code.</b> Resolving the three repeated filters, the
    /// three network filters, the two date bounds, <c>sortBy</c> and <c>sortOrder</c> is a contract
    /// about what a client may ask for, and it has one answer: a value that names no member is
    /// refused under its own parameter, never treated as "no filter". The rule lives here, beside the
    /// list it describes, so it can be tested without a running host; the controller keeps the HTTP
    /// half - the status code and the body.
    /// </para>
    /// <para>
    /// <b>The repeated filters are repeated on purpose.</b> <c>userIds</c>, <c>groupIds</c> and
    /// <c>recordTypes</c> each arrive as the query parameter spelled once per element, so a request
    /// naming two accounts is <c>?userIds=...&amp;userIds=...</c>. Each element is judged on its
    /// own: one bad element refuses the whole filter under the parameter's name, because dropping it
    /// would answer a list wider than the one the caller asked for.
    /// </para>
    /// <para>
    /// <b>Both bounds are inclusive.</b> <c>dateFrom</c> keeps rows whose <c>created_at</c> is
    /// greater than or equal to it and <c>dateTo</c> keeps rows whose <c>created_at</c> is less than
    /// or equal to it; either may be absent, which means no bound on that side. Both ends are
    /// inclusive because the screen's picker names whole days. A bound that is blank is "no bound";
    /// one that is present but not an ISO 8601 instant is refused under its own parameter.
    /// </para>
    /// <para>
    /// <b>A later <c>dateFrom</c> than <c>dateTo</c> is a valid query.</b> It describes an interval
    /// with nothing in it, and the answer is 200 with an empty page. Refusing it would turn a
    /// perfectly well-formed question into an error, and swapping the bounds would answer a
    /// different question from the one the caller asked.
    /// </para>
    /// <para>
    /// <b>The network filters are the connection log's, so one cell can write both screens.</b>
    /// <c>ip</c> and <c>isp</c> keep the stored value exactly, case included, and <c>country</c> is
    /// canonicalised to upper-case ISO alpha-2 - the same three rules <c>ConnectionLogQuery</c>
    /// applies and the same parameter names <c>ConnectionNetworkCell</c> emits. A blank value is
    /// "no filter" for all three. A value longer than its column is refused rather than passed on:
    /// it can never equal a stored row, and answering an empty page for a filter the caller plainly
    /// meant would hide the mistake behind a plausible answer.
    /// </para>
    /// <para>
    /// <b>Nothing is clamped.</b> The page bounds are the caller's to break and this type's to
    /// refuse; it does not silently narrow a request.
    /// </para>
    /// </remarks>
    public sealed class UserLogQuery
    {
        private UserLogQuery(
            int page,
            int pageSize,
            string? search,
            IReadOnlyList<Guid>? userIds,
            IReadOnlyList<Guid>? groupIds,
            IReadOnlyList<UserLogRecordType>? recordTypes,
            string? ip,
            string? countryCode,
            string? isp,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            UserLogSortField sortBy,
            UserLogSortOrder sortOrder)
        {
            Page = page;
            PageSize = pageSize;
            Search = search;
            UserIds = userIds;
            GroupIds = groupIds;
            RecordTypes = recordTypes;
            Ip = ip;
            CountryCode = countryCode;
            Isp = isp;
            DateFrom = dateFrom;
            DateTo = dateTo;
            SortBy = sortBy;
            SortOrder = sortOrder;
        }

        /// <summary>One-based page wanted. One when the caller named none.</summary>
        public int Page { get; }

        /// <summary>How many entries one page holds. The shared default when the caller named none.</summary>
        public int PageSize { get; }

        /// <summary>
        /// Case-insensitive substring of the description or the account's login name, or null for
        /// no search.
        /// </summary>
        public string? Search { get; }

        /// <summary>Accounts to keep, deduplicated and in the order sent, or null for every account.</summary>
        public IReadOnlyList<Guid>? UserIds { get; }

        /// <summary>Groups to keep, deduplicated and in the order sent, or null for every group.</summary>
        public IReadOnlyList<Guid>? GroupIds { get; }

        /// <summary>
        /// Record types to keep, deduplicated and in the order sent, or null for every type.
        /// </summary>
        public IReadOnlyList<UserLogRecordType>? RecordTypes { get; }

        /// <summary>Address to keep, compared exactly, or null.</summary>
        public string? Ip { get; }

        /// <summary>Upper-case ISO alpha-2 country to keep, or null.</summary>
        public string? CountryCode { get; }

        /// <summary>Provider to keep, compared exactly, or null.</summary>
        public string? Isp { get; }

        /// <summary>Earliest <c>created_at</c> to keep, inclusive, or null for no lower bound.</summary>
        public DateTimeOffset? DateFrom { get; }

        /// <summary>Latest <c>created_at</c> to keep, inclusive, or null for no upper bound.</summary>
        public DateTimeOffset? DateTo { get; }

        /// <summary>Column to order by. <c>createdAt</c> when the caller named none.</summary>
        public UserLogSortField SortBy { get; }

        /// <summary>Direction. Descending when the caller named none.</summary>
        public UserLogSortOrder SortOrder { get; }

        /// <summary>
        /// Resolves the text of the query into the choices it names, or reports every parameter
        /// that named nothing.
        /// </summary>
        /// <param name="page">One-based page wanted; one when null.</param>
        /// <param name="pageSize">How many entries one page holds; the shared default when null.</param>
        /// <param name="search">Substring of the description or the login name, or null.</param>
        /// <param name="userIds">Accounts to keep, as typed, or null.</param>
        /// <param name="groupIds">Groups to keep, as typed, or null.</param>
        /// <param name="recordTypes">Record types to keep, as typed, or null.</param>
        /// <param name="ip">Address to keep exactly, or null.</param>
        /// <param name="country">Country as typed, any case, or null.</param>
        /// <param name="isp">Provider to keep exactly, or null.</param>
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
        public static UserLogQuery? TryParse(
            int? page,
            int? pageSize,
            string? search,
            IReadOnlyList<string>? userIds,
            IReadOnlyList<string>? groupIds,
            IReadOnlyList<string>? recordTypes,
            string? ip,
            string? country,
            string? isp,
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

            var wantedPageSize = pageSize ?? UserLogListLimits.DefaultPageSize;

            if (wantedPageSize < 1 || wantedPageSize > UserLogListLimits.MaxPageSize)
            {
                errors["pageSize"] =
                [
                    $"The page size must be between 1 and {UserLogListLimits.MaxPageSize}."
                ];
            }

            var wantedSearch = Trimmed(search);

            if ((wantedSearch?.Length ?? 0) > UserLogListLimits.MaxSearchLength)
            {
                errors["search"] =
                [
                    $"The search must be at most {UserLogListLimits.MaxSearchLength} characters."
                ];
            }

            var wantedField = IsBlank(sortBy)
                ? UserLogSortField.CreatedAt
                : UserLogQueryText.ParseSortField(sortBy);

            if (wantedField is null)
            {
                errors["sortBy"] = ["The sort field must be one of: createdAt, recordType."];
            }

            var wantedOrder = IsBlank(sortOrder)
                ? UserLogSortOrder.Descending
                : UserLogQueryText.ParseSortOrder(sortOrder);

            if (wantedOrder is null)
            {
                errors["sortOrder"] = ["The sort order must be either asc or desc."];
            }

            var wantedUsers = ParseIdentifiers(userIds, "userIds", errors);
            var wantedGroups = ParseIdentifiers(groupIds, "groupIds", errors);
            var wantedTypes = ParseRecordTypes(recordTypes, errors);

            var wantedIp = ParseNetworkValue(ip, "ip", UserLogListLimits.IpMaxLength, errors);
            var wantedIsp = ParseNetworkValue(isp, "isp", UserLogListLimits.IspMaxLength, errors);
            var wantedCountry = ParseCountry(country, errors);

            var from = ParseInstant(dateFrom, "dateFrom", errors);
            var to = ParseInstant(dateTo, "dateTo", errors);

            if (errors.Count > 0)
            {
                return null;
            }

            return new UserLogQuery(
                wantedPage,
                wantedPageSize,
                wantedSearch,
                wantedUsers,
                wantedGroups,
                wantedTypes,
                wantedIp,
                wantedCountry,
                wantedIsp,
                from,
                to,
                wantedField!.Value,
                wantedOrder!.Value);
        }

        /// <summary>Whether a query value is absent, empty or only whitespace.</summary>
        /// <param name="value">Value from a query string.</param>
        public static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);

        /// <summary>
        /// Reads an optional repeated uuid filter: null or empty is "no filter", and the whole
        /// filter is refused when an element is not a uuid or there are too many of them.
        /// </summary>
        private static IReadOnlyList<Guid>? ParseIdentifiers(
            IReadOnlyList<string>? values,
            string parameter,
            Dictionary<string, string[]> errors)
        {
            if (values is null || values.Count == 0)
            {
                return null;
            }

            if (values.Count > UserLogListLimits.MaxFilterValues)
            {
                errors[parameter] =
                [
                    $"At most {UserLogListLimits.MaxFilterValues} values may be sent."
                ];

                return null;
            }

            var parsed = new List<Guid>(values.Count);

            foreach (var value in values)
            {
                // A blank element is not "no filter": the caller named a value that is not there,
                // and dropping it would answer a set larger than the one the request described.
                if (!Guid.TryParse(value, out var id))
                {
                    errors[parameter] =
                    [
                        "Every value must be the identifier of the item it names, as a uuid."
                    ];

                    return null;
                }

                if (!parsed.Contains(id))
                {
                    parsed.Add(id);
                }
            }

            return parsed;
        }

        /// <summary>
        /// Reads an optional repeated record-type filter: null or empty is "no filter", and the
        /// whole filter is refused when an element names no record type or there are too many.
        /// </summary>
        private static IReadOnlyList<UserLogRecordType>? ParseRecordTypes(
            IReadOnlyList<string>? values,
            Dictionary<string, string[]> errors)
        {
            if (values is null || values.Count == 0)
            {
                return null;
            }

            if (values.Count > UserLogListLimits.MaxFilterValues)
            {
                errors["recordTypes"] =
                [
                    $"At most {UserLogListLimits.MaxFilterValues} values may be sent."
                ];

                return null;
            }

            var parsed = new List<UserLogRecordType>(values.Count);

            foreach (var value in values)
            {
                if (!UserLogRecordTypes.TryParseWire(value, out var recordType))
                {
                    errors["recordTypes"] = [UserLogRecordTypes.UnknownRecordTypeHint];

                    return null;
                }

                if (!parsed.Contains(recordType))
                {
                    parsed.Add(recordType);
                }
            }

            return parsed;
        }

        /// <summary>
        /// Reads an optional exact filter (<c>ip</c> or <c>isp</c>): blank is "no filter", and a
        /// value longer than the column could hold is refused rather than passed on.
        /// </summary>
        /// <remarks>
        /// <b>Exact, and case included</b> - the same rule the connection log applies to the same
        /// two facts, because the stored value is a snapshot and folding its case would answer rows
        /// for an address or provider the log never recorded. The length bound is the column's:
        /// a value that cannot fit can never equal a stored one, and answering an empty page for a
        /// filter the caller plainly meant would hide the mistake instead of naming it.
        /// </remarks>
        private static string? ParseNetworkValue(
            string? value,
            string parameter,
            int maxLength,
            Dictionary<string, string[]> errors)
        {
            var trimmed = Trimmed(value);

            if (trimmed is null)
            {
                return null;
            }

            if (trimmed.Length > maxLength)
            {
                errors[parameter] =
                [
                    $"The value must be at most {maxLength} characters."
                ];

                return null;
            }

            return trimmed;
        }

        /// <summary>
        /// Reads an optional country: blank is "no filter", a value must name a code this
        /// installation knows.
        /// </summary>
        /// <remarks>
        /// The stored column is upper-case ISO alpha-2 by construction, so the filter is
        /// canonicalised the way the connection log's is: a typed <c>de</c> means <c>DE</c>, and
        /// only a value that names no code at all is refused - never dropped, because dropping it
        /// would answer every country under the name of one.
        /// </remarks>
        private static string? ParseCountry(
            string? value,
            Dictionary<string, string[]> errors)
        {
            if (IsBlank(value))
            {
                return null;
            }

            var normalized = UserLogQueryText.NormalizeCountry(value);

            if (normalized is null)
            {
                errors["country"] =
                [
                    "The country must be an ISO 3166-1 alpha-2 code, for example DE."
                ];
            }

            return normalized;
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

            var parsed = UserLogQueryText.ParseInstant(value);

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
