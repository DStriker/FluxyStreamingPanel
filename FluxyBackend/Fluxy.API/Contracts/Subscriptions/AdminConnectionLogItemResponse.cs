namespace Fluxy.API.Contracts.Subscriptions
{
    /// <summary>
    /// One row of the admin connection log: the session, the subscription it was made under, and
    /// enough of the stream to say what was being watched.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No duration.</b> <see cref="StartedAt"/> and <see cref="EndedAt"/> are the facts the row
    /// holds; the client subtracts them. A duration computed on the server would be a third value
    /// to keep in step, and a live session's duration is a moving number until it ends.
    /// </para>
    /// <para>
    /// <see cref="StreamTitle"/> is null when the stream row the session named no longer exists -
    /// the session happened, the row it pointed at does not. <see cref="Username"/> comes from the
    /// account that owns the subscription and is drawn as it reads today.
    /// </para>
    /// </remarks>
    public sealed record AdminConnectionLogItemResponse
    {
        /// <summary>Identifier of the connection, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Identifier of the subscription it was made under, as text.</summary>
        public required string SubscriptionId { get; init; }

        /// <summary>Identifier of the account that owns that subscription, as text.</summary>
        public required string OwnerId { get; init; }

        /// <summary>Login name of the subscription's owner.</summary>
        public required string Username { get; init; }

        /// <summary>The medium, as <c>video</c> or <c>audio</c> - the spelling the filter accepts.</summary>
        public required string StreamKind { get; init; }

        /// <summary>Opaque identifier of the stream, as text.</summary>
        public required string StreamId { get; init; }

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
