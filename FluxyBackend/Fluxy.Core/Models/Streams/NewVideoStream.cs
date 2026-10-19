using Fluxy.Core.Users.Models;

namespace Fluxy.Core.Models.Streams
{
    /// <summary>
    /// Everything an administrator supplies to create a stream in one request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There is deliberately no <c>SortOrder</c> here. A new stream joins the end of its
    /// category's list, and where exactly "the end" is (the highest position of that category,
    /// plus one) is the service's fact to read from the database, not the caller's to claim -
    /// a caller naming a position would be one stale screen away from overwriting a neighbour
    /// an operator has just moved.
    /// </para>
    /// <para>
    /// There is no <c>Status</c> either. Every stream starts stopped, and the only things that
    /// ever change it are the start and stop actions - a create that could open with a stream
    /// running would be a claim about a process that has not been told to relay anything yet.
    /// </para>
    /// <para>
    /// The three identifiers that name rows of a domain that does not exist here yet
    /// (<see cref="SeriesId"/>, <see cref="VodId"/> and <see cref="EpgSourceId"/>) are plain
    /// <see cref="Guid"/>s rather than foreign keys, for the reason the read model gives: there
    /// is no table behind them to be a key to, so they are opaque references the relaying
    /// process resolves on its own.
    /// </para>
    /// </remarks>
    public sealed record NewVideoStream
    {
        /// <summary>Name the stream gets. Trimmed, compared case sensitively.</summary>
        public required string Name { get; init; }

        /// <summary>The kind of stream, which decides what it broadcasts.</summary>
        public required VideoStreamType Type { get; init; }

        /// <summary>Category the stream is filed under. Must be a category of video streams.</summary>
        public required Guid CategoryId { get; init; }

        /// <summary>Address the icon is fetched from, or null to draw none.</summary>
        public string? LogoUrl { get; init; }

        /// <summary>Enigma2 service reference, or null when the stream is not published there.</summary>
        public string? Enigma2Sid { get; init; }

        /// <summary>How far behind live the relay runs, in minutes. Zero for none.</summary>
        public required int DelayMinutes { get; init; }

        /// <summary>
        /// Series this stream plays, for <see cref="VideoStreamType.OnlineSeries"/> and
        /// nothing else.
        /// </summary>
        public Guid? SeriesId { get; init; }

        /// <summary>
        /// On-demand item this stream plays, for <see cref="VideoStreamType.VodPlaylist"/> and
        /// nothing else.
        /// </summary>
        public Guid? VodId { get; init; }

        /// <summary>Programme guide the schedule is read from, or null to carry none.</summary>
        public Guid? EpgSourceId { get; init; }

        /// <summary>Key the channel is filed under inside that guide, or null to carry none.</summary>
        public string? EpgChannelKey { get; init; }

        /// <summary>
        /// How many days of archive are kept, and only for
        /// <see cref="VideoStreamType.Permanent"/>. Zero for none.
        /// </summary>
        public required int ArchiveDays { get; init; }

        /// <summary>User agent the relayed requests carry, or null to send none.</summary>
        public string? RestreamUserAgent { get; init; }

        /// <summary>Proxy the relay goes out through, or null to go out directly.</summary>
        public string? RestreamProxy { get; init; }

        /// <summary>Cookies the relayed requests carry, or null to send none.</summary>
        public string? RestreamCookies { get; init; }

        /// <summary>Extra headers the relayed requests carry, as typed.</summary>
        public string? RestreamHeaders { get; init; }

        /// <summary>
        /// Sources to relay from, in order, first as the primary and the rest as the fallback.
        /// Empty for the kinds that do not relay from a URL.
        /// </summary>
        public required IReadOnlyList<string> Sources { get; init; }

        /// <summary>Files to play, in order. Empty for every kind but the file playlist.</summary>
        public required IReadOnlyList<string> Files { get; init; }
    }

    /// <summary>
    /// What an administrator changes about an existing stream. Every property is optional, and
    /// an absent one means "leave it alone".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Status is not here on purpose.</b> It belongs to the start and stop actions, which are
    /// the two things that change it, and a body that could set it would make "the stream is
    /// running" a claim an edit form makes rather than a fact about a process.
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

        /// <summary>New on-demand reference; null keeps, empty clears.</summary>
        public string? VodId { get; init; }

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
    }
}
