using Fluxy.Core.Content.Interfaces;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Models;
using Fluxy.Core.Streams.ReadModels;

namespace Fluxy.Core.Streams.Interfaces
{
    /// <summary>
    /// Reads the configured streams and changes them on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately a separate service rather than extra methods on an existing one, for the
    /// reason <see cref="IContentCategoryService"/> gives: a stream shares no field, no rule
    /// and no refusal with an account, a group or a category, and a type carrying all of them
    /// would have to answer "whose row?" for every call.
    /// </para>
    /// <para>
    /// The interface says nothing about HTTP, codes or cookies. It reports
    /// <see cref="VideoStreamAction"/>, and which of those becomes a 201 and which a 409 is the
    /// transport layer's business.
    /// </para>
    /// <para>
    /// Two rules live in the implementation rather than at the call sites, so that no second
    /// endpoint reaching the same action can forget them: a name is unique <i>within its
    /// category</i>, and a stream may only carry the payload its <see cref="VideoStreamType"/>
    /// uses. Starting and stopping are their own actions rather than a change of the
    /// <see cref="VideoStreamStatus"/> through <see cref="UpdateAsync"/>, because the status is
    /// the one thing that is not a field of a form.
    /// </para>
    /// </remarks>
    public interface IVideoStreamService
    {
        /// <summary>
        /// Reads one page of the streams table, filtered and ordered the way the caller asked.
        /// </summary>
        /// <remarks>
        /// Search, filters, order and page are all applied in the query and before the count,
        /// for the reason the accounts list gives: <c>Total</c> is what the pager draws, and a
        /// total counted over everything beside rows filtered in the browser offers four pages
        /// of five rows when the filter admitted one.
        /// </remarks>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many streams one page holds.</param>
        /// <param name="search">Case-insensitive substring of the stream's name.</param>
        /// <param name="categoryId">Only streams of this category, or null for every category.</param>
        /// <param name="status">Only streams in this state, or null for both.</param>
        /// <param name="type">Only streams of this kind, or null for every kind.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The page and the size of the filtered list.</returns>
        Task<VideoStreamPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? categoryId,
            VideoStreamStatus? status,
            VideoStreamType? type,
            VideoStreamSortField sortBy,
            VideoStreamSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>Reads one stream in full, including the two lists it is built from.</summary>
        /// <param name="id">Identifier of the stream.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The stream, or null when there is no such one.</returns>
        Task<VideoStream?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Creates a stream in its category.</summary>
        /// <param name="input">Everything the stream is, except its status.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, carrying the identifier once the row exists.</returns>
        Task<VideoStreamOutcome> CreateAsync(
            NewVideoStream input,
            CancellationToken cancellationToken = default);

        /// <summary>Changes a stream. Every field a body can name is optional.</summary>
        /// <param name="id">Identifier of the stream to change.</param>
        /// <param name="patch">What to change about it.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, naming the field that was refused when one was.</returns>
        Task<VideoStreamOutcome> UpdateAsync(
            Guid id,
            VideoStreamPatch patch,
            CancellationToken cancellationToken = default);

        /// <summary>Deletes a stream together with its sources and its files.</summary>
        /// <param name="id">Identifier of the stream to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome: removed, or not found.</returns>
        Task<VideoStreamOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a stream as broadcasting.
        /// </summary>
        /// <remarks>
        /// The status is all this changes. Nothing here starts a process, because no process
        /// exists to start yet - the action is the row's state and the fact that a stream is
        /// meant to be relaying, and the part that actually relays it arrives with the relaying
        /// process itself.
        /// </remarks>
        /// <param name="id">Identifier of the stream to start.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome: started, or not found.</returns>
        Task<VideoStreamOutcome> StartAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Marks a stream as no longer broadcasting. The mirror of <see cref="StartAsync"/>.</summary>
        /// <param name="id">Identifier of the stream to stop.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome: stopped, or not found.</returns>
        Task<VideoStreamOutcome> StopAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a stream as broadcasting again.
        /// </summary>
        /// <remarks>
        /// A restart is the same state transition as a start, with its own outcome so the
        /// client can say what the operator asked for. No process is signalled, for the reason
        /// <see cref="StartAsync"/> gives: the relaying process does not exist here yet.
        /// </remarks>
        /// <param name="id">Identifier of the stream to restart.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome: restarted, or not found.</returns>
        Task<VideoStreamOutcome> RestartAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
