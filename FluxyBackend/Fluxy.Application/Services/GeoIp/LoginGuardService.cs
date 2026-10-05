using System.Globalization;
using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.GeoIp;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fluxy.Application.Services.GeoIp
{
    /// <summary>
    /// Decides whether a network may open or keep a session of one account.
    /// </summary>
    /// <remarks>
    /// The rule this enforces is stated once here rather than spread over its two callers:
    /// between the three lists the relation is AND, inside one list it is OR, and an empty
    /// list does not restrict. That last part is what makes "only a country" expressible - the
    /// IP and provider lists stay empty and simply do not vote.
    /// </remarks>
    public sealed class LoginGuardService : ILoginGuardService
    {
        private readonly FluxyDbContext _context;
        private readonly ILogger<LoginGuardService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoginGuardService"/> class.
        /// </summary>
        /// <param name="context">Context holding the accounts and their rules.</param>
        /// <param name="logger">Logger the refusals are reported to.</param>
        public LoginGuardService(FluxyDbContext context, ILogger<LoginGuardService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<bool> IsAllowedAsync(
            Guid userId,
            GeoIpInfo where,
            CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            if (user is null)
            {
                _logger.LogInformation(
                    "Refused a guarded sign-in for a missing account {UserId}.",
                    userId);

                return false;
            }

            if (!user.GeoProtectionEnabled)
            {
                return true;
            }

            var rules = await _context.LoginGuardRules
                .AsNoTracking()
                .Where(rule => rule.UserId == userId)
                .ToListAsync(cancellationToken);

            if (rules.Count == 0)
            {
                // The guard is on but nothing is listed, which allows everything. Storing that
                // state is the caller's business to prevent - see the anti-lock check on the
                // profile path - and refusing here would lock the account with no way back but
                // the password reset.
                return true;
            }

            if (!LoginGuardPolicy.AllowsAddress(
                    rules.Where(rule => rule.Kind is LoginGuardRuleKind.IpAddress).Select(rule => rule.Value),
                    where))
            {
                LogRefusal(userId, where, "address");
                return false;
            }

            if (!LoginGuardPolicy.AllowsCountry(
                    rules.Where(rule => rule.Kind is LoginGuardRuleKind.Country).Select(rule => rule.Value),
                    where))
            {
                LogRefusal(userId, where, "country");
                return false;
            }

            var systems = rules
                .Where(rule => rule.Kind is LoginGuardRuleKind.AutonomousSystem)
                .Select(rule => rule.Value)
                .ToList();

            if (!LoginGuardPolicy.AllowsProvider(ParseSystems(systems), where))
            {
                LogRefusal(userId, where, "provider");
                return false;
            }

            return true;
        }

        private static IEnumerable<int> ParseSystems(IEnumerable<string> values)
        {
            foreach (var value in values)
            {
                if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var asn))
                {
                    yield return asn;
                }
            }
        }

        private void LogRefusal(Guid userId, GeoIpInfo where, string list)
        {
            _logger.LogInformation(
                "Refused a guarded sign-in of account {UserId} from {Ip} ({Country}/AS{Asn}): " +
                "the {List} is not on the account's allow lists.",
                userId,
                where.Ip,
                where.CountryCode ?? "unknown",
                where.AutonomousSystemNumber?.ToString(CultureInfo.InvariantCulture) ?? "unknown",
                list);
        }
    }
}
