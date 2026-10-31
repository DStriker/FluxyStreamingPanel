using Fluxy.Core.Blacklists.Enums;
using Fluxy.Core.Blacklists.Models;
using Fluxy.Core.Persistence.Base;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of one blacklist entry. It is mutable, has no behaviour beyond
    /// mapping to <see cref="BlacklistEntry"/>, and knows nothing about HTTP.
    /// </summary>
    /// <remarks>
    /// One table holds the entries of all three lists: <see cref="Type"/> is a column of the
    /// row, exactly as the kind of content is a column of a category. The counter is written
    /// by the enforcement that does not exist yet, so nothing in this pass changes it after
    /// the row is inserted - the column simply starts at zero.
    /// </remarks>
    public sealed class BlacklistEntryEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BlacklistEntryEntity"/> class for an
        /// entry that is about to be inserted. The identifier and both audit timestamps are
        /// generated.
        /// </summary>
        public BlacklistEntryEntity()
        {
        }

        /// <summary>Which of the three lists the entry belongs to.</summary>
        public BlacklistType Type { get; set; }

        /// <summary>The value that is refused. Unique within its <see cref="Type"/>.</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// How many attempts the entry has blocked, starting at zero. Written by the
        /// enforcement alone; no endpoint of this pass changes it.
        /// </summary>
        public int BlockedAttempts { get; set; }

        /// <summary>Free-text note an operator left beside the entry, or null.</summary>
        public string? Note { get; set; }

        /// <summary>Builds the business model out of this row.</summary>
        public BlacklistEntry ToModel() => new()
        {
            Id = Id,
            Type = Type,
            Value = Value,
            BlockedAttempts = BlockedAttempts,
            Note = Note,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }
}
