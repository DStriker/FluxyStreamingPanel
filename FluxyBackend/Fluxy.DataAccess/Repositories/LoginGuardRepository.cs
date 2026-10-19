using System.Globalization;
using Fluxy.Core.Abstractions.Repositories;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
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
    internal sealed class LoginGuardRepository : ILoginGuardRepository
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

        /// <inheritdoc />
        public async Task ApplyAsync(
            Guid userId,
            NormalizedLoginGuard guard,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Users.FindAsync([userId], cancellationToken);

            if (entity is null)
            {
                return;
            }

            entity.GeoProtectionEnabled = guard.GeoProtectionEnabled;
            entity.BindSessionToIp = guard.BindSessionToIp;

            // Deleted before the new rows are added, so a list that drops a network does not
            // keep it beside the new one. The table gives every row to exactly one account and
            // nobody reads a row's identity, which is why this is delete-and-insert rather
            // than a diff.
            await _context.LoginGuardRules
                .Where(rule => rule.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            foreach (var ip in guard.AllowedIps)
            {
                _context.LoginGuardRules.Add(new UserLoginGuardRuleEntity
                {
                    UserId = userId,
                    Kind = LoginGuardRuleKind.IpAddress,
                    Value = ip
                });
            }

            if (guard.AllowedCountry is { } country)
            {
                _context.LoginGuardRules.Add(new UserLoginGuardRuleEntity
                {
                    UserId = userId,
                    Kind = LoginGuardRuleKind.Country,
                    Value = country
                });
            }

            if (guard.AllowedAutonomousSystemNumber is { } asn)
            {
                _context.LoginGuardRules.Add(new UserLoginGuardRuleEntity
                {
                    UserId = userId,
                    Kind = LoginGuardRuleKind.AutonomousSystem,
                    Value = asn.ToString(CultureInfo.InvariantCulture)
                });
            }
        }
    }
}
