using Fluxy.Core.Abstractions.Repositories;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="ILoginGuardRepository"/>.
    /// </summary>
    /// <remarks>
    /// Two reads rather than one join: the switch belongs to the account row and the rules are
    /// rows of their own, so a single query would either repeat the switch once per rule or
    /// hide that the two questions are separate. The service asks both, in the order it has
    /// always asked them.
    /// </remarks>
    public sealed class LoginGuardRepository : ILoginGuardRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoginGuardRepository"/> class.
        /// </summary>
        /// <param name="context">Context the switch and the rules are read from.</param>
        public LoginGuardRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public Task<bool?> GetGeoProtectionEnabledAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
            => _context.Users
                .AsNoTracking()
                .Where(entry => entry.Id == userId)
                .Select(entry => (bool?)entry.GeoProtectionEnabled)
                .FirstOrDefaultAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<IReadOnlyList<LoginGuardRule>> GetRulesAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
            => await _context.LoginGuardRules
                .AsNoTracking()
                .Where(rule => rule.UserId == userId)
                .Select(rule => new LoginGuardRule
                {
                    Kind = rule.Kind,
                    Value = rule.Value
                })
                .ToListAsync(cancellationToken);
    }
}
