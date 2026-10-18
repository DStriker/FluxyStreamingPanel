namespace Fluxy.API.Contracts.Streams
{
    /// <summary>
    /// One row of the admin streams table: the ten things a line of it draws.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kind and state travel as the name of the enum member, spelled exactly as the profile
    /// spells its role - <c>Permanent</c>, <c>Running</c> - because that is what a client
    /// already matches on and a second spelling for the same enum would be a second fact
    /// about it.
    /// </para>
    /// <para>
    /// The restreaming parameters, the payload and the schedule are deliberately absent: the
    /// table does not draw them, and a list endpoint that answered with all of them would push
    /// twenty full rows over the wire to draw ten columns. They arrive with the detail
    /// endpoint, once, when a form asks for them.
    /// </para>
    /// <para>
    /// <see cref="HasEpg"/> is the one column that is not a column. Whether a stream carries a
    /// programme guide is a question about two fields, and the server answers it once per row
    /// so the table does not have to know that the answer has two halves.
    /// </para>
    /// </remarks>
    public sealed record AdminVideoStreamItemResponse
    {
        /// <summary>Identifier of the stream, as text. The table shows it shortened.</summary>
        public required string Id { get; init; }

        /// <summary>Display name, unique within its category and compared case sensitively.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the stream is broadcasting right now, as the name of the enum member.</summary>
        public required string Status { get; init; }

        /// <summary>The kind of stream, as the name of the enum member.</summary>
        public required string Type { get; init; }

        /// <summary>Identifier of the category the stream is filed under, as text.</summary>
        public required string CategoryId { get; init; }

        /// <summary>Display name of that category, which is what the column draws.</summary>
        public required string CategoryName { get; init; }

        /// <summary>Address the icon is fetched from, or null when the stream has none.</summary>
        public string? LogoUrl { get; init; }

        /// <summary>
        /// Source the relaying process is using right now, reported back rather than entered.
        /// Null for a stream that is not relaying or whose process has not said which one it
        /// took.
        /// </summary>
        public string? ActiveSource { get; init; }

        /// <summary>Whether the stream carries a programme guide at all.</summary>
        public required bool HasEpg { get; init; }

        /// <summary>Position among the streams of the same category, lowest first.</summary>
        public required int SortOrder { get; init; }
    }

    /// <summary>
    /// One page of the admin streams table, with the count the pager is built from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is the size of the *filtered* list, not of the table. That is the
    /// whole reason search, filters, order and paging are answered by the server: a total
    /// counted over everything beside rows filtered in the browser gives a pager offering four
    /// pages of five rows when the filter admitted one.
    /// </remarks>
    public sealed record AdminVideoStreamPageResponse
    {
        /// <summary>The streams this page holds, in the requested order.</summary>
        public required IReadOnlyList<AdminVideoStreamItemResponse> Items { get; init; }

        /// <summary>How many streams the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many streams one page holds.</summary>
        public required int PageSize { get; init; }
    }

    /// <summary>
    /// One stream in full, for the edit form - everything the operator can decide about it,
    /// plus the one fact the relaying process reports back.
    /// </summary>
    /// <remarks>
    /// The two lists arrive in the order they are relayed or played, which is the order the
    /// form draws its fields in and the order a save sends them back in. The payload the
    /// chosen kind of stream does not use is empty here, so a form that reads a stream and
    /// writes it back cannot carry a value the row never held.
    /// </remarks>
    public sealed record AdminVideoStreamDetailResponse
    {
        /// <summary>Identifier of the stream, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Display name, unique within its category.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the stream is broadcasting right now, as the name of the enum member.</summary>
        public required string Status { get; init; }

        /// <summary>The kind of stream, as the name of the enum member.</summary>
        public required string Type { get; init; }

        /// <summary>Identifier of the category the stream is filed under, as text.</summary>
        public required string CategoryId { get; init; }

        /// <summary>Display name of that category.</summary>
        public required string CategoryName { get; init; }

        /// <summary>Address the icon is fetched from, or null.</summary>
        public string? LogoUrl { get; init; }

        /// <summary>Source the relaying process is using right now, or null.</summary>
        public string? ActiveSource { get; init; }

        /// <summary>Enigma2 service reference as text, or null.</summary>
        public string? Enigma2Sid { get; init; }

        /// <summary>How far behind live the relay runs, in minutes.</summary>
        public required int DelayMinutes { get; init; }

        /// <summary>Series the stream plays, as text, or null.</summary>
        public string? SeriesId { get; init; }

        /// <summary>On-demand item the stream plays, as text, or null.</summary>
        public string? VodId { get; init; }

        /// <summary>Programme guide the schedule is read from, as text, or null.</summary>
        public string? EpgSourceId { get; init; }

        /// <summary>Key the channel is filed under inside that guide, or null.</summary>
        public string? EpgChannelKey { get; init; }

        /// <summary>Whether the stream carries a programme guide at all.</summary>
        public required bool HasEpg { get; init; }

        /// <summary>How many days of archive are kept. Zero for a stream that keeps none.</summary>
        public required int ArchiveDays { get; init; }

        /// <summary>Position among the streams of the same category, lowest first.</summary>
        public required int SortOrder { get; init; }

        /// <summary>User agent the relayed requests carry, or null.</summary>
        public string? RestreamUserAgent { get; init; }

        /// <summary>Proxy the relay goes out through, or null.</summary>
        public string? RestreamProxy { get; init; }

        /// <summary>Cookies the relayed requests carry, or null.</summary>
        public string? RestreamCookies { get; init; }

        /// <summary>Extra headers the relayed requests carry, as typed, or null.</summary>
        public string? RestreamHeaders { get; init; }

        /// <summary>Sources to relay from, in order, first as the primary.</summary>
        public required IReadOnlyList<string> Sources { get; init; }

        /// <summary>Files to play, in order. Empty for every kind but the file playlist.</summary>
        public required IReadOnlyList<string> Files { get; init; }
    }
}
