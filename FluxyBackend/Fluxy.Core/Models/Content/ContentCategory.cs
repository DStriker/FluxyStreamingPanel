namespace Fluxy.Core.Models.Content
{
    /// <summary>
    /// Immutable business representation of one category of one kind of content.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A category is named, typed and ordered - nothing else. The type is the partition the
    /// category lives in (<see cref="ContentType"/>), and every category of one type is shown
    /// to an operator in <see cref="SortOrder"/>: the order they arranged it in, kept as a
    /// plain integer so reading a page is one <c>ORDER BY</c> and re-arranging is writing the
    /// numbers 1..N over the rows the request names.
    /// </para>
    /// <para>
    /// <see cref="SortOrder"/> is a position, not a rank with a meaning of its own. Gaps
    /// between the values are expected - deletions leave them behind, and nothing is ever
    /// renumbered to close them - so nothing may assume the values are contiguous.
    /// </para>
    /// </remarks>
    public sealed record ContentCategory
    {
        /// <summary>Identifier of the category.</summary>
        public required Guid Id { get; init; }

        /// <summary>
        /// Display name, unique within its <see cref="Type"/>, compared case sensitively -
        /// the same rule the accounts and the groups are held to.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>The kind of content this category sorts.</summary>
        public required ContentType Type { get; init; }

        /// <summary>
        /// Position among the categories of the same <see cref="Type"/>, lowest first.
        /// Assigned when the category is created (after the current last one) and rewritten
        /// wholesale when an operator re-arranges the list.
        /// </summary>
        public required int SortOrder { get; init; }
    }
}
