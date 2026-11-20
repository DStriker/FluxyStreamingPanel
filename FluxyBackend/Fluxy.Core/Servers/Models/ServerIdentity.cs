namespace Fluxy.Core.Servers.Models
{
    /// <summary>
    /// What a server says about itself when it is asked: the address it is reached at, the port
    /// it listens on, the operating system it runs and the interface it answers through.
    /// </summary>
    /// <remarks>
    /// A value object rather than a set of interface properties, because the four facts describe
    /// one answer - a provider that could supply three of them but not the fourth would describe
    /// a machine that does not exist. The startup seeder uses it to fill the main server row the
    /// first time an installation starts.
    /// </remarks>
    public sealed record ServerIdentity
    {
        /// <summary>Address the server is reached at.</summary>
        public required string Ip { get; init; }

        /// <summary>Port the server listens on.</summary>
        public required int Port { get; init; }

        /// <summary>Name of the operating system the machine runs.</summary>
        public required string OperatingSystem { get; init; }

        /// <summary>Name of the network interface the server is reached through.</summary>
        public required string NetworkInterface { get; init; }
    }
}
