namespace Fluxy.API.Contracts.Streams
{
    /// <summary>
    /// One row of the admin audio streams table: the eight things a line of it draws.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kind and state travel as the name of the enum member, spelled exactly as the profile
    /// spells its role - <c>OnDemand</c>, <c>Running</c> - because that is what a client
    /// already matches on and a second spelling for the same enum would be a second fact
    /// about it.
    /// </para>
    /// <para>
    /// The restreaming parameters and the source list are deliberately absent: the table does
    /// not draw them, and a list endpoint that answered with all of them would push twenty
    /// full rows over the wire to draw eight columns. They arrive with the detail endpoint,
    /// once, when a form asks for them.
    /// </para>
    /// </remarks>
    public sealed record AdminAudioStreamItemResponse
    {
        /// <summary>Identifier of the stream, as text. The table shows it shortened.</summary>
        public required string Id { get; init; }

        /// <summary>Display name, unique within its category and compared case sensitively.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the stream is active right now, as the name of the enum member.</summary>
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
    }

    /// <summary>
    /// One page of the admin audio streams table, with the count the pager is built from.
    /// </summary>
    /// <remarks>
    /// <see cref="Total"/> is the size of the *filtered* list, not of the table. That is the
    /// whole reason search, filters, order and paging are answered by the server.
    /// </remarks>
    public sealed record AdminAudioStreamPageResponse
    {
        /// <summary>The streams this page holds, in the requested order.</summary>
        public required IReadOnlyList<AdminAudioStreamItemResponse> Items { get; init; }

        /// <summary>How many streams the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many streams one page holds.</summary>
        public required int PageSize { get; init; }
    }

    /// <summary>
    /// One audio stream in full, for the edit form - everything the operator can decide about
    /// it, plus the one fact the relaying process reports back.
    /// </summary>
    /// <remarks>
    /// The sources arrive in the order they are relayed, which is the order the form draws its
    /// fields in and the order a save sends them back in.
    /// </remarks>
    public sealed record AdminAudioStreamDetailResponse
    {
        /// <summary>Identifier of the stream, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Display name, unique within its category.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the stream is active right now, as the name of the enum member.</summary>
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

        /// <summary>
        /// Bouquets the stream belongs to, as text, in the bouquets' own order. A <b>set</b>:
        /// it answers "which packages is this stream in" for the form's picker.
        /// </summary>
        public required IReadOnlyList<string> BouquetIds { get; init; }
    }
}
