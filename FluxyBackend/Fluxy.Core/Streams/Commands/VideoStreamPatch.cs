using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Commands
{
    /// <summary>
    /// What an administrator changes about an existing stream. Every property is optional, and
    /// an absent one means "leave it alone".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Status is here so the edit form can enable or disable a stream.</b> It is the same
    /// row fact the start and stop actions write; an absent value keeps whatever the row
    /// holds, exactly as every other property of this patch does.
    /// </para>
    /// <para>
    /// <b>The three identifiers travel as text rather than as <see cref="Guid"/>s.</b> On a
    /// create there is nothing to keep, so a null identifier simply means "none"; on a change
    /// a null identifier would be ambiguous between "leave it" and "clear it", and the two are
    /// different requests. The text form answers both without a second field to tell them
    /// apart, and it is the convention the profile change already follows: absent or
    /// <c>null</c> keeps the stored value, an empty string clears it, and anything else is the
    /// new value.
    /// </para>
    /// <para>
    /// The two lists follow the same shape with an extra state. An absent list keeps what the
    /// row already holds; an empty list clears it; a list with entries replaces the stored one
    /// wholesale, in the order the request names them. Replacing rather than patching is what
    /// makes the order the operator arranged on screen the order the relay plays in, with no
    /// position arithmetic to get wrong.
    /// </para>
    /// </remarks>
    public sealed record VideoStreamPatch
    {
        /// <summary>New name, or null to keep the one on the row.</summary>
        public string? Name { get; init; }

        /// <summary>New kind of stream, or null to keep the one on the row.</summary>
        public VideoStreamType? Type { get; init; }

        /// <summary>New status, or null to keep the one on the row.</summary>
        public VideoStreamStatus? Status { get; init; }

        /// <summary>New category, or null to keep the one on the row.</summary>
        public Guid? CategoryId { get; init; }

        /// <summary>New icon address; null keeps, empty clears.</summary>
        public string? LogoUrl { get; init; }

        /// <summary>New service reference; null keeps, empty clears.</summary>
        public string? Enigma2Sid { get; init; }

        /// <summary>New delay in minutes, or null to keep the one on the row.</summary>
        public int? DelayMinutes { get; init; }

        /// <summary>New series reference; null keeps, empty clears.</summary>
        public string? SeriesId { get; init; }

        /// <summary>
        /// New on-demand list, in the order the stream plays it; null keeps, empty clears.
        /// </summary>
        /// <remarks>
        /// The same three states the source and file lists have, and for the same reason: an
        /// absent list means "this patch says nothing about the playlist", an empty one means
        /// "it holds nothing", and anything else replaces the stored order wholesale - which
        /// is what makes the order on the operator's screen the order the relay follows.
        /// </remarks>
        public IReadOnlyList<Guid>? VodItems { get; init; }

        /// <summary>New programme guide reference; null keeps, empty clears.</summary>
        public string? EpgSourceId { get; init; }

        /// <summary>New channel key; null keeps, empty clears.</summary>
        public string? EpgChannelKey { get; init; }

        /// <summary>New archive length in days, or null to keep the one on the row.</summary>
        public int? ArchiveDays { get; init; }

        /// <summary>New user agent; null keeps, empty clears.</summary>
        public string? RestreamUserAgent { get; init; }

        /// <summary>New proxy; null keeps, empty clears.</summary>
        public string? RestreamProxy { get; init; }

        /// <summary>New cookies; null keeps, empty clears.</summary>
        public string? RestreamCookies { get; init; }

        /// <summary>New headers; null keeps, empty clears.</summary>
        public string? RestreamHeaders { get; init; }

        /// <summary>New source list; null keeps, empty clears.</summary>
        public IReadOnlyList<string>? Sources { get; init; }

        /// <summary>New file list; null keeps, empty clears.</summary>
        public IReadOnlyList<string>? Files { get; init; }

        /// <summary>
        /// New set of bouquets the stream belongs to; null keeps, empty leaves every bouquet.
        /// </summary>
        /// <remarks>
        /// A <b>set</b> rather than a list: the field is a picker on the stream's own form, so
        /// it answers "which packages is this stream in" and says nothing about where it sits
        /// inside one. A bouquet the stream joins takes it at the end of that bouquet's list;
        /// its position in a bouquet it already belongs to is the bouquet screen's to decide.
        /// </remarks>
        public IReadOnlyList<Guid>? BouquetIds { get; init; }
    }
}
