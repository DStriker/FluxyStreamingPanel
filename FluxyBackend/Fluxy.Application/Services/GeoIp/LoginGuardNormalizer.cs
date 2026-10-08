using Fluxy.Core.Models.Users;

namespace Fluxy.Application.Services.GeoIp
{
    /// <summary>
    /// Turns a requested login guard into its canonical form plus everything wrong with it.
    /// </summary>
    /// <remarks>
    /// One copy of these rules exists for two callers: the profile service, where the owner of
    /// the account sets a guard on themself, and the admin service, where an operator sets one
    /// on somebody else. The alternative was a second copy of the same five checks, and the day
    /// the two stopped agreeing an address would be accepted by one form and refused by the
    /// other with nothing in the code to say which one was right.
    ///
    /// What is *not* shared is the anti-lock check, because it is a different rule wearing the
    /// same clothes: on the profile it refuses a guard that would lock out the person saving
    /// it, and it asks for their own client address to decide. An admin editing another
    /// account has no such address to check - the network being pinned is that account's, not
    /// theirs - so the check does not apply and is deliberately not attempted.
    ///
    /// Errors are keyed by the C# name of the property that failed, the same convention every
    /// other service here follows; the transport is what turns them into the camelCase names a
    /// form reads.
    /// </remarks>
    public static class LoginGuardNormalizer
    {
        /// <summary>
        /// Canonical form of <paramref name="settings"/> plus everything wrong with it.
        /// </summary>
        /// <param name="settings">The guard as the caller spelled it.</param>
        /// <returns>
        /// The value to store - always produced, so a caller that only wants to render one
        /// never has to check the errors - and the errors, empty when the guard is acceptable.
        /// </returns>
        public static (NormalizedLoginGuard Value, Dictionary<string, string[]> Errors) Normalize(
            LoginGuardSettings settings)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var ips = (settings.AllowedIps ?? [])
                .Select(LoginGuardPolicy.NormalizeIpEntry)
                .ToList();

            if (ips.Any(entry => entry is null))
            {
                errors[nameof(LoginGuardSettings.AllowedIps)] =
                [
                    "Every address must be an IP address or a CIDR range, IPv4 or IPv6."
                ];
            }

            var canonicalIps = ips
                .Where(entry => entry is not null)
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (canonicalIps.Length > LoginGuardPolicy.MaxAllowedIps)
            {
                errors[nameof(LoginGuardSettings.AllowedIps)] =
                [
                    $"No more than {LoginGuardPolicy.MaxAllowedIps} addresses may be allowed."
                ];
            }

            string? country = null;
            if (!string.IsNullOrWhiteSpace(settings.AllowedCountry))
            {
                country = LoginGuardPolicy.NormalizeCountry(settings.AllowedCountry);
                if (country is null)
                {
                    errors[nameof(LoginGuardSettings.AllowedCountry)] =
                    [
                        "The country must be an ISO 3166-1 alpha-2 code this system knows."
                    ];
                }
            }

            int? asn = null;
            if (settings.AllowedAutonomousSystemNumber is { } requested)
            {
                if (!LoginGuardPolicy.IsAutonomousSystemNumber(requested))
                {
                    errors[nameof(LoginGuardSettings.AllowedAutonomousSystemNumber)] =
                    [
                        "The provider must be an autonomous system number greater than zero."
                    ];
                }
                else
                {
                    asn = requested;
                }
            }

            return (
                new NormalizedLoginGuard(
                    settings.GeoProtectionEnabled,
                    settings.BindSessionToIp,
                    canonicalIps,
                    country,
                    asn),
                errors);
        }
    }
}
