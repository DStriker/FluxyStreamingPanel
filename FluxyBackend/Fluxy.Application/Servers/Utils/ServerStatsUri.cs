using System.Net;

namespace Fluxy.Application.Servers.Utils
{
    /// <summary>
    /// Builds the address a server's own statistics are read from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The payload is an assumption, and this type is part of its isolation.</b> The contract
    /// recorded as A1 in <c>docs/admin-dashboard.md</c> is <c>GET http://{ip}:{port}/api/stats</c>
    /// over plain HTTP. A real agent protocol later replaces the adapter that calls this builder
    /// and the private payload beside it; nothing above that adapter knows the shape.
    /// </para>
    /// <para>
    /// <b>IPv6 literals are bracketed.</b> A bare <c>::1</c> in a URI's authority is ambiguous,
    /// so a parsed IPv6 address has to travel as <c>[::1]</c>. The address is parsed first and
    /// then formatted by <see cref="IPAddress"/>, which means the caller's spelling - compressed
    /// or not - does not decide whether the request is well formed.
    /// </para>
    /// <para>
    /// A value that is not an address, or a port outside <c>1..65535</c>, answers false rather
    /// than throwing: a row an operator typed badly is a server the dashboard reports as
    /// unavailable, not a request that fails.
    /// </para>
    /// </remarks>
    public static class ServerStatsUri
    {
        /// <summary>The path of the statistics endpoint on a server.</summary>
        public const string StatsPath = "/api/stats";

        /// <summary>Builds the statistics address of one server.</summary>
        /// <param name="ip">Address of the server, as stored.</param>
        /// <param name="port">Port the server listens on.</param>
        /// <param name="uri">The address when this method returns true.</param>
        /// <returns>True when both values name a reachable address shape.</returns>
        public static bool TryBuild(string ip, int port, out Uri uri)
        {
            uri = null!;

            if (port is < 1 or > IPEndPoint.MaxPort)
            {
                return false;
            }

            if (!IPAddress.TryParse(ip, out var address))
            {
                return false;
            }

            var host = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                ? $"[{address}]"
                : address.ToString();

            return Uri.TryCreate(
                $"http://{host}:{port}{StatsPath}",
                UriKind.Absolute,
                out uri!);
        }
    }
}
