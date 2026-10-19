using Fluxy.Core.Users.Enums;

namespace Fluxy.Core.Users.Outcomes
{
    /// <summary>
    /// The whole of a bulk operation over groups: what was asked for, and how each group
    /// ended.
    /// </summary>
    /// <remarks>
    /// Partial success again, and for the sharper reason the accounts give: three of the
    /// groups on a page may be the installation's own foundation rows, and "delete these five"
    /// answering nothing at all because two of them were is the least useful thing this
    /// endpoint could do. The refusal that names the offending row travels in the entry.
    /// </remarks>
    public sealed record BulkUserGroupOutcome
    {
        /// <summary>The operation this answer is for, echoed so the answer describes itself.</summary>
        public required BulkUserGroupAction Action { get; init; }

        /// <summary>
        /// One entry per identifier the request named, in the order they were named.
        /// </summary>
        public required IReadOnlyList<BulkUserGroupItemResult> Items { get; init; }
    }
}
