using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Models;
using Fluxy.Core.Streams.Outcomes;
using Fluxy.Core.Streams.ReadModels;

namespace Fluxy.Core.Streams.Interfaces
{
    /// <summary>
    /// Reads the configured audio streams and changes them on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An audio stream has one payload shape - an ordered list of sources - so the rule that
    /// "the type owns the fields only it can use" is simpler here than for a video stream: both
    /// kinds relay from the same list, and the type decides only how the relay is started. What
    /// remains is the ordinary rule that a stream with nothing to relay is refused.
    /// </para>
    /// <para>
    /// The service owns the rules and the sentences; the controller owns transport. Neither
    /// knows the other's vocabulary, which is what lets a refusal be reworded without changing
    /// a status code and a status be changed without touching a rule.
    /// </para>
    /// </remarks>
    public interface IAudioStreamService
    {
        /// <summary>
        /// Reads one page of the audio streams table, filtered and ordered the way the caller
        /// asked.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many streams one page holds.</param>
        /// <param name="search">Case-insensitive substring of the stream's name.</param>
        /// <param name="categoryId">Only streams of this category, or null for every category.</param>
        /// <param name="status">Only streams in this state, or null for both.</param>
        /// <param name="type">Only streams of this kind, or null for both.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The page and the size of the filtered list.</returns>
        Task<AudioStreamPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? categoryId,
            AudioStreamStatus? status,
            AudioStreamType? type,
            AudioStreamSortField sortBy,
            AudioStreamSortOrder sortOrder,
            CancellationToken cancellationToken = default);

        /// <summary>Reads one stream in full, including its sources.</summary>
        /// <param name="id">Identifier of the stream.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The stream, or null when no row carries the identifier.</returns>
        Task<AudioStream?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Creates a stream in its category.</summary>
        /// <param name="input">Everything the stream is.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, carrying the identifier once the row exists.</returns>
        Task<AudioStreamOutcome> CreateAsync(
            NewAudioStream input,
            CancellationToken cancellationToken = default);

        /// <summary>Changes a stream. Every field a body can name is optional.</summary>
        /// <param name="id">Identifier of the stream to change.</param>
        /// <param name="patch">What to change about it.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome, naming the field that was refused when one was.</returns>
        Task<AudioStreamOutcome> UpdateAsync(
            Guid id,
            AudioStreamPatch patch,
            CancellationToken cancellationToken = default);

        /// <summary>Deletes a stream together with its sources.</summary>
        /// <param name="id">Identifier of the stream to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome: removed, or not found.</returns>
        Task<AudioStreamOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a stream as active.
        /// </summary>
        /// <remarks>
        /// The status is all this changes. Nothing here starts a process, because the process
        /// that relays an audio stream is not part of this API: the action writes the row's
        /// state, which is the fact that the stream is meant to be relaying.
        /// </remarks>
        /// <param name="id">Identifier of the stream to start.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome: started, or not found.</returns>
        Task<AudioStreamOutcome> StartAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Marks a stream as no longer active. The mirror of <see cref="StartAsync"/>.</summary>
        /// <param name="id">Identifier of the stream to stop.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome: stopped, or not found.</returns>
        Task<AudioStreamOutcome> StopAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a stream as active again.
        /// </summary>
        /// <remarks>
        /// A restart is the same state transition as a start, with its own outcome so the
        /// client can say what the operator asked for.
        /// </remarks>
        /// <param name="id">Identifier of the stream to restart.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The outcome: restarted, or not found.</returns>
        Task<AudioStreamOutcome> RestartAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
