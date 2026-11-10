namespace Fluxy.Core.Requests.Enums
{
    /// <summary>
    /// The two stored columns the API request failure log may be ordered by.
    /// </summary>
    /// <remarks>
    /// Both are columns of <c>api_request_failures</c> itself. Nothing joined or resolved on read
    /// is sortable: an order built on a value the server only has after the page was cut would
    /// order a page that had already been chosen, which is the same lie a client-side sort tells.
    /// The reason is sortable because grouping the refusals of one kind is how an operator reads
    /// the log.
    /// </remarks>
    public enum ApiRequestFailureSortField
    {
        /// <summary>When the failure was recorded. The default, and the newest first.</summary>
        CreatedAt = 0,

        /// <summary>Why the request was refused.</summary>
        Reason = 1
    }
}
