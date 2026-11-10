namespace Fluxy.API.Contracts.Requests
{
    /// <summary>
    /// One refused public-API request as a row of the admin table: the subscription data it named,
    /// the content snapshot, the reason, the network it came from and when.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The content snapshot is answered exactly as stored.</b> <see cref="ContentKind"/> is the
    /// <c>ContentType</c> member name (<c>"VideoStream"</c> ...), the spelling the content-categories
    /// contract already uses, and <see cref="ContentName"/> is the name captured at write time. The
    /// three content fields are null together: a refusal that named no content carries none of them,
    /// and no read path resolves a name from a content table.
    /// </para>
    /// <para>
    /// <see cref="Reason"/> is the snake_case wire name (<c>auth_failed</c>, <c>user_blocked</c>, ...),
    /// the same vocabulary the <c>reasons</c> filter accepts, so a client never learns two spellings
    /// of one fact.
    /// </para>
    /// <para>
    /// <see cref="Id"/> and <see cref="ContentId"/> are text because a JavaScript client parses a
    /// uuid as a number otherwise, and the two are unrelated: the first is this record's own key,
    /// the second an opaque identifier the refused request named.
    /// </para>
    /// </remarks>
    public sealed record AdminApiRequestFailureItemResponse
    {
        /// <summary>Identifier of the failure record, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Free-form access parameter the refused request named, or null.</summary>
        public string? SubscriptionData { get; init; }

        /// <summary>
        /// Kind of the content the request named, as its <c>ContentType</c> member name, or null.
        /// </summary>
        public string? ContentKind { get; init; }

        /// <summary>Opaque identifier of that content, as text, or null.</summary>
        public string? ContentId { get; init; }

        /// <summary>Name of that content as it read at write time, or null.</summary>
        public string? ContentName { get; init; }

        /// <summary>Why the request was refused, as its snake_case wire name.</summary>
        public required string Reason { get; init; }

        /// <summary>Address the request came from.</summary>
        public required string Ip { get; init; }

        /// <summary>Provider the address belonged to, or null.</summary>
        public string? Isp { get; init; }

        /// <summary>Upper-case ISO 3166-1 alpha-2 country of the address, or null.</summary>
        public string? CountryCode { get; init; }

        /// <summary>English sentence describing the refusal.</summary>
        public required string Description { get; init; }

        /// <summary>When the record was written.</summary>
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
