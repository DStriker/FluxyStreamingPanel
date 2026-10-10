namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// One bulk operation over a set of groups: which operation, over which groups.
    /// </summary>
    /// <remarks>
    /// The identifiers travel explicitly for the reason <see cref="BulkUserOperation"/> gives
    /// at length: the selection is the rows of the page on screen, so the request names them
    /// rather than describing a predicate whose result nobody has seen.
    ///
    /// There is no third member because a bulk edit of names, levels or permissions does not
    /// exist: those are the fields a form fills in and a base group refuses outright, and a
    /// request that set them across a page would be a request to write the same value into
    /// rows that were never inspected.
    /// </remarks>
    public sealed record BulkUserGroupOperation
    {
        /// <summary>What to do to every group named in <see cref="GroupIds"/>.</summary>
        public required BulkUserGroupAction Action { get; init; }

        /// <summary>
        /// The groups to act on, in the order the client listed them. At most
        /// <see cref="BulkOperationLimits.MaxItems"/> of them; duplicates collapse rather than
        /// run an operation twice over the same row.
        /// </summary>
        public required IReadOnlyList<Guid> GroupIds { get; init; }
    }
}
