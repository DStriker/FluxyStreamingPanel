using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Models;

namespace Fluxy.Core.Streams.Commands
{
    /// <summary>
    /// The values a create or an update writes to an audio stream row.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="AudioStream"/> because a write does not name the facts the
    /// storage owns - the identifier and the source the relay reports - and it does not carry
    /// the category's display name, which belongs to the category rather than to this row. The
    /// status <i>is</i> part of a write: the create form and the edit form both choose whether
    /// the stream is active.
    /// </remarks>
    public sealed record AudioStreamWrite
    {
        /// <summary>Display name, unique within <see cref="CategoryId"/>.</summary>
        public required string Name { get; init; }

        /// <summary>The kind of stream.</summary>
        public required AudioStreamType Type { get; init; }

        /// <summary>Whether the stream is active, or disabled and kept without broadcasting.</summary>
        public required AudioStreamStatus Status { get; init; }

        /// <summary>Category the stream is filed under.</summary>
        public required Guid CategoryId { get; init; }

        /// <summary>Address the icon is fetched from, or null.</summary>
        public string? LogoUrl { get; init; }

        /// <summary>Enigma2 service reference as text, or null.</summary>
        public string? Enigma2Sid { get; init; }

        /// <summary>User agent the relayed requests carry, or null.</summary>
        public string? RestreamUserAgent { get; init; }

        /// <summary>Proxy the relay goes out through, or null.</summary>
        public string? RestreamProxy { get; init; }

        /// <summary>Cookies the relayed requests carry, or null.</summary>
        public string? RestreamCookies { get; init; }

        /// <summary>Extra headers the relayed requests carry, as typed.</summary>
        public string? RestreamHeaders { get; init; }

        /// <summary>Trimmed, non-blank source addresses in relay order.</summary>
        public required IReadOnlyList<string> Sources { get; init; }
    }
}
