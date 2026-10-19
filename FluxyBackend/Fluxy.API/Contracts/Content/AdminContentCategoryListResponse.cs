using Fluxy.Core.Models.Content;

namespace Fluxy.API.Contracts.Content
{
    /// <summary>
    /// One row of the admin content categories list: what a line of it draws.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The content type travels on every row even though the list was asked for by one type.
    /// An item that names what it is is one a client can cache or carry across screens
    /// without a wrapper to remember the question in, and the cost is a short string a
    /// server that has just filtered by the very value already holds.
    /// </para>
    /// <para>
    /// <see cref="SortOrder"/> is the position the row holds, not the position it is shown
    /// at: the items arrive already ordered by it, so a client that draws them in order never
    /// needs to read it. It is on the row for the client that wants to show or verify a
    /// position without re-deriving it from the index.
    /// </para>
    /// </remarks>
    public sealed record AdminContentCategoryItemResponse
    {
        /// <summary>Identifier of the category, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Display name, unique within its content type, compared case sensitively.</summary>
        public required string Name { get; init; }

        /// <summary>The kind of content this category sorts, as the name of the enum member.</summary>
        public required string ContentType { get; init; }

        /// <summary>Position among the categories of the same kind, lowest first.</summary>
        public required int SortOrder { get; init; }
    }

    /// <summary>
    /// Every category of one kind of content, in the order an operator arranged them in.
    /// </summary>
    /// <remarks>
    /// Deliberately a list and not a page: there is no <c>total</c>, no <c>page</c> and no
    /// <c>pageSize</c>, because there is no pager. A type's categories are a screenful by
    /// construction - they are the section headings of a content grid, not a per-row record
    /// - and a pager over a list that never outgrows one page would be a control promising a
    /// second page that has no reason to exist.
    /// </remarks>
    public sealed record AdminContentCategoryListResponse
    {
        /// <summary>Every category of the type, lowest position first.</summary>
        public required IReadOnlyList<AdminContentCategoryItemResponse> Items { get; init; }
    }
}
