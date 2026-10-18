namespace Fluxy.API.Controllers.Support
{
    /// <summary>
    /// Builds the error map of a paged list with the three bounds every list shares.
    /// </summary>
    /// <remarks>
    /// <c>page</c>, <c>pageSize</c> and <c>search</c> mean the same thing on every list of this
    /// API, and so do the sentences that describe breaking them - a client that has read one
    /// list's refusal has read them all. What a list adds on top - a status filter, a category,
    /// a sort field and the hint naming the values that exist - stays in the controller that
    /// owns it, because those values are different on every screen.
    /// </remarks>
    internal static class ListQueryErrors
    {
        /// <summary>
        /// The map a list action is refused with, holding whatever of the three common bounds
        /// the request broke.
        /// </summary>
        /// <param name="page">Page the client asked for.</param>
        /// <param name="pageSize">Page size the client asked for.</param>
        /// <param name="search">Search term the client sent, if any.</param>
        /// <param name="maxPageSize">Largest page size the resource accepts.</param>
        /// <param name="maxSearchLength">Longest search term the resource accepts.</param>
        public static Dictionary<string, string[]> Common(
            int page,
            int pageSize,
            string? search,
            int maxPageSize,
            int maxSearchLength)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (page < 1)
            {
                errors["page"] = ["The page must be at least 1."];
            }

            if (pageSize < 1 || pageSize > maxPageSize)
            {
                errors["pageSize"] = [$"The page size must be between 1 and {maxPageSize}."];
            }

            if ((search?.Length ?? 0) > maxSearchLength)
            {
                errors["search"] = [$"The search must be at most {maxSearchLength} characters."];
            }

            return errors;
        }
    }
}
