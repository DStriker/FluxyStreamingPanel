namespace Fluxy.Core.Content.ReadModels
{
    /// <summary>
    /// One content item as a bouquet screen names it: an identifier, a display name and the
    /// category it is filed under.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately not the content's own list model. The two lists of the content manager
    /// exist to be moved between, and almost every field beyond the name would be a column
    /// nobody reads in a list that can hold thousands of rows - the state, the kind and the
    /// rest belong to the tables that already show them.
    /// </para>
    /// <para>
    /// The category is the one exception, and it earns its place: it is what the filter over
    /// each list works on, which is how an operator moves a whole section into a package at
    /// once instead of item by item. It travels beside the identifier rather than inside it
    /// because the category has an identity of its own - the screen groups and filters by it,
    /// and two categories may carry the same name across the four content kinds.
    /// </para>
    /// </remarks>
    public sealed record BouquetMember
    {
        /// <summary>Identifier of the content item.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name of the content item.</summary>
        public required string Name { get; init; }

        /// <summary>Identifier of the category the item is filed under.</summary>
        public required Guid CategoryId { get; init; }

        /// <summary>Display name of that category.</summary>
        public required string CategoryName { get; init; }
    }
}
