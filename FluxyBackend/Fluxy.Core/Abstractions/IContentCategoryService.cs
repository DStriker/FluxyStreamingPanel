using Fluxy.Core.Models.Content;

namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Reads the categories of one kind of content and changes them on an admin's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately a separate service rather than extra methods on an existing one, for the
    /// reason <see cref="IUserGroupService"/> gives: a category shares no field, no rule and
    /// no refusal with an account or a group, and a type carrying all three would have to
    /// answer "whose row?" for every call.
    /// </para>
    /// <para>
    /// The interface says nothing about HTTP, codes or cookies. It reports
    /// <see cref="ContentCategoryAction"/>, and which of those becomes a 201 and which a 409
    /// is the transport layer's business.
    /// </para>
    /// </remarks>
    public interface IContentCategoryService
    {
        /// <summary>
        /// Reads every category of one kind of content, in the order an operator arranged
        /// them in.
        /// </summary>
        /// <remarks>
        /// There is deliberately no paging here. A type's categories are a screenful by
        /// construction - they are the column headers of a content grid, not a per-row
        /// record - and a page of them would put a pager over a list that never outgrows one
        /// page while promising a total the caller did not ask about.
        /// </remarks>
        /// <param name="type">The kind of content whose categories are wanted.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Every category of the type, lowest position first.</returns>
        Task<IReadOnlyList<ContentCategory>> GetAllAsync(
            ContentType type,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a category at the end of its type's list.
        /// </summary>
        /// <param name="input">Name and kind of the new category.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, carrying the identifier once the row exists.</returns>
        Task<ContentCategoryOutcome> CreateAsync(
            NewContentCategory input,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Changes a category. The one field a body can name is the name.
        /// </summary>
        /// <param name="id">Identifier of the category to change.</param>
        /// <param name="patch">What to change about it.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, naming the field that was refused when one was.</returns>
        Task<ContentCategoryOutcome> UpdateAsync(
            Guid id,
            ContentCategoryPatch patch,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a category.
        /// </summary>
        /// <remarks>
        /// There is no "in use" refusal yet, on purpose: no table of content items exists to
        /// be in it, so the check would guard an empty set. It arrives with the content
        /// tables - a foreign key with <c>ON DELETE RESTRICT</c> and a refusal that names the
        /// count - not before them.
        /// </remarks>
        /// <param name="id">Identifier of the category to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome: removed, or not found.</returns>
        Task<ContentCategoryOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Rewrites the order of one type's categories to the one the request names.
        /// </summary>
        /// <param name="input">The type and every one of its categories, in the new order.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The outcome: reordered, refused because the list did not name the whole of the
        /// type exactly once, or refused because the type holds nothing to reorder.
        /// </returns>
        Task<ContentCategoryOutcome> ReorderAsync(
            ReorderContentCategories input,
            CancellationToken cancellationToken = default);
    }
}
