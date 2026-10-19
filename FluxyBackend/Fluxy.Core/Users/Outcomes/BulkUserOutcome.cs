using Fluxy.Core.Users.Enums;

namespace Fluxy.Core.Users.Outcomes
{
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
