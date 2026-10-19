using Fluxy.Core.GeoIp.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fluxy.Application.Users.Utils
{
    /// <summary>
    /// The one rule about skipping the login guard in Development, in one place.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The sign-in path, the refresh path and the anti-lock check each had their own copy of the
    /// same six lines, and two of the three were byte for byte the same. A rule that decides
    /// whether a network is refused has to be the same rule everywhere: a copy that drifts is a
    /// sign-in that refuses a developer while the profile page lets the same address through.
    /// </para>
    /// <para>
    /// The rule is deliberately narrow. Loopback, private and link-local addresses carry no
    /// GeoIP data by definition, so on a development machine - where every client is one of them
    /// - the guard would refuse its own developer. Outside Development there is no bypass at
    /// all: a local address on a public host is not the developer, it is a misconfiguration.
    /// </para>
    /// </remarks>
    public static class DevelopmentGuardBypass
    {
        /// <summary>
        /// Whether the guard is skipped for this address on the sign-in and refresh paths.
        /// </summary>
        /// <param name="clientAddress">The address the request came from.</param>
        /// <param name="environment">The environment the host runs in.</param>
        /// <param name="logger">Where the bypass is recorded.</param>
        /// <returns>True when the caller must not refuse the request for network reasons.</returns>
        public static bool BypassesEnforcement(
            string? clientAddress,
            IHostEnvironment environment,
            ILogger logger)
        {
            if (!IsLocalDevelopment(clientAddress, environment))
            {
                return false;
            }

            logger.LogWarning(
                "The login guard was bypassed for local address {ClientAddress} because this " +
                "host runs in Development.",
                clientAddress);

            return true;
        }

        /// <summary>
        /// Whether the anti-lock check is skipped for this address.
        /// </summary>
        /// <param name="clientAddress">The address the request came from.</param>
        /// <param name="environment">The environment the host runs in.</param>
        /// <param name="logger">Where the bypass is recorded.</param>
        /// <returns>True when the caller must not refuse its own network.</returns>
        public static bool BypassesAntiLock(
            string? clientAddress,
            IHostEnvironment environment,
            ILogger logger)
        {
            if (!IsLocalDevelopment(clientAddress, environment))
            {
                return false;
            }

            logger.LogWarning(
                "The login guard anti-lock check was bypassed for local address " +
                "{ClientAddress} because this host runs in Development.",
                clientAddress);

            return true;
        }

        private static bool IsLocalDevelopment(string? clientAddress, IHostEnvironment environment)
            => environment.IsDevelopment() && GeoIpAddress.IsLocal(clientAddress);
    }
}
