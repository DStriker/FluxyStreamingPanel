using Fluxy.Core.Models.Authentication;

namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Reads the current state of an account, so that authorization can be decided from the
    /// database rather than from what a token happens to claim.
    /// </summary>
    /// <remarks>
    /// This is the one lookup that makes a signed token stop being the only word on the matter.
    /// A role written into a token describes the account at the moment it was issued and nothing
    /// later, so an account that was demoted, blocked or deleted keeps presenting the role it
    /// had until its tokens expired. Reading the row back on every authorized request is what
    /// makes a change take effect on the next call instead of up to a token lifetime later.
    ///
    /// It costs one query per authorized request, which is the price of that guarantee. There is
    /// no caching here on purpose: any cache would have a lifetime, and a stale answer is
    /// exactly the failure this type exists to prevent.
    /// </remarks>
    public interface IAccessChecker
    {
        /// <summary>
        /// Reads what an account's row says about it right now.
        /// </summary>
        /// <param name="userId">Identifier taken from the access token.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The state of the account. A missing row yields a standing whose status is null rather
        /// than a special case, so the caller has one thing to check.
        /// </returns>
        Task<AccountStanding> GetStandingAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
