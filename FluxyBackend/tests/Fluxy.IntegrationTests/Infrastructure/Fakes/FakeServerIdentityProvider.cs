using Fluxy.Core.Servers.Interfaces;
using Fluxy.Core.Servers.Models;

namespace Fluxy.IntegrationTests.Infrastructure.Fakes
{
    /// <summary>
    /// Answers a deterministic identity for every test, so the seeded main row is the same on
    /// every machine that runs the suite.
    /// </summary>
    /// <remarks>
    /// The real provider probes the host, and the suite would then assert values that differ
    /// between a workstation and a CI container. It is replaced in <see cref="FluxyApiFactory"/>
    /// beside the other fakes, which is why it is registered as an interface rather than resolved
    /// as a concrete type.
    /// </remarks>
    public sealed class FakeServerIdentityProvider : IServerIdentityProvider
    {
        /// <summary>The address every seeded row carries.</summary>
        public const string Ip = "127.0.0.1";

        /// <summary>The port every seeded row carries.</summary>
        public const int Port = 5159;

        /// <summary>The operating system name every seeded row carries.</summary>
        public const string OperatingSystem = "Test OS";

        /// <summary>The network interface every seeded row carries.</summary>
        public const string NetworkInterface = "eth0";

        /// <inheritdoc />
        public ServerIdentity Read() => new()
        {
            Ip = Ip,
            Port = Port,
            OperatingSystem = OperatingSystem,
            NetworkInterface = NetworkInterface
        };
    }
}
