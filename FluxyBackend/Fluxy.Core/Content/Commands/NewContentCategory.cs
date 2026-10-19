using Fluxy.Core.Content.Enums;

namespace Fluxy.Core.Content.Commands
{
    /// <summary>
    /// Everything an administrator supplies to create a category in one request.
    /// </summary>
    /// <remarks>
    /// There is deliberately no <c>SortOrder</c> here: a new category joins the end of its
    /// type's list, and where exactly "the end" is (the highest position plus one) is the
    /// service's fact to read from the database, not the caller's to claim. A caller that
    /// named a position would be one stale screen away from overwriting a neighbour an
    /// operator has just moved.
    /// </remarks>
    public sealed record NewContentCategory
    {
        /// <summary>Name the category gets. Trimmed, compared case sensitively.</summary>
        public required string Name { get; init; }

        /// <summary>The kind of content the category sorts.</summary>
        public required ContentType Type { get; init; }
    }

    /// <summary>
    /// What an administrator changes about an existing category. Every property is optional,
    /// and an absent one means "leave it alone".
    /// </summary>
    /// <remarks>
    /// The name is the only thing a category can be given that it does not already have: its
    /// type is the partition it lives in and never moves, and its position is not a field an
    /// individual request writes - the list is re-arranged wholesale, one reordering per type,
    /// rather than by moving one row and shifting the others.
    /// </remarks>
}
