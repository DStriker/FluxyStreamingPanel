using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Models
{
    /// <summary>
    /// Immutable business representation of one configured audio stream: everything the
    /// operator decides about it, plus the one fact the relaying process reports back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An audio stream is named, typed and filed under a category. Unlike a video stream it has
    /// exactly one payload shape: both <see cref="AudioStreamType.OnDemand"/> and
    /// <see cref="AudioStreamType.Redirect"/> relay from an ordered list of
    /// <see cref="Sources"/>, so there is no payload field the type can switch off.
    /// </para>
    /// <para>
    /// <see cref="ActiveSource"/> is the one field nothing in the interface writes. It names the
    /// source the relaying process is using right now, and it can only be known by the process
    /// doing the relaying - an operator may look at it, but a form that offered it as a field
    /// would be a claim about a fact the operator has no way to establish.
    /// </para>
    /// <para>
    /// The list is plain ordered strings rather than rows of its own, because that is exactly
    /// what an operator edits: a screen of text fields whose order is the order they are tried
    /// in. The rows they are stored as are the storage's business, not the model's.
    /// </para>
    /// </remarks>
    public sealed record AudioStream
    {
        /// <summary>Identifier of the stream.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name, unique within <see cref="CategoryId"/>.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the stream is active and broadcasting, or disabled and kept.</summary>
        public required AudioStreamStatus Status { get; init; }

        /// <summary>The kind of stream, which decides how it is started.</summary>
        public required AudioStreamType Type { get; init; }

        /// <summary>Category the stream is filed under.</summary>
        public required Guid CategoryId { get; init; }

        /// <summary>Display name of that category, read with the row.</summary>
        public required string CategoryName { get; init; }

        /// <summary>Address the icon is fetched from, or null when the stream has none.</summary>
        public string? LogoUrl { get; init; }

        /// <summary>
        /// Source the relaying process is using right now, reported back rather than entered.
        /// Nothing in the interface writes it.
        /// </summary>
        public string? ActiveSource { get; init; }

        /// <summary>Enigma2 service reference as text, or null when the stream is not published there.</summary>
        public string? Enigma2Sid { get; init; }

        /// <summary>User agent the relayed requests carry, or null to send none.</summary>
        public string? RestreamUserAgent { get; init; }

        /// <summary>Proxy the relay goes out through, or null to go out directly.</summary>
        public string? RestreamProxy { get; init; }

        /// <summary>Cookies the relayed requests carry, or null to send none.</summary>
        public string? RestreamCookies { get; init; }

        /// <summary>Extra headers the relayed requests carry, as typed.</summary>
        public string? RestreamHeaders { get; init; }

        /// <summary>The addresses this stream relays from, in the order they are tried.</summary>
        public required IReadOnlyList<string> Sources { get; init; }
    }
}
