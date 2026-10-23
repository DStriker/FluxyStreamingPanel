using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Utils;
using Fluxy.Core.Users.Models;

namespace Fluxy.Core.Streams.Models
{
    /// <summary>
    /// Immutable business representation of one configured stream: everything the operator
    /// decides about it, plus the one fact the relaying process reports back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A stream is named, typed and filed under a category. Everything
    /// else is either the answer to "what does it broadcast" (<see cref="Sources"/>,
    /// <see cref="Files"/>, <see cref="SeriesId"/>, <see cref="VodId"/> - exactly one of the
    /// four is used, decided by <see cref="Type"/>), or the answer to "how does it relay it"
    /// (<see cref="RestreamUserAgent"/>, <see cref="RestreamProxy"/>,
    /// <see cref="RestreamCookies"/>, <see cref="RestreamHeaders"/>, <see cref="DelayMinutes"/>).
    /// </para>
    /// <para>
    /// <see cref="ActiveSource"/> is the one field nothing in the interface writes. It names
    /// the source the relaying process is using right now, and it can only be known by the
    /// process doing the relaying - an operator may look at it, but a form that offered it as
    /// a field would be a claim about a fact the operator has no way to establish.
    /// </para>
    /// <para>
    /// The two lists are plain ordered strings rather than rows of their own, because that is
    /// exactly what an operator edits: a screen of text fields whose order is the order they
    /// are played in. The rows they are stored as are the storage's business, not the model's.
    /// </para>
    /// </remarks>
    public sealed record VideoStream
    {
        /// <summary>Identifier of the stream.</summary>
        public required Guid Id { get; init; }

        /// <summary>
        /// Display name, at most 200 characters, unique within its
        /// <see cref="CategoryId"/> and compared case sensitively - the same rule the accounts,
        /// the groups and the categories are held to.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>Whether the stream is broadcasting right now.</summary>
        public required VideoStreamStatus Status { get; init; }

        /// <summary>The kind of stream, which decides which of the payload fields is used.</summary>
        public required VideoStreamType Type { get; init; }

        /// <summary>
        /// Category the stream is filed under, always a category of
        /// <see cref="Fluxy.Core.Streams.Models.VideoStream"/>.
        /// </summary>
        public required Guid CategoryId { get; init; }

        /// <summary>Display name of that category, which is what the table column draws.</summary>
        public required string CategoryName { get; init; }

        /// <summary>
        /// Address the icon is fetched from, or null when the stream has none. A URL rather
        /// than an upload: the picture lives wherever it already lives, and this column only
        /// remembers where.
        /// </summary>
        public string? LogoUrl { get; init; }

        /// <summary>
        /// Source the relaying process is using right now, reported back rather than entered.
        /// Null while nothing is relaying or the process has not said which one it took.
        /// </summary>
        public string? ActiveSource { get; init; }

        /// <summary>
        /// Enigma2 service reference, or null when the stream is not published there. Stored as
        /// text because a service reference is a string of segments (<c>1:0:19:...</c>), not a
        /// number - it only reads as one because every segment happens to be decimal.
        /// </summary>
        public string? Enigma2Sid { get; init; }

        /// <summary>
        /// How far behind live the relay runs, in minutes. Zero means no deliberate delay, and
        /// that is the value every new stream starts with.
        /// </summary>
        public required int DelayMinutes { get; init; }

        /// <summary>
        /// Series this stream plays, for <see cref="VideoStreamType.OnlineSeries"/> and nothing
        /// else. Null when the stream is another kind, or when its series is not chosen yet.
        /// </summary>
        public Guid? SeriesId { get; init; }

        /// <summary>
        /// On-demand item this stream plays, for <see cref="VideoStreamType.VodPlaylist"/> and
        /// nothing else.
        /// </summary>
        public Guid? VodId { get; init; }

        /// <summary>
        /// Programme guide the schedule is read from, or null when the stream carries none.
        /// No table backs this identifier yet - the guide sources are their own domain and do
        /// not exist here - so it is an opaque reference rather than a foreign key.
        /// </summary>
        public Guid? EpgSourceId { get; init; }

        /// <summary>
        /// Key the channel is filed under inside that guide, or null when there is none. Kept
        /// beside <see cref="EpgSourceId"/> because a guide that names its channels is only
        /// half of what is needed: two sources may use different keys for the same channel.
        /// </summary>
        public string? EpgChannelKey { get; init; }

        /// <summary>Whether this stream carries a programme guide at all.</summary>
        public bool HasEpg => VideoStreamEpg.Configured(EpgSourceId, EpgChannelKey);

        /// <summary>
        /// How many days of archive are kept, and only for <see cref="VideoStreamType.Permanent"/>
        /// - every other kind is started on request, so "keep yesterday's" is not a question it
        /// can answer. Zero means no archive, which is the value every new stream starts with.
        /// </summary>
        public required int ArchiveDays { get; init; }

        /// <summary>User agent the relayed requests carry, or null to send none.</summary>
        public string? RestreamUserAgent { get; init; }

        /// <summary>Proxy the relay goes out through, or null to go out directly.</summary>
        public string? RestreamProxy { get; init; }

        /// <summary>Cookies the relayed requests carry, or null to send none.</summary>
        public string? RestreamCookies { get; init; }

        /// <summary>
        /// Extra headers the relayed requests carry, kept as the operator typed them rather
        /// than as a parsed structure - the relaying process is what decides how to read them,
        /// and a shape fixed here would be a second opinion about a format it owns.
        /// </summary>
        public string? RestreamHeaders { get; init; }

        /// <summary>
        /// Sources to relay from, in the order they are used and with the ones after the first
        /// acting as the fallback. Used by the three URL kinds and empty for the others.
        /// </summary>
        public required IReadOnlyList<string> Sources { get; init; }

        /// <summary>
        /// Files to play, in order. Used by <see cref="VideoStreamType.FilePlaylist"/> and empty
        /// for every other kind.
        /// </summary>
        public required IReadOnlyList<string> Files { get; init; }
    }
}
