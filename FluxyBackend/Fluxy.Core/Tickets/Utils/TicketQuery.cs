using Fluxy.Core.Tickets.Enums;

namespace Fluxy.Core.Tickets.Utils
{
    /// <summary>
    /// The choices a support ticket query names, resolved from the text the caller sent, or the map
    /// of parameters that could not be resolved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this is a rule and not transport code.</b> Resolving the page bounds, the search, the
    /// status filter, <c>sortBy</c> and <c>sortOrder</c> is a contract about what a client may ask
    /// for, and it has one answer: a value that names nothing is refused under its own parameter,
    /// never treated as "no filter". The rule lives here, beside the limits it reads, so it can be
    /// tested without a running host; the controller keeps the HTTP half - the status code and the
    /// body.
    /// </para>
    /// <para>
    /// <b>The status filter is viewer-relative, and that is why it is not a stored enum.</b> A
    /// client asks for <c>unread</c> or <c>read</c> and means "for me": the account's list reads
    /// the account's flag and the operator's list reads the operator's, while <c>closed</c> names
    /// the lifecycle both sides share. The resolved value is therefore one of the three words in
    /// <see cref="TicketStatuses"/>, not a <see cref="TicketStatus"/> member - the repository pairs
    /// it with the flag of the side whose page it is cutting.
    /// </para>
    /// <para>
    /// <b>Nothing is clamped.</b> The page bounds are the caller's to break and this type's to
    /// refuse; it does not silently narrow a request.
    /// </para>
    /// </remarks>
    public sealed class TicketQuery
    {
        private TicketQuery(
            int page,
            int pageSize,
            string? search,
            string? status,
            TicketSortField sortBy,
            TicketSortOrder sortOrder)
        {
            Page = page;
            PageSize = pageSize;
            Search = search;
            Status = status;
            SortBy = sortBy;
            SortOrder = sortOrder;
        }

        /// <summary>One-based page wanted. One when the caller named none.</summary>
        public int Page { get; }

        /// <summary>How many tickets one page holds. The shared default when the caller named none.</summary>
        public int PageSize { get; }

        /// <summary>Case-insensitive substring to look for, or null for no search.</summary>
        public string? Search { get; }

        /// <summary>
        /// One of <see cref="TicketStatuses.Unread"/>, <see cref="TicketStatuses.Read"/> or
        /// <see cref="TicketStatuses.Closed"/>, or null for every ticket. The first two are read
        /// against the viewer's own unread flag; the third is the stored lifecycle.
        /// </summary>
        public string? Status { get; }

        /// <summary>Column to order by. <c>lastReplyAt</c> when the caller named none.</summary>
        public TicketSortField SortBy { get; }

        /// <summary>Direction. Descending when the caller named none.</summary>
        public TicketSortOrder SortOrder { get; }

        /// <summary>
        /// Resolves the text of the query into the choices it names, or reports every parameter
        /// that named nothing.
        /// </summary>
        /// <param name="page">One-based page wanted; one when null.</param>
        /// <param name="pageSize">How many tickets one page holds; the shared default when null.</param>
        /// <param name="search">Substring to look for, or null.</param>
        /// <param name="status">Status word as typed, or null.</param>
        /// <param name="sortBy">Column as typed; <c>lastReplyAt</c> when absent or blank.</param>
        /// <param name="sortOrder">Direction as typed; <c>desc</c> when absent or blank.</param>
        /// <param name="errors">Rejected parameters, keyed by the query-string name.</param>
        /// <returns>The resolved query, or null when any parameter was refused.</returns>
        /// <remarks>
        /// Every parameter is judged before the answer is given, so one request that broke two of
        /// them is told about both at once. The key of each entry is the query-string name, which is
        /// what the transport hands back to the client unchanged.
        /// </remarks>
        public static TicketQuery? TryParse(
            int? page,
            int? pageSize,
            string? search,
            string? status,
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

            var wantedPageSize = pageSize ?? TicketLimits.DefaultPageSize;

            if (wantedPageSize < 1 || wantedPageSize > TicketLimits.MaxPageSize)
            {
                errors["pageSize"] =
                [
                    $"The page size must be between 1 and {TicketLimits.MaxPageSize}."
                ];
            }

            var wantedSearch = Trimmed(search);

            if ((wantedSearch?.Length ?? 0) > TicketLimits.MaxSearchLength)
            {
                errors["search"] =
                [
                    $"The search must be at most {TicketLimits.MaxSearchLength} characters."
                ];
            }

            var wantedStatus = IsBlank(status) ? null : TicketQueryText.ParseStatus(status);

            if (!IsBlank(status) && wantedStatus is null)
            {
                errors["status"] = ["The status must be one of: unread, read, closed."];
            }

            var wantedField = IsBlank(sortBy)
                ? TicketSortField.LastReplyAt
                : TicketQueryText.ParseSortField(sortBy);

            if (wantedField is null)
            {
                errors["sortBy"] =
                [
                    "The sort field must be one of: subject, username, createdAt, lastReplyAt."
                ];
            }

            var wantedOrder = IsBlank(sortOrder)
                ? TicketSortOrder.Descending
                : TicketQueryText.ParseSortOrder(sortOrder);

            if (wantedOrder is null)
            {
                errors["sortOrder"] = ["The sort order must be either asc or desc."];
            }

            if (errors.Count > 0)
            {
                return null;
            }

            return new TicketQuery(
                wantedPage,
                wantedPageSize,
                wantedSearch,
                wantedStatus,
                wantedField!.Value,
                wantedOrder!.Value);
        }

        /// <summary>Whether a query value is absent, empty or only whitespace.</summary>
        /// <param name="value">Value from a query string.</param>
        public static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);

        /// <summary>Trims a value, answering null when it is blank.</summary>
        private static string? Trimmed(string? value)
            => IsBlank(value) ? null : value!.Trim();
    }
}
