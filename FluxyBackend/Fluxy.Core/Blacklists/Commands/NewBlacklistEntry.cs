using Fluxy.Core.Blacklists.Enums;

namespace Fluxy.Core.Blacklists.Commands
{
    /// <summary>
    /// Everything an administrator supplies to create a blacklist entry.
    /// </summary>
    /// <remarks>
    /// The counter and the added date are absent on purpose: a new entry starts at zero blocked
    /// attempts and is stamped by the context, and a request that carried either would be a
    /// value nothing is allowed to write.
    /// </remarks>
    public sealed record NewBlacklistEntry
    {
        /// <summary>Which of the three lists the entry belongs to.</summary>
        public required BlacklistType Type { get; init; }

        /// <summary>The value that is refused. Trimmed before it is stored.</summary>
        public required string Value { get; init; }

        /// <summary>Free-text note, or null when there is none.</summary>
        public string? Note { get; init; }
    }
}
