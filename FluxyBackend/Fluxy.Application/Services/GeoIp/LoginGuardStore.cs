using System.Globalization;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.Application.Services.GeoIp
{
    /// <summary>
    /// Writes the allow lists of one account: delete what it held, insert what it was given.
    /// </summary>
    /// <remarks>
    /// Shared by the profile service and the admin service for one narrow reason - there is
    /// exactly one shape the <c>user_login_guard_rules</c> table may be written in, and a
    /// second copy of it would be a second source for the fact of which networks an account
    /// accepts. The switches are *not* written here: they are columns of the account row and
    /// the caller may already hold that row for reasons of its own, so it sets them and this
    /// class saves them with the rules in one write, the way the profile service has always
    /// done it.
    /// </remarks>
    public static class LoginGuardStore
    {
        /// <summary>
        /// Replaces the rules of <paramref name="userId"/> with <paramref name="guard"/>'s
        /// lists and saves everything the caller has staged on the account row as well.
        /// </summary>
        /// <param name="context">Context holding the rules.</param>
        /// <param name="userId">Account whose lists are replaced.</param>
        /// <param name="guard">The lists to store. Already canonical.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// The old rows are deleted first, so a guard that drops a network does not keep it
        /// beside the new one. Every row of the table belongs to exactly one account and has
        /// no identity anybody reads, which is why delete-and-insert rather than a diff: a diff
        /// would be more writes in the common case only when the lists did not change at all,
        /// and it would still have to prove the same thing about duplicates.
        /// </remarks>
        public static async Task ReplaceRulesAsync(
            FluxyDbContext context,
            Guid userId,
            NormalizedLoginGuard guard,
            CancellationToken cancellationToken)
        {
            await context.LoginGuardRules
                .Where(rule => rule.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            foreach (var ip in guard.AllowedIps)
            {
                context.LoginGuardRules.Add(new UserLoginGuardRuleEntity
                {
                    UserId = userId,
                    Kind = LoginGuardRuleKind.IpAddress,
                    Value = ip
                });
            }

            if (guard.AllowedCountry is { } country)
            {
                context.LoginGuardRules.Add(new UserLoginGuardRuleEntity
                {
                    UserId = userId,
                    Kind = LoginGuardRuleKind.Country,
                    Value = country
                });
            }

            if (guard.AllowedAutonomousSystemNumber is { } asn)
            {
                context.LoginGuardRules.Add(new UserLoginGuardRuleEntity
                {
                    UserId = userId,
                    Kind = LoginGuardRuleKind.AutonomousSystem,
                    Value = asn.ToString(CultureInfo.InvariantCulture)
                });
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
