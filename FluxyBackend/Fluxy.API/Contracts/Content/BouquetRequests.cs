namespace Fluxy.API.Contracts.Content
{
    /// <summary>
    /// Body of a create: the name of the bouquet and nothing else.
    /// </summary>
    /// <remarks>
    /// There is deliberately no <c>sortOrder</c> here, for the reason the categories' create
    /// request gives: a new bouquet joins the end of the list, and where the end is (the highest
    /// position plus one) is the service's fact to read from the database - a caller naming a
    /// position would be one stale screen away from overwriting a row an operator just moved.
    /// </remarks>
    public sealed record CreateBouquetRequest
    {
        /// <summary>Name of the new bouquet, compared without regard to case for uniqueness.</summary>
        public string? Name { get; init; }
    }

    /// <summary>
    /// Body of a rename. An absent name is left alone.
    /// </summary>
    public sealed record UpdateBouquetRequest
    {
        /// <summary>New name, or null to keep the one on the row.</summary>
        public string? Name { get; init; }
    }

    /// <summary>
    /// Body of the whole-list reorder: every bouquet, in the order it should hold.
    /// </summary>
    /// <remarks>
    /// The identifiers travel as <b>text</b>, like every other list on this API, so a value that
    /// is not a uuid is a <c>validation_failed</c> naming the field rather than a model-binding
    /// refusal from the framework. Duplicates are not collapsed on the way in: an order naming
    /// one row twice is not an order, and the service refuses it together with every other list
    /// that does not name each row exactly once.
    /// </remarks>
    public sealed record ReorderBouquetsRequest
    {
        /// <summary>Every bouquet, in the order they should hold from now on.</summary>
        public IReadOnlyList<string>? OrderedIds { get; init; }
    }

    /// <summary>
    /// Body of the content write: one kind of content and every item of it the bouquet should
    /// hold, in the order it should hold them.
    /// </summary>
    /// <remarks>
    /// The list is the new membership **and** the new order in one statement, because that is
    /// what the content manager produces. It is per kind because the four kinds are ordered
    /// apart: a video stream's position says nothing about an audio stream's.
    /// </remarks>
    public sealed record SetBouquetContentRequest
    {
        /// <summary>
        /// Which of the four kinds the list is about, as the lower-case enum member name.
        /// </summary>
        public string? ContentType { get; init; }

        /// <summary>
        /// Every item of that kind the bouquet should hold, in the order it should hold them.
        /// An empty list empties the bouquet of that kind; an absent one is refused.
        /// </summary>
        public IReadOnlyList<string>? ContentIds { get; init; }
    }
}
