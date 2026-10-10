namespace Fluxy.Core.Models.Users
{
    /// <summary>How one group inside a bulk operation ended.</summary>
    /// <remarks>
    /// The word is <see cref="UserGroupAction"/> - the same one the single-group endpoint
    /// reports - because both the refusals that protect the installation live in the
    /// single-group method this called: a base group that may only be renamed, and a group
    /// still holding members. Both therefore arrive here with exactly the sentence a one-row
    /// request would have produced.
    /// </remarks>
    public sealed record BulkUserGroupItemResult
    {
        /// <summary>Group the entry is about.</summary>
        public required Guid GroupId { get; init; }

        /// <summary>How the operation on that group ended.</summary>
        public required UserGroupAction Action { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }

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
