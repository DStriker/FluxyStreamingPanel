namespace Fluxy.API.Contracts.Epg
{
    /// <summary>
    /// One programme guide source as the API answers with it: every operator-owned field and
    /// the moment of the last refresh.
    /// </summary>
    /// <remarks>
    /// The raw guide is deliberately absent. It is what the updater parses, it can be
    /// megabytes, and no screen draws it - a page of twenty sources must not carry twenty
    /// files over the wire.
    /// </remarks>
    public sealed record AdminEpgSourceResponse
    {
        /// <summary>Identifier of the source, as text. The table shows it shortened.</summary>
        public required string Id { get; init; }

        /// <summary>Display name, unique across the installation.</summary>
        public required string Name { get; init; }

        /// <summary>Absolute http(s) address the guide is fetched from.</summary>
        public required string Url { get; init; }

        /// <summary>How many days of programmes are kept for this source.</summary>
        public required int DaysKeep { get; init; }

        /// <summary>When the guide was last refreshed, or null when it never has been.</summary>
        public DateTimeOffset? LastEpgUpdatedAt { get; init; }
    }
}
