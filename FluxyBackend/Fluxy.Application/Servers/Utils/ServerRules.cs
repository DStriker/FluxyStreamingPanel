using System.Net;
using Fluxy.Core.GeoIp.Utils;
using Fluxy.Core.Servers.Commands;
using Fluxy.Core.Servers.Utils;

namespace Fluxy.Application.Servers.Utils
{
    /// <summary>
    /// The rules and sentences every write to a server shares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A create and a full write reach the same row through different requests, so the rules
    /// that are a fact about the row rather than about the request live here once. The service
    /// asks <see cref="Validate"/> first and calls <see cref="Normalize"/> only once nothing was
    /// refused, so a value that is judged is judged as it arrived - in particular a country that
    /// names nothing is still in the list <see cref="Validate"/> reads, rather than having been
    /// dropped by a normalizer that ran too early.
    /// </para>
    /// <para>
    /// The address rule is written here rather than reused from the API layer's blacklist
    /// helper: a rule of the domain must not live above the layer that judges it. It is the same
    /// rule - <see cref="IPAddress.TryParse(string, out IPAddress?)"/> accepts an IPv4 or IPv6
    /// literal and refuses everything else - plus the stored column's length.
    /// </para>
    /// <para>
    /// The keys of the refused map are the C# property names of <see cref="ServerWrite"/>, which
    /// are also the names both requests spell; the transport rewrites them to camelCase for the
    /// client, the way it does for every other service of this API.
    /// </para>
    /// </remarks>
    public static class ServerRules
    {
        /// <summary>The sentence a name that is blank or too long gets.</summary>
        public static readonly string[] NameRule =
        [
            "The name must be between " + ServerLimits.NameMinLength + " and " +
                ServerLimits.NameMaxLength + " characters."
        ];

        /// <summary>The sentence an address that is not a literal, or is too long, gets.</summary>
        public static readonly string[] IpRule =
        [
            "The IP must be a valid IPv4 or IPv6 address of at most " +
                ServerLimits.IpMaxLength + " characters."
        ];

        /// <summary>The sentence a VPN address that is not a literal gets.</summary>
        public static readonly string[] VpnIpRule =
        [
            "The VPN IP must be a valid IPv4 or IPv6 address of at most " +
                ServerLimits.IpMaxLength + " characters."
        ];

        /// <summary>The sentence a port outside the TCP range gets.</summary>
        public static readonly string[] PortRule =
        [
            "The port must be between " + ServerLimits.PortMin + " and " +
                ServerLimits.PortMax + "."
        ];

        /// <summary>The sentence a client ceiling outside the allowed range gets.</summary>
        public static readonly string[] ClientCountRule =
        [
            "The client count must be between " + ServerLimits.ClientCountMin + " and " +
                ServerLimits.ClientCountMax + "; 0 means unlimited."
        ];

        /// <summary>The sentence a negative latency gets.</summary>
        public static readonly string[] LatencyRule =
        [
            "The latency must be zero (not measured) or greater."
        ];

        /// <summary>The sentence a guaranteed speed outside the allowed range gets.</summary>
        public static readonly string[] GuaranteedSpeedRule =
        [
            "The guaranteed speed must be between " + ServerLimits.GuaranteedSpeedMinKbps +
                " and " + ServerLimits.GuaranteedSpeedMaxKbps + " kbps; 0 means unknown."
        ];

        /// <summary>The sentence an operating system name that is blank or too long gets.</summary>
        public static readonly string[] OsNameRule =
        [
            "The operating system must be between 1 and " +
                ServerLimits.OsNameMaxLength + " characters."
        ];

        /// <summary>The sentence a network interface that is blank or too long gets.</summary>
        public static readonly string[] NetworkInterfaceRule =
        [
            "The network interface must be between 1 and " +
                ServerLimits.NetworkInterfaceMaxLength + " characters."
        ];

        /// <summary>The sentence a host name that is too long gets.</summary>
        public static readonly string[] HostnameRule =
        [
            "The host name must be at most " + ServerLimits.HostnameMaxLength + " characters."
        ];

        /// <summary>The sentence a country list with something that is not a code gets.</summary>
        public static readonly string[] CountryCodesRule =
        [
            "Every country code must be an ISO 3166-1 alpha-2 code, and a server may name at " +
                "most " + ServerLimits.CountryCodeCountMax + " countries."
        ];

        /// <summary>
        /// Checks everything about a resolved write that can be judged without asking the
        /// database.
        /// </summary>
        /// <param name="server">
        /// Values the row is about to carry, as they arrived: text is trimmed while it is judged
        /// rather than before, so an oversized value is measured without its surrounding spaces.
        /// </param>
        /// <returns>The rejected fields, empty when the row may be stored.</returns>
        public static Dictionary<string, string[]> Validate(ServerWrite server)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var name = server.Name.Trim();

            if (name.Length is < ServerLimits.NameMinLength or > ServerLimits.NameMaxLength)
            {
                errors[nameof(ServerWrite.Name)] = NameRule;
            }

            if (!IsAddress(server.Ip))
            {
                errors[nameof(ServerWrite.Ip)] = IpRule;
            }

            if (NormalizeText(server.VpnIp) is { } vpnIp && !IsAddress(vpnIp))
            {
                errors[nameof(ServerWrite.VpnIp)] = VpnIpRule;
            }

            if (server.Port is < ServerLimits.PortMin or > ServerLimits.PortMax)
            {
                errors[nameof(ServerWrite.Port)] = PortRule;
            }

