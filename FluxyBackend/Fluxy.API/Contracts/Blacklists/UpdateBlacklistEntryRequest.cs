namespace Fluxy.API.Contracts.Blacklists
{
    /// <summary>
    /// What the edit-entry dialog sends. Every property is optional, and an absent one means
    /// "leave it alone".
    /// </summary>
    /// <remarks>
    /// The list is deliberately absent: it is what the screen already is, and moving an entry
    /// between lists would change what the enforcement matches on while the value stayed the
    /// same. The counter and the added date are absent for the same reason - no request of this
    /// pass writes either.
    ///
    /// The note has a "clear" state where the value does not: an empty string clears it, and an
    /// absent property keeps the stored one. The value is required of a row, so a present but
    /// blank one is refused rather than stored.
    /// </remarks>
    public sealed record UpdateBlacklistEntryRequest
    {
        /// <summary>New value, or null to keep the one on the row.</summary>
        public string? Value { get; init; }

        /// <summary>New note, an empty string to clear it, or null to keep the stored one.</summary>
        public string? Note { get; init; }
    }
}
