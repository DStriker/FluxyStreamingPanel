using Fluxy.Core.Streams.Models;
using Fluxy.Core.Users.Models;

namespace Fluxy.API.Contracts.Streams
{
    /// <summary>
    /// What the add-stream form sends to make a stream in one request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There is deliberately no <c>sortOrder</c> and no <c>status</c> here, for the reason
    /// <c>VideoStream</c> gives: a new stream joins the end of its category's list, and where
    /// exactly that is is the service's fact to read from the database; and every stream
    /// starts stopped, so a body that could open with it running would be a claim about a
    /// process nothing has told to relay anything yet.
    /// </para>
    /// <para>
    /// <b>Only the payload the chosen kind of stream uses should be filled in.</b> A permanent,
    /// on-demand or redirect stream takes a list of <c>sources</c>; a series stream takes
    /// <c>seriesId</c>; a video-playlist stream takes <c>vodId</c>; a file-playlist stream
    /// takes a list of <c>files</c>. The others are accepted and then dropped rather than
    /// refused, because a form whose operator has just changed the type is still carrying the
    /// previous type's values - refusing them would answer a request nobody meant to make.
    /// </para>
    /// <para>
    /// The three references travel as text rather than as uuids, so that the same body shape
    /// can be reused by the change endpoint, where <c>null</c> means "keep" and an empty
    /// string means "clear". Here both mean "none", which is the only thing a create can mean.
    /// </para>
    /// </remarks>
    public sealed record CreateVideoStreamRequest
    {
        /// <summary>Name of the stream, as typed. Unique within its category.</summary>
        public string? Name { get; init; }

        /// <summary>
        /// The kind of stream, as the name of the enum member compared case insensitively -
        /// <c>permanent</c>, <c>ondemand</c>, <c>redirect</c>, <c>onlineseries</c>,
        /// <c>vodplaylist</c> or <c>fileplaylist</c>.
        /// </summary>
        public string? Type { get; init; }

        /// <summary>Identifier of the category the stream is filed under, as text.</summary>
        public string? CategoryId { get; init; }

        /// <summary>Address the icon is fetched from, or none.</summary>
        public string? LogoUrl { get; init; }

        /// <summary>Enigma2 service reference as text, or none.</summary>
        public string? Enigma2Sid { get; init; }

        /// <summary>How far behind live the relay runs, in minutes. Absent means none.</summary>
        public int? DelayMinutes { get; init; }

        /// <summary>Series the stream plays, for a series stream. As text.</summary>
        public string? SeriesId { get; init; }

        /// <summary>On-demand item the stream plays, for a video-playlist stream. As text.</summary>
        public string? VodId { get; init; }

        /// <summary>Programme guide the schedule is read from, or none. As text.</summary>
        public string? EpgSourceId { get; init; }

        /// <summary>Key the channel is filed under inside that guide, or none.</summary>
        public string? EpgChannelKey { get; init; }

        /// <summary>How many days of archive are kept, and only for a permanent stream. Absent means none.</summary>
        public int? ArchiveDays { get; init; }

        /// <summary>User agent the relayed requests carry, or none.</summary>
        public string? RestreamUserAgent { get; init; }

        /// <summary>Proxy the relay goes out through, or none.</summary>
        public string? RestreamProxy { get; init; }

        /// <summary>Cookies the relayed requests carry, or none.</summary>
        public string? RestreamCookies { get; init; }

        /// <summary>Extra headers the relayed requests carry, as typed.</summary>
        public string? RestreamHeaders { get; init; }

        /// <summary>
        /// Sources to relay from, in order, the first as the primary and the rest as the
        /// fallback.
        /// </summary>
        public IReadOnlyList<string>? Sources { get; init; }

        /// <summary>Files to play, in order, for a file-playlist stream.</summary>
        public IReadOnlyList<string>? Files { get; init; }
    }

    /// <summary>
    /// What the edit-stream form sends. Every property is optional, and an absent one means
    /// "leave it alone".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Status is not here on purpose.</b> It belongs to the start and stop actions, and a
    /// body that could set it would make "this stream is running" a claim an edit form makes
    /// rather than a fact about a process.
    /// </para>
    /// <para>
    /// A nullable text field answers "keep" and "clear" without a second field to tell them
    /// apart: <c>null</c> keeps whatever the row holds, an empty string clears it, and
    /// anything else is the new value. The three references work the same way, which is why
    /// they travel as text - a null uuid there would be ambiguous between "leave it" and
    /// "clear it", and those are two different requests.
    /// </para>
    /// <para>
    /// The two lists follow the same shape with one extra state: an absent list keeps the
    /// stored one, an empty list clears it, and a list with entries replaces the stored one
    /// wholesale in the order they are named. Replacing rather than patching is what makes the
    /// order on the operator's screen the order the relay plays in, with no position
    /// arithmetic to get wrong.
    /// </para>
    /// </remarks>
    public sealed record UpdateVideoStreamRequest
    {
        /// <summary>New name, or null to keep the one on the row.</summary>
        public string? Name { get; init; }

        /// <summary>New kind of stream, as the name of the enum member, or null to keep.</summary>
        public string? Type { get; init; }

        /// <summary>New category, as text; null keeps.</summary>
        public string? CategoryId { get; init; }

        /// <summary>New icon address; null keeps, empty clears.</summary>
        public string? LogoUrl { get; init; }

        /// <summary>New service reference; null keeps, empty clears.</summary>
        public string? Enigma2Sid { get; init; }

        /// <summary>New delay in minutes, or null to keep.</summary>
        public int? DelayMinutes { get; init; }

        /// <summary>New series reference; null keeps, empty clears.</summary>
        public string? SeriesId { get; init; }

        /// <summary>New on-demand reference; null keeps, empty clears.</summary>
        public string? VodId { get; init; }

        /// <summary>New programme guide reference; null keeps, empty clears.</summary>
        public string? EpgSourceId { get; init; }

        /// <summary>New channel key; null keeps, empty clears.</summary>
        public string? EpgChannelKey { get; init; }

        /// <summary>New archive length in days, or null to keep.</summary>
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
