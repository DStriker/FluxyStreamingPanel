namespace Fluxy.API.Contracts.Content
{
    /// <summary>
    /// What the add-category form sends to make a category in one request.
    /// </summary>
    /// <remarks>
    /// There is deliberately no <c>sortOrder</c> here. A new category joins the end of its
    /// type's list, and where "the end" is (the highest position plus one) is the service's
    /// fact to read from the database - a caller naming a position would be one stale screen
    /// away from overwriting a row an operator has just moved. The type is required rather
    /// than defaulted: a category without a kind would be one no content list knows where to
    /// show.
    /// </remarks>
    public sealed record CreateContentCategoryRequest
    {
        /// <summary>Name of the category, as typed.</summary>
        public string? Name { get; init; }

        /// <summary>The kind of content the category sorts, as the name of the enum member.</summary>
        public string? Type { get; init; }
    }

    /// <summary>
    /// What the rename form sends: the one field a category can be given that it does not
    /// already have.
    /// </summary>
    /// <remarks>
    /// Every property is optional and an absent one means "leave it alone", the convention
    /// every other PATCH on this API follows. A category's type never moves and its position
    /// is not written one row at a time - the list is re-arranged wholesale - so the name is
    /// the whole of what this body can usefully carry.
    /// </remarks>
    public sealed record UpdateContentCategoryRequest
    {
        /// <summary>New name, or null to keep the one on the row.</summary>
        public string? Name { get; init; }
    }

    /// <summary>
    /// What a drop after a drag sends: the whole new order of one type's categories.
    /// </summary>
    /// <remarks>
    /// The identifiers travel as the full ordered list rather than as one moved row with its
    /// new position, because the caller has just dragged on a screen that shows the entire
    /// list and one write of 1..N over the named rows is atomic where a moved row plus
    /// shifting the others is several. The list has to name every category of the type
    /// exactly once - the server refuses a subset or a duplicate rather than guessing what
    /// the unnamed rows should now be at.
    /// </remarks>
    public sealed record ReorderContentCategoriesRequest
    {
        /// <summary>The kind of content whose list was re-arranged, as the name of the enum member.</summary>
        public string? ContentType { get; init; }

        /// <summary>Every category of the type, in the order they should hold from now on.</summary>
        public IReadOnlyList<string>? OrderedIds { get; init; }
    }
}
