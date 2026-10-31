using Fluxy.Core.Blacklists.Enums;

namespace Fluxy.Core.Blacklists.ReadModels
{
    /// <summary>
    /// One blacklist entry as the API answers with it.
    /// </summary>
    /// <remarks>
    /// Everything the row holds is here, including the counter and the added date: both are
    /// facts the table draws, and neither is writable through the admin API. The shape is the
    /// same for all three lists - the list itself is the <see cref="Type"/>, and the request
    /// that produced this answer already named it.
    /// </remarks>
    public sealed record BlacklistEntryView
    {
        /// <summary>Identifier of the entry.</summary>
        public required Guid Id { get; init; }

        /// <summary>Which of the three lists the entry belongs to.</summary>
        public required BlacklistType Type { get; init; }

        /// <summary>The value that is refused.</summary>
        public required string Value { get; init; }

        /// <summary>How many attempts the entry has blocked.</summary>
        public required int BlockedAttempts { get; init; }

        /// <summary>Free-text note, or null when there is none.</summary>
        public string? Note { get; init; }

        /// <summary>When the entry was added.</summary>
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
