namespace Fluxy.Core.Requests.Enums
{
    /// <summary>Which way a page of the API request failure log runs.</summary>
    public enum ApiRequestFailureSortOrder
    {
        /// <summary>Oldest or lowest-numbered first.</summary>
        Ascending = 0,

        /// <summary>Newest or highest-numbered first.</summary>
        Descending = 1
    }
}
