using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Authentication;
using Fluxy.Core.Users.Repositories;
using Fluxy.Core.Users.Utils;

namespace Fluxy.Application.Services.Authorization
{
    /// <summary>
    /// Reads an account's current role, permissions and state from the database, so
    /// authorization is decided from the row rather than from a token.
    /// </summary>
    /// <remarks>
    /// This exists because a token cannot be the authority about itself. A signed token says
    /// what the account was when the token was written, and that statement is all it can ever
    /// say - there is no way to amend one without breaking its signature, which is the property
    /// that makes it verifiable at all. So an account demoted an hour ago keeps presenting its
    /// old role until the token expires, and a blocked account keeps a working token until then.
    ///
    /// The role and the permissions both come from the account's group, which is the whole
    /// reason a group holds them together: moving an account is one write that changes what
    /// level it has and what it may do inside that level, and this lookup is one read that
    /// answers both. The status is the effective one - the account's own row combined with the
    /// group's through <see cref="UserStatusComposition"/> - because "may this account act at
    /// all" has never depended on the account alone since a group could be blocked.
    ///
    /// Reading the row back closes that gap at the cost of one indexed lookup per authorized
    /// request. It is not cached, because a cache would have a lifetime and a stale answer is
    /// precisely the failure being prevented.
    /// </remarks>
    public sealed class AccessChecker : IAccessChecker
    {
        private readonly IUserRepository _users;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessChecker"/> class.
        /// </summary>
        /// <param name="users">Repository the standing is read from.</param>
        public AccessChecker(IUserRepository users)
        {
            _users = users;
        }

        /// <inheritdoc />
        public Task<AccountStanding> GetStandingAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            // The repository projects exactly these values: one indexed read of an account and
            // its group by primary key, with no password hash or registration code fetched at
            // all. A null Status means the token names an account that no longer exists.
            return _users.GetStandingAsync(userId, cancellationToken);
        }
    }
}
