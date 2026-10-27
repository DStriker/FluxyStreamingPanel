using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Commands
{
    /// <summary>
    /// What an administrator changes about an existing audio stream. Every property is
    /// optional, and an absent one means "leave it alone".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Status is here so the edit form can enable or disable a stream.</b> It is the same
    /// row fact the start and stop actions write; an absent value keeps whatever the row holds,
    /// exactly as every other property of this patch does.
    /// </para>
    /// <para>
    /// An absent list keeps what the row already holds; a list with entries replaces the stored
    /// one wholesale, in the order the request names them. Replacing rather than patching is
    /// what makes the order the operator arranged on screen the order the relay tries, with no
    /// position arithmetic to get wrong. An empty list is refused rather than stored: both
    /// kinds relay from a source list, so a stream with none would be a row that can never
    /// play.
    /// </para>
    /// </remarks>
    public sealed record AudioStreamPatch
    {
        /// <summary>New name, or null to keep the one on the row.</summary>
        public string? Name { get; init; }

        /// <summary>New kind of stream, or null to keep.</summary>
        public AudioStreamType? Type { get; init; }

        /// <summary>New status, or null to keep the stored one.</summary>
        public AudioStreamStatus? Status { get; init; }

        /// <summary>New category, or null to keep.</summary>
        public Guid? CategoryId { get; init; }

        /// <summary>New icon address; null keeps, empty clears.</summary>
        public string? LogoUrl { get; init; }

        /// <summary>New service reference; null keeps, empty clears.</summary>
        public string? Enigma2Sid { get; init; }

        /// <summary>New user agent; null keeps, empty clears.</summary>
        public string? RestreamUserAgent { get; init; }

        /// <summary>New proxy; null keeps, empty clears.</summary>
        public string? RestreamProxy { get; init; }

        /// <summary>New cookies; null keeps, empty clears.</summary>
        public string? RestreamCookies { get; init; }

        /// <summary>New headers; null keeps, empty clears.</summary>
        public string? RestreamHeaders { get; init; }

        /// <summary>New source list; null keeps, a list replaces. An empty list is refused.</summary>
        public IReadOnlyList<string>? Sources { get; init; }

        /// <summary>
        /// New set of bouquets the stream belongs to; null keeps, empty leaves every bouquet.
        /// </summary>
        /// <remarks>
        /// A <b>set</b> rather than a list: the field answers "which packages is this stream
        /// in" and says nothing about where it sits inside one. A bouquet the stream joins
        /// takes it at the end of that bouquet's list.
        /// </remarks>
        public IReadOnlyList<Guid>? BouquetIds { get; init; }
    }
}
