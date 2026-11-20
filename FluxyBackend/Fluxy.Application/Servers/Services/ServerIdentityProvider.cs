using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Fluxy.Core.Servers.Interfaces;
using Fluxy.Core.Servers.Models;
using Fluxy.Core.Servers.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fluxy.Application.Servers.Services
{
    /// <summary>
    /// Answers what the machine this process runs on looks like, from configuration first and
    /// from the machine itself second.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing here throws.</b> The provider inspects whatever machine it happens to run on,
    /// and that machine may have no usable interface, an unresolvable name or a runtime that
    /// cannot describe its operating system. Every such case answers the documented fallback of
    /// <see cref="MainServer"/> instead, because a server that cannot describe itself is still a
    /// server and a startup that aborted over a probe would leave the installation with no row.
    /// </para>
    /// <para>
    /// <b>Configuration wins, and it is the deployment's answer rather than a guess.</b>
    /// <c>Servers:MainIp</c> and <c>Servers:MainPort</c> name the address a container or a
    /// reverse proxy is reached at, which the process cannot always discover from inside. When
    /// they say nothing, the address is read from the host and the port from the <c>Urls</c> the
    /// host was asked to listen on.
    /// </para>
    /// <para>
    /// The network interface is the requirement's literal <c>eth0</c>. Enumerating the machine's
    /// interfaces would answer the interface the operating system uses, not the one the
    /// deployment routes through, and the value is an editable starting point rather than a
    /// measured fact.
    /// </para>
    /// </remarks>
    public sealed class ServerIdentityProvider : IServerIdentityProvider
    {
        /// <summary>Configuration key naming the address the main server is reached at.</summary>
        public const string MainIpKey = "Servers:MainIp";

        /// <summary>Configuration key naming the port the main server listens on.</summary>
        public const string MainPortKey = "Servers:MainPort";

        private readonly IConfiguration _configuration;
        private readonly ILogger<ServerIdentityProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerIdentityProvider"/> class.
        /// </summary>
        /// <param name="configuration">Configuration the two overrides are read from.</param>
        /// <param name="logger">Logger the fallbacks worth remembering are reported to.</param>
        public ServerIdentityProvider(
            IConfiguration configuration,
            ILogger<ServerIdentityProvider> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <inheritdoc />
        public ServerIdentity Read()
        {
            var ip = ReadIp();
            var port = ReadPort();
            var operatingSystem = ReadOperatingSystem();

            return new ServerIdentity
            {
                Ip = ip,
                Port = port,
                OperatingSystem = operatingSystem,
                NetworkInterface = MainServer.DefaultNetworkInterface
            };
        }

        /// <summary>
        /// Configuration, then the host's own non-loopback IPv4 address, then the fallback.
        /// </summary>
        private string ReadIp()
        {
            try
            {
                var configured = _configuration[MainIpKey];

                if (!string.IsNullOrWhiteSpace(configured)
                    && IPAddress.TryParse(configured.Trim(), out var address))
                {
                    return address.ToString();
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Could not read {Key}, so the main server address is probed from the host.",
                    MainIpKey);
            }

            var detected = TryDetectHostAddress();

            if (detected is not null)
            {
                return detected;
            }

            return MainServer.FallbackIp;
        }

        /// <summary>
        /// Configuration, then the first port of the <c>Urls</c> the host listens on, then the
        /// fallback.
        /// </summary>
        private int ReadPort()
        {
            try
            {
                var configured = _configuration[MainPortKey];

                if (!string.IsNullOrWhiteSpace(configured)
                    && int.TryParse(configured.Trim(), out var port)
                    && port is >= ServerLimits.PortMin and <= ServerLimits.PortMax)
                {
                    return port;
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Could not read {Key}, so the main server port is read from Urls.",
                    MainPortKey);
            }

            return FirstPortFromUrls() ?? MainServer.FallbackPort;
        }

        /// <summary>
        /// The first port of the <c>Urls</c> the host listens on, or null when configuration
        /// cannot be read.
        /// </summary>
        /// <remarks>
        /// Reading the key is a configuration access like any other and can throw, so it sits
        /// under the same guard every other probe in this class carries - the contract is that
        /// nothing here throws, and a read outside a try would break it on a provider that does.
        /// </remarks>
        private int? FirstPortFromUrls()
        {
            try
            {
                return FirstPortOf(_configuration["Urls"]);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Could not read Urls, so the main server port falls back to {Port}.",
                    MainServer.FallbackPort);

                return null;
            }
        }

        /// <summary>
        /// The runtime's own description of the operating system, trimmed and cut to the column.
        /// </summary>
        private string ReadOperatingSystem()
        {
            try
            {
                var description = RuntimeInformation.OSDescription?.Trim();

                if (!string.IsNullOrEmpty(description))
                {
                    return description.Length <= ServerLimits.OsNameMaxLength
                        ? description
                        : description[..ServerLimits.OsNameMaxLength];
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "The runtime could not describe the operating system.");
            }

            return MainServer.FallbackOsName;
        }

        /// <summary>
        /// The first port named in an ASP.NET Core <c>Urls</c> value, or null when it names none.
        /// </summary>
        /// <remarks>
        /// <c>Urls</c> is a semicolon-separated list of addresses, and each address may be a
        /// wildcard host (<c>*</c>, <c>+</c>, <c>0.0.0.0</c>), a bracketed IPv6 literal or a name.
        /// Parsing it as a <see cref="Uri"/> would refuse the wildcard forms the host accepts,
        /// so the port is read as the trailing digits after the last colon of each entry. An
        /// entry with no digits after its last colon - <c>*</c>, <c>+</c>,
        /// <c>http://localhost</c> - names no port and is skipped.
        /// </remarks>
        /// <param name="urls">Value of the <c>Urls</c> configuration key, possibly null.</param>
        public static int? FirstPortOf(string? urls)
        {
            if (string.IsNullOrWhiteSpace(urls))
            {
                return null;
            }

            foreach (var entry in urls.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = entry.Trim();
                var colon = trimmed.LastIndexOf(':');

                if (colon < 0 || colon == trimmed.Length - 1)
                {
                    continue;
                }

                var candidate = trimmed[(colon + 1)..];

                // A bracketed IPv6 literal's last colon separates the port, but an unbracketed
                // one ends in hex digits and has no port at all; a trailing ']' means the entry
                // named an address and no port.
                if (candidate.Length == 0
                    || candidate.Any(character => !char.IsAsciiDigit(character)))
                {
                    continue;
                }

                if (int.TryParse(candidate, out var port)
                    && port is >= ServerLimits.PortMin and <= ServerLimits.PortMax)
                {
                    return port;
                }
            }

            return null;
        }

        /// <summary>
        /// The host's own address: a non-loopback IPv4, or null when the host has none.
        /// </summary>
        private string? TryDetectHostAddress()
        {
            try
            {
                var addresses = Dns.GetHostAddresses(Dns.GetHostName());

                var usable = addresses.FirstOrDefault(address =>
                    address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(address));

                if (usable is not null)
                {
                    return usable.ToString();
                }

                var fallback = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(networkInterface =>
                        networkInterface.OperationalStatus == OperationalStatus.Up
                        && networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(networkInterface =>
                        networkInterface.GetIPProperties().UnicastAddresses)
                    .Select(unicast => unicast.Address)
                    .FirstOrDefault(address =>
                        address.AddressFamily == AddressFamily.InterNetwork
                        && !IPAddress.IsLoopback(address));

                return fallback?.ToString();
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "The host could not report an address, so the documented fallback is used.");
            }

            return null;
        }
    }
}
