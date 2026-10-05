using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Fluxy.Core.Models.GeoIp;

namespace Fluxy.Application.Services.GeoIp
{
    /// <summary>
    /// The rules of the login guard allow lists: what is acceptable, what canonical form it is
    /// stored in, and how an address is compared against it.
    /// </summary>
    /// <remarks>
    /// Kept in one place for the same reason <c>RegistrationPolicy</c> is: the profile service
    /// writes the lists and the guard service reads them, and the two have to agree on what a
    /// value means without importing each other's storage details.
    /// </remarks>
    public static class LoginGuardPolicy
    {
        /// <summary>Longest allow list: how many networks one account may keep.</summary>
        public const int MaxAllowedIps = 5;

        /// <summary>
        /// Every ISO 3166-1 alpha-2 code this installation knows, upper case. Built from the
        /// runtime's own regions so the list cannot drift from what the platform recognizes.
        /// </summary>
        private static readonly HashSet<string> CountryCodes = BuildCountryCodes();

        /// <summary>
        /// Whether the address is a loopback or a private one. Such an address never reaches a
        /// GeoIP database - it is not on the public map - so the guard treats it separately.
        /// </summary>
        public static bool IsLocalAddress(string? clientAddress)
        {
            if (!IPAddress.TryParse(clientAddress?.Trim(), out var address))
            {
                return false;
            }

            return IPAddress.IsLoopback(address) || IsPrivate(address);
        }

        /// <summary>
        /// Whether two recorded addresses name the same peer. Parsed rather than compared as
        /// text, because one IPv6 address has many spellings and a session must not end over
        /// <c>::1</c> versus <c>0:0:0:0:0:0:0:1</c>.
        /// </summary>
        public static bool AddressesEqual(string? left, string? right)
        {
            if (IPAddress.TryParse(left?.Trim(), out var leftAddress)
                && IPAddress.TryParse(right?.Trim(), out var rightAddress))
            {
                return leftAddress.Equals(rightAddress);
            }

            return string.Equals(left?.Trim(), right?.Trim(), StringComparison.Ordinal);
        }

        /// <summary>
        /// Canonical form of one allow list entry: a normalized address, or a normalized
        /// network in CIDR form. Null when the entry is not an address at all.
        /// </summary>
        public static string? NormalizeIpEntry(string? entry)
        {
            var trimmed = entry?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return null;
            }

            if (IPAddress.TryParse(trimmed, out var address))
            {
                return address.ToString();
            }

            var slash = trimmed.IndexOf('/');
            if (slash < 1
                || !IPAddress.TryParse(trimmed[..slash].Trim(), out var network)
                || !int.TryParse(
                    trimmed[(slash + 1)..].Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var prefix))
            {
                return null;
            }

            var width = network.AddressFamily is AddressFamily.InterNetwork ? 32 : 128;
            if (prefix < 0 || prefix > width)
            {
                return null;
            }

            return $"{network}/{prefix}";
        }

        /// <summary>
        /// Whether an address falls inside one canonical entry - an exact address or a CIDR.
        /// An entry that does not parse, or an address of another family, never matches.
        /// </summary>
        public static bool MatchesEntry(string canonicalEntry, IPAddress address)
        {
            var slash = canonicalEntry.IndexOf('/');
            if (slash < 0)
            {
                return IPAddress.TryParse(canonicalEntry, out var exact) && exact.Equals(address);
            }

            if (!IPAddress.TryParse(canonicalEntry[..slash], out var network)
                || !int.TryParse(
                    canonicalEntry[(slash + 1)..],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var prefix))
            {
                return false;
            }

            var networkBytes = network.GetAddressBytes();
            var addressBytes = address.GetAddressBytes();
            if (networkBytes.Length != addressBytes.Length)
            {
                return false;
            }

            var fullBytes = prefix / 8;
            var restBits = prefix % 8;

            for (var i = 0; i < fullBytes; i++)
            {
                if (networkBytes[i] != addressBytes[i])
                {
                    return false;
                }
            }

            if (restBits > 0)
            {
                var mask = (byte)(0xFF << (8 - restBits));
                if ((networkBytes[fullBytes] & mask) != (addressBytes[fullBytes] & mask))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Canonical form of a country: upper case ISO 3166-1 alpha-2. Null when the value is
        /// not a code this installation knows.
        /// </summary>
        public static string? NormalizeCountry(string? country)
        {
            var trimmed = country?.Trim().ToUpperInvariant();
            if (trimmed is null || !CountryCodes.Contains(trimmed))
            {
                return null;
            }

            return trimmed;
        }

        /// <summary>Whether the value is a usable autonomous system number.</summary>
        public static bool IsAutonomousSystemNumber(int value) => value > 0;

        /// <summary>
        /// Whether a network passes the address list. An empty list does not restrict; an
        /// address that cannot be parsed, or one of another family than the entry, never
        /// matches.
        /// </summary>
        public static bool AllowsAddress(IEnumerable<string> canonicalEntries, GeoIpInfo where)
        {
            var entries = canonicalEntries.ToList();
            if (entries.Count == 0)
            {
                return true;
            }

            if (!IPAddress.TryParse(where.Ip?.Trim(), out var address))
            {
                return false;
            }

            return entries.Any(entry => MatchesEntry(entry, address));
        }

        /// <summary>
        /// Whether a network passes the country list. An empty list does not restrict; an
        /// unknown country never matches a non-empty one.
        /// </summary>
        public static bool AllowsCountry(IEnumerable<string> canonicalCountries, GeoIpInfo where)
        {
            var countries = canonicalCountries.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (countries.Count == 0)
            {
                return true;
            }

            return where.CountryCode is not null && countries.Contains(where.CountryCode);
        }

        /// <summary>
        /// Whether a network passes the provider list, matched by autonomous system number
        /// alone. An empty list does not restrict; an unknown provider never matches a
        /// non-empty one. The organization name is display only and never decides.
        /// </summary>
        public static bool AllowsProvider(IEnumerable<int> autonomousSystems, GeoIpInfo where)
        {
            var systems = autonomousSystems.ToHashSet();
            if (systems.Count == 0)
            {
                return true;
            }

            return where.AutonomousSystemNumber is { } asn && systems.Contains(asn);
        }

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

        private static HashSet<string> BuildCountryCodes()
        {
            var codes = new HashSet<string>(StringComparer.Ordinal);

            foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
            {
                try
                {
                    codes.Add(new RegionInfo(culture.Name).TwoLetterISORegionName);
                }
                catch (ArgumentException)
                {
                    // A culture without a region is not a country and contributes nothing.
                }
            }

            return codes;
        }
    }
}
