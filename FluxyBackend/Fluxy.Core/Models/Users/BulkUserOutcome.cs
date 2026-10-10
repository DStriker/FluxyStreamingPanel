namespace Fluxy.Core.Models.Users
{
    /// <summary>How one account inside a bulk operation ended.</summary>
    /// <remarks>
    /// The word is <see cref="AdminUserAction"/> - the very same one the single-account
    /// endpoint reports - and that is the point rather than an economy. Every refusal this
    /// service can give (the <c>Cannot</c> members, the per-target permission, the state the
    /// row is already in) is produced by the single-account method the bulk operation called,
    /// so a client that reads <c>cannot_block_self</c> off one row of a bulk answer is
    /// reading the sentence the one-row endpoint would have given it.
    /// </remarks>
    public sealed record BulkUserItemResult
    {
        /// <summary>Account the entry is about.</summary>
        public required Guid UserId { get; init; }

        /// <summary>How the operation on that account ended.</summary>
        public required AdminUserAction Action { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }

    /// <summary>
    /// The whole of a bulk operation: what was asked for, and how each account ended.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Partial success is the shape, on purpose.</b> The accounts of one page are
    /// independent rows, and one of them being the caller's own is a fact about that row and
    /// not about the other nineteen. An all-or-nothing bulk would turn "delete these 20" into
    /// a request that changed nothing because one of the 20 was protected - and the operator
    /// would have no way to tell which one, because the refusal that explains it would have
    /// been thrown away with the rollback.
    /// </para>
    /// <para>
    /// So there is no status here and no success flag: those are facts about how the answer
    /// is transported, and the service knows nothing about transport. Which of these entries
    /// is an <c>ok</c>, what the whole operation's code is and which status carries it are the
    /// API layer's answer, stated beside the other response tables.
    /// </para>
    /// </remarks>
    public sealed record BulkUserOutcome
    {
        /// <summary>The operation this answer is for, echoed so the answer describes itself.</summary>
        public required BulkUserAction Action { get; init; }

        /// <summary>
        /// One entry per identifier the request named, in the order they were named. Never
        /// empty for an operation that reached the service - an operation that cannot be
        /// applied to a particular account still reports that account and why.
        /// </summary>
        public required IReadOnlyList<BulkUserItemResult> Items { get; init; }
    }
}