            if (server.ClientCount is < ServerLimits.ClientCountMin
                or > ServerLimits.ClientCountMax)
            {
                errors[nameof(ServerWrite.ClientCount)] = ClientCountRule;
            }

            if (server.Latency < ServerLimits.LatencyMin)
            {
                errors[nameof(ServerWrite.Latency)] = LatencyRule;
            }

            if (server.GuaranteedSpeedKbps
                is < ServerLimits.GuaranteedSpeedMinKbps or > ServerLimits.GuaranteedSpeedMaxKbps)
            {
                errors[nameof(ServerWrite.GuaranteedSpeedKbps)] = GuaranteedSpeedRule;
            }

            var osName = server.OsName.Trim();

            if (osName.Length is < 1 or > ServerLimits.OsNameMaxLength)
            {
                errors[nameof(ServerWrite.OsName)] = OsNameRule;
            }

            var networkInterface = server.NetworkInterface.Trim();

            if (networkInterface.Length is < 1
                or > ServerLimits.NetworkInterfaceMaxLength)
            {
                errors[nameof(ServerWrite.NetworkInterface)] = NetworkInterfaceRule;
            }

            if (NormalizeText(server.Hostname) is { Length: > ServerLimits.HostnameMaxLength })
            {
                errors[nameof(ServerWrite.Hostname)] = HostnameRule;
            }

            // The list is normalized here as well as in Normalize, because this is the phase
            // that has to notice an element that names no country - Normalize would otherwise
            // have dropped it before anybody could refuse it.
            NormalizeCountries(server.CountryCodes, errors);

            return errors;
        }

        /// <summary>
        /// Trims every text field, turns a blank optional one into null, and rewrites the
        /// country list into its stored form.
        /// </summary>
        /// <remarks>
        /// Called only after <see cref="Validate"/> passed. The country list keeps the order the
        /// operator arranged, loses every repetition after its first occurrence, and is stored
        /// upper case - a list that reached this method with an element naming no country would
        /// lose it, which is why the refusal belongs to the phase before.
        /// </remarks>
        /// <param name="server">Values that already passed validation.</param>
        /// <returns>The values as they are about to be stored.</returns>
        public static ServerWrite Normalize(ServerWrite server) => new()
        {
            Id = server.Id,
            Name = server.Name.Trim(),
            Ip = server.Ip.Trim(),
            Port = server.Port,
            ClientCount = server.ClientCount,
            OsName = server.OsName.Trim(),
            NetworkInterface = server.NetworkInterface.Trim(),
            Latency = server.Latency,
            Status = server.Status,
            GuaranteedSpeedKbps = server.GuaranteedSpeedKbps,
            TimeshiftOnly = server.TimeshiftOnly,
            Hostname = NormalizeText(server.Hostname),
            VpnIp = NormalizeText(server.VpnIp),
            CountryCodes = NormalizeCountries(server.CountryCodes, errors: null)
        };

        /// <summary>
        /// Whether a value is an address this row may store: a single IPv4 or IPv6 literal that
        /// fits the column.
        /// </summary>
        /// <param name="value">Value to judge; surrounding spaces are ignored.</param>
        public static bool IsAddress(string value)
        {
            var trimmed = value.Trim();

            return trimmed.Length is > 0 and <= ServerLimits.IpMaxLength
                && IPAddress.TryParse(trimmed, out _);
        }

        /// <summary>
        /// Canonical form of a country list: upper case, de-duplicated in first-occurrence
        /// order, and with every element that names no country left out.
        /// </summary>
        /// <param name="codes">Codes as they arrived, possibly null.</param>
        /// <returns>The stored list; empty when the input names nothing.</returns>
        public static string[] NormalizeCountries(IReadOnlyList<string>? codes)
            => NormalizeCountries(codes, errors: null);

        /// <summary>
        /// Whether a normalized country list is short enough to store.
        /// </summary>
        /// <param name="count">How many distinct codes the list holds.</param>
        /// <remarks>
        /// A guard rather than the rule that refuses an ordinary request: the runtime knows fewer
        /// codes than the cap, so no list of valid codes can reach it today. It is stated and
        /// tested anyway, because the cap is part of the stored column's contract and the day the
        /// list of codes grows it must still hold.
        /// </remarks>
        public static bool IsWithinCountryCap(int count)
            => count <= ServerLimits.CountryCodeCountMax;

        /// <summary>Trims a text and turns a blank one into null.</summary>
        private static string? NormalizeText(string? value)
        {
            if (value is null)
            {
                return null;
            }

            var trimmed = value.Trim();

            return trimmed.Length == 0 ? null : trimmed;
        }

        /// <summary>
        /// Rewrites the country list into its stored form and, when an error map is supplied,
        /// refuses every element that names no country or a list longer than the cap.
        /// </summary>
        private static string[] NormalizeCountries(
            IReadOnlyList<string>? codes,
            Dictionary<string, string[]>? errors)
        {
            if (codes is null || codes.Count == 0)
            {
                return [];
            }

            var normalized = new List<string>(codes.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var refused = false;

            foreach (var code in codes)
            {
                var country = CountryCodes.Normalize(code);

                if (country is null)
                {
                    refused = true;
                    continue;
                }

                if (seen.Add(country))
                {
                    normalized.Add(country);
                }
            }

            if (errors is not null
                && (refused || !IsWithinCountryCap(normalized.Count)))
            {
                errors[nameof(ServerWrite.CountryCodes)] = CountryCodesRule;
            }

            return [.. normalized];
        }
    }
}
