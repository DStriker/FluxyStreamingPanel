using Fluxy.Core.Models.Authentication;

namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Reads the visit history of an account that is already signed in.
    /// </summary>
    /// <remarks>
    /// Read only by construction: there is no method here that writes, because a history is a
    /// record of what happened and neither the account holding the session nor the operator
    /// reading it can retroactively change it. Rows appear when a token is issued and disappear
    /// when the existing cleanup retires them - nothing this interface can do affects either.
    ///
    /// The interface says nothing about HTTP, pages of JSON or status codes. It reports one page
    /// of visits or nothing at all, and which of those becomes a 403 is the transport's business.
    /// </remarks>
    public interface ISessionHistoryService
    {
        /// <summary>
        /// Reads one page of an account's visits, newest first.
        /// </summary>
        /// <param name="userId">Identifier taken from the access token.</param>
        /// <param name="page">One based page to read. Values below one are read as the first.</param>
        /// <param name="pageSize">
        /// How many visits the page holds, clamped to
        /// <see cref="SessionHistoryLimits.MaxPageSize"/>. The endpoint refuses an out of range
        /// value before this service sees it; the clamp is here so that a caller inside the
        /// application cannot ask for an unbounded read either.
        /// </param>
        /// <param name="currentSessionId">
        /// Session the caller is reading with, used to mark its own entries. Null when the token
        /// carried no session claim, which marks nothing rather than guessing.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The page, with <see cref="SessionHistoryPage.Total"/> counting every visit the account
        /// has, or null when the account is missing or is not registered - a blocked account is
        /// handed no history, the same rule the profile reads apply.
        /// </returns>
        Task<SessionHistoryPage?> GetHistoryAsync(
            Guid userId,
            int page,
            int pageSize,
            Guid? currentSessionId,
            CancellationToken cancellationToken = default);
    }
}
