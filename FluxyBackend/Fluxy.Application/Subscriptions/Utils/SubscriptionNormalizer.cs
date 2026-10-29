using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Fluxy.Application.Subscriptions.Utils
{
    /// <summary>
    /// The canonical forms and the shapes a subscription's access fields are judged by: a MAC, an
    /// IP whitelist entry and a user agent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kept apart from <see cref="SubscriptionRules"/> because two things ask these questions in
    /// different situations - a create and a change both normalize, and the tests exercise the
    /// normalization on its own - while the rules file answers "may this row be written at all".
    /// </para>
    /// <para>
    /// A MAC is stored in one form and accepted in several: either separator, either case. The
    /// canonical form is upper case with colons, because that is what a set-top box prints on its
    /// label and what an operator reads back to a support agent; a database that stored whatever
    /// was typed would make the same device two rows on two days.
    /// </para>
    /// </remarks>
    public static partial class SubscriptionNormalizer
    {
        /// <summary>
        /// Canonical <c>XX:XX:XX:XX:XX:XX</c> form of a MAC typed with colons or hyphens in any
        /// case, or null when the value is not six pairs of hex digits.
        /// </summary>
        /// <param name="mac">Value as the operator typed it, possibly null or blank.</param>
        public static string? NormalizeMac(string? mac)
        {
            var trimmed = mac?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return null;
            }

            var compact = trimmed.Replace(":", string.Empty).Replace("-", string.Empty);

            if (compact.Length != 12
                || !compact.All(character =>
                    character is >= '0' and <= '9'
                    or >= 'a' and <= 'f'
                    or >= 'A' and <= 'F'))
            {
                return null;
            }

            var upper = compact.ToUpperInvariant();
            var pairs = new string[6];

            for (var index = 0; index < 6; index++)
            {
                pairs[index] = upper.Substring(index * 2, 2);
            }

            return string.Join(':', pairs);
        }

        /// <summary>
        /// Canonical form of one IP whitelist entry - a normalized address or a normalized CIDR
        /// network - or null when the entry is neither.
        /// </summary>
        /// <remarks>
        /// The same acceptance the login guard's address list uses: an exact address or a CIDR
        /// range, IPv4 or IPv6, in the runtime's own normalized spelling. There is no second
        /// parser for "an address" in this installation, and this is the one both lists go
        /// through.
        /// </remarks>
        /// <param name="entry">Value as the operator typed it.</param>
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
        /// A user-agent entry as it should be stored: trimmed, with the blanks removed.
        /// </summary>
        /// <remarks>
        /// Free text and nothing else - a user agent is whatever the client sends, so refusing one
        /// for an unusual shape would refuse exactly the clients a whitelist exists to admit. What
        /// is not free is its length and its emptiness, which the rules file judges.
        /// </remarks>
        /// <param name="entry">Value as the operator typed it.</param>
        public static string? NormalizeUserAgent(string? entry)
        {
            var trimmed = entry?.Trim();

            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }

        /// <summary>
        /// Whether a playlist id is the 32-character lower-case hex string this system mints.
        /// </summary>
        /// <param name="playlistId">Value to test.</param>
        public static bool IsPlaylistId(string? playlistId)
            => playlistId is not null && PlaylistIdPattern().IsMatch(playlistId);

        [GeneratedRegex("^[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
        private static partial Regex PlaylistIdPattern();
    }
}
