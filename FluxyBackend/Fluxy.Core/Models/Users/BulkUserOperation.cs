namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// One bulk operation over a set of accounts: which operation, over which accounts, and -
    /// for a move - which group they go to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The identifiers travel explicitly rather than as a filter, and that is the whole shape
    /// of the request: the selection a table offers is the rows of the page the operator is
    /// looking at, so the request names them. A predicate instead would answer "every account
    /// that looks like this one" - a different question, asked by a caller who cannot see what
    /// it is about to change, and one whose size the server would have to discover by running
    /// it.
    /// </para>
    /// <para>
    /// <see cref="GroupId"/> belongs to <see cref="BulkUserAction.AssignGroup"/> and to
    /// nothing else. It is ignored rather than refused for the other four, because refusing a
    /// field four of the five operations have no use for would make one request shape
    /// unusable for four of them - and the one operation that does need it is refused by the
    /// service when it comes without one, so nothing can reach a move that silently changes
    /// nothing.
    /// </para>
    /// </remarks>
    public sealed record BulkUserOperation
    {
        /// <summary>What to do to every account named in <see cref="UserIds"/>.</summary>
        public required BulkUserAction Action { get; init; }

        /// <summary>
        /// The accounts to act on, in the order the client listed them. At most
        /// <see cref="BulkOperationLimits.MaxItems"/> of them; duplicates collapse rather than
        /// run an operation twice over the same row.
        /// </summary>
        public required IReadOnlyList<Guid> UserIds { get; init; }

        /// <summary>Destination of a move, or null for the four operations that have none.</summary>
        public Guid? GroupId { get; init; }
    }
}
