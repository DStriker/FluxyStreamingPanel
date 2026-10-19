using Fluxy.Core.Users.Models;

namespace Fluxy.Core.Models.Streams
{
    /// <summary>
    /// The values a create or an update writes to a stream row.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Separate from <see cref="VideoStream"/> because a write does not name the facts the
    /// storage owns - the identifier, the status, the source the relay reports and the
    /// position in the category - and it does not carry the category's display name, which
    /// belongs to the category rather than to this row.
    /// </para>
    /// <para>
    /// The two lists have already been resolved by the service: a list the chosen type cannot
    /// use arrives empty, and every entry has been trimmed. The repository therefore writes
    /// exactly what it is given instead of repeating the type rule that produced it.
    /// </para>
    /// </remarks>
    public sealed record VideoStreamWrite
    {
        /// <summary>Display name, unique within <see cref="CategoryId"/>.</summary>
        public required string Name { get; init; }

        /// <summary>The kind of stream, which decides which payload fields it uses.</summary>
        public required VideoStreamType Type { get; init; }

        /// <summary>Category the stream is filed under.</summary>
        public required Guid CategoryId { get; init; }

        /// <summary>Address the icon is fetched from, or null when there is none.</summary>
        public string? LogoUrl { get; init; }

        /// <summary>Enigma2 service reference, or null when not published there.</summary>
        public string? Enigma2Sid { get; init; }

        /// <summary>How far behind live the relay runs, in minutes.</summary>
        public required int DelayMinutes { get; init; }

        /// <summary>Series the stream plays, or null when its type does not use one.</summary>
        public Guid? SeriesId { get; init; }

        /// <summary>On-demand item the stream plays, or null when its type does not use one.</summary>
        public Guid? VodId { get; init; }

        /// <summary>Programme guide the schedule is read from, or null when there is none.</summary>
        public Guid? EpgSourceId { get; init; }

        /// <summary>Channel key inside that guide, or null when there is none.</summary>
        public string? EpgChannelKey { get; init; }

        /// <summary>How many days of archive are kept; zero for every kind but Permanent.</summary>
        public required int ArchiveDays { get; init; }

        /// <summary>User agent the relayed requests carry, or null to send none.</summary>
        public string? RestreamUserAgent { get; init; }

        /// <summary>Proxy the relay goes out through, or null to go out directly.</summary>
        public string? RestreamProxy { get; init; }

        /// <summary>Cookies the relayed requests carry, or null to send none.</summary>
        public string? RestreamCookies { get; init; }

        /// <summary>Extra headers the relayed requests carry, as typed.</summary>
        public string? RestreamHeaders { get; init; }

        /// <summary>Sources to relay from, in order; empty when the type uses none.</summary>
        public required IReadOnlyList<string> Sources { get; init; }

        /// <summary>Files to play, in order; empty when the type uses none.</summary>
        public required IReadOnlyList<string> Files { get; init; }
    }
}
