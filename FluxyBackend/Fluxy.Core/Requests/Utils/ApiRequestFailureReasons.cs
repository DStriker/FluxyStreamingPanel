using Fluxy.Core.Requests.Enums;

namespace Fluxy.Core.Requests.Utils
{
    /// <summary>
    /// The wire vocabulary of <see cref="ApiRequestFailureReason"/>: the snake_case name a client
    /// sends and reads, and the member each spelling names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The table lives here so it can be tested where it is used.</b> The unit test project
    /// references Core and Application only, never the API layer, so a mapping kept in the
    /// transport would be reachable only through a running host. The transport parses the query
    /// string and refuses what names nothing; the answer itself is a pure rule with no HTTP in it.
    /// </para>
    /// <para>
    /// <b>Comparison is case-insensitive and whitespace-tolerant</b>, the same as every other list
    /// filter in this API: <c>reasons=AUTH_FAILED</c> and <c>reasons=auth_failed</c> are one
    /// request. A value that names no member answers false, and the transport turns that into a
    /// refusal under <c>reasons</c> - never into "no filter", because a filter that quietly widened
    /// to everything would answer a request the caller did not make.
    /// </para>
    /// <para>
    /// <b>The names run in both directions.</b> <see cref="WireName"/> is what a response carries
    /// and what <see cref="TryParseWire"/> accepts, so a client never has to learn two vocabularies
    /// for one fact.
    /// </para>
    /// </remarks>
    public static class ApiRequestFailureReasons
    {
        /// <summary>
        /// Every reason, in declaration order - which is also the value the database stores.
        /// </summary>
        public static IReadOnlyList<ApiRequestFailureReason> All { get; } =
        [
            ApiRequestFailureReason.UnknownId,
            ApiRequestFailureReason.GeoIpViolation,
            ApiRequestFailureReason.AuthFailed,
            ApiRequestFailureReason.TooManyConnections,
            ApiRequestFailureReason.SubscriptionExpired,
            ApiRequestFailureReason.UserBlocked
        ];

        /// <summary>
        /// The sentence a filter that names no reason is refused with. Built from
        /// <see cref="All"/> rather than written out, because a vocabulary that grows is a hint
        /// that would otherwise keep naming yesterday's members.
        /// </summary>
        public static string UnknownReasonHint { get; } =
            "The reason must be one of: " +
            string.Join(", ", All.Select(WireName)) +
            ".";

        /// <summary>The snake_case name a client sends and reads.</summary>
        /// <param name="reason">Member to name.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="reason"/> names no member of the vocabulary.
        /// </exception>
        public static string WireName(ApiRequestFailureReason reason) => reason switch
        {
            ApiRequestFailureReason.UnknownId => "unknown_id",
            ApiRequestFailureReason.GeoIpViolation => "geo_ip_violation",
            ApiRequestFailureReason.AuthFailed => "auth_failed",
            ApiRequestFailureReason.TooManyConnections => "too_many_connections",
            ApiRequestFailureReason.SubscriptionExpired => "subscription_expired",
            ApiRequestFailureReason.UserBlocked => "user_blocked",
            _ => throw new ArgumentOutOfRangeException(
                nameof(reason),
                reason,
                "No wire name is defined for that reason.")
        };

        /// <summary>Reads a wire name back into the member it names.</summary>
        /// <param name="value">Name as sent, possibly null.</param>
        /// <param name="reason">The member the name stands for, when there is one.</param>
        /// <returns>True when the name names a member of the vocabulary.</returns>
        public static bool TryParseWire(string? value, out ApiRequestFailureReason reason)
        {
            reason = default;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var text = value.Trim();

            foreach (var member in All)
            {
                if (string.Equals(WireName(member), text, StringComparison.OrdinalIgnoreCase))
                {
                    reason = member;

                    return true;
                }
            }

            return false;
        }
    }
}
