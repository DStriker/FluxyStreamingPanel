namespace Fluxy.API.Contracts.Blacklists
{
    /// <summary>
    /// One blacklist entry as the API answers with it: the value, its list, the counter and the
    /// note.
    /// </summary>
    /// <remarks>
    /// The counter and the added date are present because the table draws them; neither is
    /// writable through this API - the enforcement increments the counter, and the added date
    /// is stamped once when the row is inserted.
    /// </remarks>
    public sealed record AdminBlacklistEntryResponse
    {
        /// <summary>Identifier of the entry, as text. The table shows it shortened.</summary>
        public required string Id { get; init; }

        /// <summary>Which of the three lists the entry belongs to.</summary>
        public required string Type { get; init; }

        /// <summary>The value that is refused.</summary>
        public required string Value { get; init; }

        /// <summary>How many attempts the entry has blocked.</summary>
        public required int BlockedAttempts { get; init; }

        /// <summary>Free-text note, or null when there is none.</summary>
        public string? Note { get; init; }

        /// <summary>When the entry was added, as an ISO timestamp.</summary>
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
