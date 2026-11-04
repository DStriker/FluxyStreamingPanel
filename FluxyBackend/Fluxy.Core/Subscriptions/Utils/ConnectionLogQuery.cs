using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.Utils
{
    /// <summary>
    /// The choices a connection log query names, resolved from the text the caller sent, or the
    /// map of parameters that could not be resolved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this is a rule and not transport code.</b> Resolving <c>kind</c>, <c>state</c>,
    /// <c>sortBy</c> and <c>sortOrder</c> is a contract about what a client may ask for, and it
    /// has one answer: a value that names no member is refused under its own parameter, never
    /// treated as "no filter". A mistyped medium silently widening the list to both media is the
    /// failure this type exists to prevent, so the rule is written once, here, where it can be
    /// tested without a running host - the same seam <c>M3uPlaylistParser</c> is. The controller
    /// keeps the HTTP half: the shared page/pageSize/search bounds, the status code and the body.
    /// </para>
    /// <para>
    /// <b>Defaults belong to the screen.</b> An absent <c>kind</c> is <c>video</c> and an absent
    /// <c>state</c> is <c>all</c>, because those are the two answers the untouched log screen
    /// starts from; the same values spelled as empty text mean the same thing. Nothing is clamped:
    /// the bounds are the caller's to break and the transport's to refuse.
    /// </para>
    /// <para>
    /// <b>Country is canonicalised, not merely filtered on.</b> The column stores upper-case ISO
    /// alpha-2, so a caller typing <c>de</c> gets <c>DE</c>; a value that is not a code this
    /// installation knows is refused under <c>country</c> rather than dropped, because dropping it
    /// would answer every country under the name of one.
    /// </para>
    /// <para>
    /// <b>The address, the provider and the user agent are exact filters and nothing more.</b> A
    /// blank value is "no filter" and a value is compared as stored, case included: a user agent
    /// is a client's own string, and folding its case would answer rows for a client that never
    /// sent that string. It is the same rule <c>ip</c> and <c>isp</c> follow; only <c>country</c>
    /// is canonicalised, because its column is canonical by construction.
    /// </para>
    /// </remarks>
    public sealed class ConnectionLogQuery
    {
        private ConnectionLogQuery(
            ConnectionStreamKind streamKind,
            bool liveOnly,
            Guid? subscriptionId,
            Guid? ownerId,
            Guid? streamId,
            string? ip,
            string? countryCode,
            string? isp,
            string? userAgent,
            ConnectionLogSortField sortBy,
            ConnectionLogSortOrder sortOrder)
        {
            StreamKind = streamKind;
            LiveOnly = liveOnly;
            SubscriptionId = subscriptionId;
            OwnerId = ownerId;
            StreamId = streamId;
            Ip = ip;
            CountryCode = countryCode;
            Isp = isp;
            UserAgent = userAgent;
            SortBy = sortBy;
            SortOrder = sortOrder;
        }

        /// <summary>Medium to keep. Always resolved: <c>video</c> when the caller named none.</summary>
        public ConnectionStreamKind StreamKind { get; }

        /// <summary>True to keep only the sessions that have not ended.</summary>
        public bool LiveOnly { get; }

        /// <summary>Subscription to keep, or null for every subscription.</summary>
        public Guid? SubscriptionId { get; }

        /// <summary>Owner of the subscription to keep, or null for every owner.</summary>
        public Guid? OwnerId { get; }

        /// <summary>Opaque stream identifier to keep, or null for every stream.</summary>
        public Guid? StreamId { get; }

        /// <summary>Address to keep, compared exactly, or null.</summary>
        public string? Ip { get; }

        /// <summary>Upper-case ISO alpha-2 country to keep, or null.</summary>
        public string? CountryCode { get; }

        /// <summary>Provider to keep, compared exactly, or null.</summary>
        public string? Isp { get; }

        /// <summary>User agent to keep, compared exactly, or null.</summary>
        public string? UserAgent { get; }

        /// <summary>Column to order by. <c>startedAt</c> when the caller named none.</summary>
        public ConnectionLogSortField SortBy { get; }

        /// <summary>Direction. Descending when the caller named none.</summary>
        public ConnectionLogSortOrder SortOrder { get; }

        /// <summary>
        /// Resolves the text of the query into the choices it names, or reports every parameter
        /// that named nothing.
        /// </summary>
        /// <param name="kind">Medium as typed; <c>video</c> when absent or blank.</param>
        /// <param name="state">State as typed; <c>all</c> when absent or blank.</param>
        /// <param name="subscriptionId">Subscription to keep, as typed, or null.</param>
        /// <param name="ownerId">Owner to keep, as typed, or null.</param>
        /// <param name="streamId">Stream to keep, as typed, or null.</param>
        /// <param name="ip">Address to keep exactly, or null.</param>
        /// <param name="country">Country as typed, any case, or null.</param>
        /// <param name="isp">Provider to keep exactly, or null.</param>
        /// <param name="userAgent">User agent to keep exactly, or null.</param>
        /// <param name="sortBy">Column as typed; <c>startedAt</c> when absent or blank.</param>
        /// <param name="sortOrder">Direction as typed; <c>desc</c> when absent or blank.</param>
        /// <param name="errors">Rejected parameters, keyed by the query-string name.</param>
        /// <returns>The resolved query, or null when any parameter was refused.</returns>
        /// <remarks>
        /// Every parameter is judged before the answer is given, so one request that broke two of
        /// them is told about both at once. The key of each entry is the query-string name, which
        /// is what the transport hands back to the client unchanged.
        /// </remarks>
        public static ConnectionLogQuery? TryParse(
            string? kind,
            string? state,
            string? subscriptionId,
            string? ownerId,
            string? streamId,
            string? ip,
            string? country,
            string? isp,
            string? userAgent,
            string? sortBy,
            string? sortOrder,
            out Dictionary<string, string[]> errors)
        {
            errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var wantedKind = IsBlank(kind)
                ? ConnectionStreamKind.VideoStream
                : ConnectionLogQueryText.ParseStreamKind(kind);

            if (wantedKind is null)
            {
                errors["kind"] = ["The stream kind must be either video or audio."];
            }

            var wantedLiveOnly = IsBlank(state)
                ? false
                : ConnectionLogQueryText.ParseLiveOnly(state);

            if (wantedLiveOnly is null)
            {
                errors["state"] = ["The state must be either live or all."];
            }

            var wantedField = IsBlank(sortBy)
                ? ConnectionLogSortField.StartedAt
                : ConnectionLogQueryText.ParseSortField(sortBy);

            if (wantedField is null)
            {
                errors["sortBy"] = ["The sort field must be one of: startedAt, endedAt."];
            }

            var wantedOrder = IsBlank(sortOrder)
                ? ConnectionLogSortOrder.Descending
                : ConnectionLogQueryText.ParseSortOrder(sortOrder);

            if (wantedOrder is null)
            {
                errors["sortOrder"] = ["The sort order must be either asc or desc."];
            }

            var subscription = ParseIdentifier(subscriptionId, "subscriptionId", errors);
            var owner = ParseIdentifier(ownerId, "ownerId", errors);
            var stream = ParseIdentifier(streamId, "streamId", errors);

            var wantedCountry = ParseCountry(country, errors);

            if (errors.Count > 0)
            {
                return null;
            }

            return new ConnectionLogQuery(
                wantedKind!.Value,
                wantedLiveOnly!.Value,
                subscription,
                owner,
                stream,
                Trimmed(ip),
                wantedCountry,
                Trimmed(isp),
                Trimmed(userAgent),
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
        /// Reads an optional country: blank is "no filter", a value must name a code.
        /// </summary>
        private static string? ParseCountry(
            string? value,
            Dictionary<string, string[]> errors)
        {
            if (IsBlank(value))
            {
                return null;
            }

            var normalized = ConnectionLogQueryText.NormalizeCountry(value);

            if (normalized is null)
            {
                errors["country"] =
                [
                    "The country must be an ISO 3166-1 alpha-2 code, for example DE."
                ];
            }

            return normalized;
        }

        private static string? Trimmed(string? value)
            => IsBlank(value) ? null : value!.Trim();
    }
}
