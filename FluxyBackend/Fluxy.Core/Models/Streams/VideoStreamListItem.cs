using Fluxy.Core.Models.Users;

namespace Fluxy.Core.Models.Streams
{
    /// <summary>
    /// One row of the streams table: what a line of it draws, and nothing it does not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A projection rather than <see cref="VideoStream"/> for the same reason
    /// <c>AdminUser</c> is one: the table does not draw the restreaming parameters, the
    /// payload or the schedule, and a list endpoint that answered with all of them would push
    /// twenty full rows over the wire to draw eight columns.
    /// </para>
    /// <para>
    /// <see cref="HasEpg"/> is the one column that is not a column. Whether a stream carries a
    /// programme guide is a question about two fields, and asking it once per row here is what
    /// keeps the table from having to know that the answer has two halves.
    /// </para>
    /// </remarks>
    public sealed record VideoStreamListItem
    {
        /// <summary>Identifier of the stream, as text. The table shows it shortened.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name, unique within its category.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the stream is broadcasting right now.</summary>
        public required VideoStreamStatus Status { get; init; }

        /// <summary>The kind of stream, which decides what it broadcasts.</summary>
        public required VideoStreamType Type { get; init; }

        /// <summary>Category the stream is filed under.</summary>
        public required Guid CategoryId { get; init; }

        /// <summary>Display name of that category, which is what the column draws.</summary>
        public required string CategoryName { get; init; }

        /// <summary>Address the icon is fetched from, or null when the stream has none.</summary>
        public string? LogoUrl { get; init; }

        /// <summary>
        /// Source the relaying process is using right now, reported back rather than entered.
        /// </summary>
        public string? ActiveSource { get; init; }

        /// <summary>
        /// Whether the stream carries a programme guide at all, answered once per row by the
        /// query using the rule in <see cref="VideoStreamEpg"/>.
        /// </summary>
        public bool HasEpg { get; init; }

        /// <summary>Position among the streams of the same category, lowest first.</summary>
        public required int SortOrder { get; init; }
    }
}
