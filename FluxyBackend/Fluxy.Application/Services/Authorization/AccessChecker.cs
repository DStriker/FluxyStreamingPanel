using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Authentication;
using Fluxy.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.Application.Services.Authorization
{
    /// <summary>
    /// Reads an account's current role and state from the database, so authorization is decided
    /// from the row rather than from a token.
    /// </summary>
    /// <remarks>
    /// This exists because a token cannot be the authority about itself. A signed token says
    /// what the account was when the token was written, and that statement is all it can ever
    /// say - there is no way to amend one without breaking its signature, which is the property
    /// that makes it verifiable at all. So an account demoted an hour ago keeps presenting its
    /// old role until the token expires, and a blocked account keeps a working token until then.
    ///
    /// Reading the row back closes that gap at the cost of one indexed lookup per authorized
    /// request. The lookup is the cheapest possible one: a single primary key read of two narrow
    /// columns. It is not cached, because a cache would have a lifetime and a stale answer is
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
            // The projection is the point: two columns of one row by primary key, rather than
            // materializing the entity. The password hash and the registration code are not
            // fetched at all, so a mistake that logged this would have far less to leak.
            var row = await _context.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new { user.Status, user.Role })
                .FirstOrDefaultAsync(cancellationToken);

            if (row is null)
            {
                // A token that names an account which no longer exists. Status null rather than
                // a state of its own, so the caller checks one thing.
                return new AccountStanding();
            }

            return new AccountStanding
            {
                Status = row.Status,
                Role = row.Role
            };
        }
    }
}
