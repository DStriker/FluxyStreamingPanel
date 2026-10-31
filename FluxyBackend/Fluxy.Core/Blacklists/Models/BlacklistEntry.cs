using Fluxy.Core.Blacklists.Enums;

namespace Fluxy.Core.Blacklists.Models
{
    /// <summary>
    /// Immutable business representation of one blacklist entry: a value that is refused, the
    /// list it belongs to, how many attempts it has blocked and the note an operator left
    /// beside it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One table holds all three lists: <see cref="Type"/> is an attribute of the row rather
    /// than a separate table per kind, because the entry has the same shape whichever list it
    /// belongs to and the value is what a caller matches on.
    /// </para>
    /// <para>
    /// <see cref="BlockedAttempts"/> is written by the enforcement that does not exist yet -
    /// the future public API increments it as it refuses traffic. Nothing in this pass writes
    /// it after the row is created, which is why the admin API has no way to change it: an
    /// endpoint that could set a counter nobody reads yet would be a write with no caller.
    /// </para>
    /// </remarks>
    public sealed record BlacklistEntry
    {
        /// <summary>Identifier of the entry.</summary>
        public required Guid Id { get; init; }

        /// <summary>Which of the three lists the entry belongs to.</summary>
        public required BlacklistType Type { get; init; }

        /// <summary>
        /// The value that is refused, at most
        /// <see cref="Utils.BlacklistEntryLimits.ValueMaxLength"/> characters. For an
        /// <see cref="BlacklistType.Ip"/> entry it is a single valid IPv4 or IPv6 address; for
        /// the other two kinds it is the literal string the enforcement matches on and is not
        /// interpreted.
        /// </summary>
        public required string Value { get; init; }

        /// <summary>
        /// How many attempts this entry has blocked. Starts at zero and is incremented by the
        /// enforcement alone; no endpoint of this pass changes it.
        /// </summary>
        public required int BlockedAttempts { get; init; }

        /// <summary>
        /// Free-text note an operator left beside the entry, at most
        /// <see cref="Utils.BlacklistEntryLimits.NoteMaxLength"/> characters, or null when
        /// there is none.
        /// </summary>
        public string? Note { get; init; }

        /// <summary>When the entry was added. No endpoint of this pass changes it.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>When the row was last written.</summary>
        public required DateTimeOffset UpdatedAt { get; init; }
    }
}
