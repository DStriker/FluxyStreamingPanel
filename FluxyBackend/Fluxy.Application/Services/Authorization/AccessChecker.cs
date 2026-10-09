using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Authentication;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

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
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessChecker"/> class.
        /// </summary>
        /// <param name="context">Context holding the accounts.</param>
        public AccessChecker(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<AccountStanding> GetStandingAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            // The projection is the point: one indexed read of an account and its group by
            // primary key, rather than materializing either entity. The password hash and the
            // registration code are not fetched at all, so a mistake that logged this would
            // have far less to leak.
            var row = await _context.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new
                {
                    user.Status,

                    // The null-forgiving operators are about the *declaration*, not about the
                    // database: the foreign key is NOT NULL and RESTRICT, so the join EF builds
                    // for a required relationship cannot leave the group out. The property is
                    // still nullable on purpose - a query that did not include it really does
                    // have nothing there, and ToModel() is the one place allowed to refuse -
                    // so the sites that did include it are the ones that say so.
                    GroupStatus = user.Group!.Status,
                    Role = user.Group!.Role,
                    Permissions = user.Group!.Permissions
                        .Select(grant => grant.Permission)
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (row is null)
            {
                // A token that names an account which no longer exists. Status null rather than
                // a state of its own, so the caller checks one thing.
                return new AccountStanding();
            }

            return new AccountStanding
            {
                Status = UserStatusComposition.Combine(row.Status, row.GroupStatus),
                Role = row.Role,
                Permissions = row.Permissions.ToHashSet()
            };
        }
    }
}
