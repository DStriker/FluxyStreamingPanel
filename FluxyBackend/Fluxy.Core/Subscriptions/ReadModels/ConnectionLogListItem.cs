using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.ReadModels
{
    /// <summary>
    /// One connection as a row of the admin log: the session, the account that owns the
    /// subscription, and enough of the stream to say what was being watched.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The owner and the title are resolved, not stored.</b> <see cref="Username"/> comes from
    /// the account the subscription belongs to and <see cref="StreamTitle"/> from the stream table
    /// the kind names, because a name that changed after the session ended should read as the name
    /// the operator sees today rather than as a snapshot of a deleted row. The identifiers travel
    /// beside them so a client can link to the subscription and to the stream.
    /// </para>
    /// <para>
    /// <see cref="StreamTitle"/> is null when the stream row is gone. That is a fact worth drawing
    /// - the connection happened, the row it named does not exist any more - rather than a reason
    /// to hide the row or to fail the page.
    /// </para>
    /// <para>
    /// There is no duration here: the two timestamps are the facts, and the reader subtracts
    /// them. A duration computed on the server would be a third value to keep in step, and a
    /// live session's duration is a moving number until it ends.
    /// </para>
    /// </remarks>
    public sealed record ConnectionLogListItem
    {
        /// <summary>Identifier of the connection.</summary>
        public required Guid Id { get; init; }

        /// <summary>Identifier of the subscription the connection was made under.</summary>
        public required Guid SubscriptionId { get; init; }

        /// <summary>Identifier of the account that owns that subscription.</summary>
        /// <remarks>
        /// Carried for one reader: the row draws the owner's name as a link to the account, and a
        /// name is not an address. It is the same owner the <c>ownerId</c> filter names, so the
        /// page can also label the filter it applied with the name it is showing.
        /// </remarks>
        public required Guid OwnerId { get; init; }

        /// <summary>Login name of the subscription's owner.</summary>
        public required string Username { get; init; }

        /// <summary>Which medium the connection delivered.</summary>
        public required ConnectionStreamKind StreamKind { get; init; }

        /// <summary>Opaque identifier of the stream it delivered.</summary>
        public required Guid StreamId { get; init; }

        /// <summary>Name of that stream, or null when the row it named is gone.</summary>
        public string? StreamTitle { get; init; }

        /// <summary>Address the session came from.</summary>
        public required string Ip { get; init; }

        /// <summary>User agent the session presented, or null.</summary>
        public string? UserAgent { get; init; }

        /// <summary>Upper-case ISO alpha-2 country of the address, or null when unknown.</summary>
        public string? CountryCode { get; init; }

        /// <summary>Internet provider the address belongs to, or null when unknown.</summary>
        public string? Isp { get; init; }

        /// <summary>When the session opened.</summary>
        public required DateTimeOffset StartedAt { get; init; }

        /// <summary>When it finished, or null while it is still live.</summary>
        public DateTimeOffset? EndedAt { get; init; }

        /// <summary>Process identifier the broadcasting side reported, or null.</summary>
        public int? BroadcastPid { get; init; }
    }
}
