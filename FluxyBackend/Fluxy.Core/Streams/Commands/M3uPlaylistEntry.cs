namespace Fluxy.Core.Streams.Commands
{
    /// <summary>
    /// One stream a parsed M3U playlist asks to create.
    /// </summary>
    /// <remarks>
    /// The name is already resolved by the parser: the title after the comma of the
    /// <c>#EXTINF</c> line when it has one, otherwise <c>tvg-name</c>, otherwise the
    /// <c>Stream N</c> fallback. <see cref="LineNumber"/> is kept so a refusal can name the
    /// line of the file the operator has to fix rather than an ordinal of an answer they
    /// never see.
    /// </remarks>
    public sealed record M3uPlaylistEntry
    {
        /// <summary>Display name the entry asks for.</summary>
        public required string Name { get; init; }

        /// <summary>Absolute address the stream relays from.</summary>
        public required string Source { get; init; }

        /// <summary>
        /// Icon address from <c>tvg-logo</c>, or null when the entry named none. Stored on a
        /// newly created stream only; an import never overwrites an existing row's logo.
        /// </summary>
        public string? LogoUrl { get; init; }

        /// <summary>One-based line the entry starts on, for a refusal to name it.</summary>
        public required int LineNumber { get; init; }
    }
}
