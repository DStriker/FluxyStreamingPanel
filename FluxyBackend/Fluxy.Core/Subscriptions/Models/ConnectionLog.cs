using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.Models
{
    /// <summary>
    /// Immutable business representation of one streaming connection: who watched which stream,
    /// from where, on what device, and for how long.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The network facts are stored, not resolved on read.</b> The visit history deliberately
    /// resolves country and provider at read time, because a table of visits would then hold a
    /// fact the GeoIP database could later contradict. This row diverges on purpose: the operator
    /// filters this table by country and by provider, and a value that only exists while a page
    /// is being built cannot be filtered or counted on the server. The snapshot is therefore taken
    /// when the session opens and kept as it was - the mobile network a viewer used last month is
    /// a fact about that session, not a claim about today.
    /// </para>
    /// <para>
    /// <b>The subscription is a real foreign key.</b> A log without its subscription is a row
    /// nobody can be told about, so the constraint is <c>ON DELETE CASCADE</c>: deleting a
    /// subscription removes the connections it carried. That is the intended consequence and not
    /// an accident of the schema.
    /// </para>
    /// <para>
    /// <b>There is no duration column.</b> The client computes it from the two timestamps, which
    /// are the only facts the row actually holds - a stored duration would be a third value that
    /// could disagree with them.
    /// </para>
    /// </remarks>
    public sealed record ConnectionLog
    {
        /// <summary>Identifier of the connection.</summary>
        public required Guid Id { get; init; }

        /// <summary>The subscription the connection was made under.</summary>
        public required Guid SubscriptionId { get; init; }

        /// <summary>Which medium it delivered.</summary>
        public required ConnectionStreamKind StreamKind { get; init; }

        /// <summary>
        /// Opaque identifier of the stream it delivered. Deliberately not a foreign key: the
        /// delivery process may report a stream row this installation has no record of, the same
        /// way <c>VideoStream.SeriesId</c> may name a series that was never recorded.
        /// </summary>
        public required Guid StreamId { get; init; }

        /// <summary>Address the session came from, up to 45 characters (the longest IPv6 form).</summary>
        public required string Ip { get; init; }

        /// <summary>User agent the session presented, or null when it presented none.</summary>
        public string? UserAgent { get; init; }

        /// <summary>Upper-case ISO 3166-1 alpha-2 country of the address, or null when unknown.</summary>
        public string? CountryCode { get; init; }

        /// <summary>Internet provider the address belongs to, or null when unknown.</summary>
        public string? Isp { get; init; }

        /// <summary>When the session opened.</summary>
        public required DateTimeOffset StartedAt { get; init; }

        /// <summary>When it finished, or null while it is still live.</summary>
        public DateTimeOffset? EndedAt { get; init; }

        /// <summary>Process identifier the broadcasting side reported, or null when it reported none.</summary>
        public int? BroadcastPid { get; init; }

        /// <summary>Creation timestamp of the row.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>Last modification timestamp of the row.</summary>
        public required DateTimeOffset UpdatedAt { get; init; }
    }
}
