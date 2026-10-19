using System.Net;

namespace Fluxy.Core.GeoIp.Utils
{
    /// <summary>
    /// Facts about a client address that more than one layer has to agree on.
    /// </summary>
    /// <remarks>
    /// A local address - loopback, private or link local - is not on the public map, so the
    /// GeoIP databases have nothing to say about it, and the login guard treats such a network
    /// separately from one it could name. The reader of the databases and the guard that
    /// consults it are two different layers, so the answer itself belongs here rather than in
    /// either of them: the reader is infrastructure, the guard is a business rule, and neither
    /// may be the only place that knows what "local" means.
    /// </remarks>
    public static class GeoIpAddress
    {
        /// <summary>
        /// Whether the address is a loopback or a private one.
        /// </summary>
        /// <param name="clientAddress">
        /// Address as it was received, in any spelling. A value that is not an address at all
        /// is not a local one.
        /// </param>
        /// <returns>True for a loopback, private or link local address.</returns>
        public static bool IsLocal(string? clientAddress)
        {
            if (!IPAddress.TryParse(clientAddress?.Trim(), out var address))
            {
                return false;
            }

            return IPAddress.IsLoopback(address) || IsPrivate(address);
        }

        /// <summary>
        /// Whether the address is in a private range.
        /// </summary>
        /// <param name="address">Address to classify.</param>
        /// <returns>True for a private or link local address.</returns>
        private static bool IsPrivate(IPAddress address)
        {
            var bytes = address.GetAddressBytes();

            // IPv4 private ranges and link local, compared as bytes so no DNS is involved.
            if (bytes.Length == 4)
            {
                return bytes[0] == 10
                    || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                    || (bytes[0] == 192 && bytes[1] == 168)
                    || (bytes[0] == 169 && bytes[1] == 254);
            }

            // IPv6 unique local (fc00::/7) and link local (fe80::/10).
            return (bytes[0] & 0xFE) == 0xFC || (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80);
        }
    }
}
