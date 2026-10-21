using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Interfaces;
using Fluxy.Core.Streams.Models;
using Fluxy.Core.Streams.Outcomes;

namespace Fluxy.Application.Streams.Services
{
    /// <summary>
    /// Applies one operation to a set of streams, one stream at a time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bulk endpoint is not a second implementation of the single-row operations, it is
    /// those operations applied to each row in turn. A category move is still the update that
    /// knows about the category-scoped name, and a start is still the transition that reports
    /// the previous status; a hundred rows in a request are refused for exactly the reasons one
    /// row is.
    /// </para>
    /// <para>
    /// Three rules do live here because they are about the <i>collective</i> operation rather
    /// than about a stream: a delay and an archive are only changed in bulk on permanent
    /// streams, and restream settings only on permanent or on-demand streams. The single-row
    /// form deliberately does not repeat those two, so an edit of one stream stays the general
    /// form it always was; this operation offers a shorter menu and states its own bounds.
    /// </para>
    /// </remarks>
    public sealed class VideoStreamBulkService : IVideoStreamBulkService
    {
        /// <summary>The sentence a delay change on another kind of stream gets.</summary>
        private static readonly string[] DelayRule =
            ["Delay can only be set on permanent streams."];

        /// <summary>The sentence an archive change on another kind of stream gets.</summary>
        private static readonly string[] ArchiveRule =
            ["Archive length can only be set on permanent streams."];

        /// <summary>The sentence a restream change on another kind of stream gets.</summary>
        private static readonly string[] RestreamRule =
            ["Restream settings can only be set on permanent or on-demand streams."];

        /// <summary>The sentence a move without a destination gets.</summary>
        private static readonly string[] CategoryRule =
            ["A bulk move needs the category to move the streams to."];

        /// <summary>The sentence a type change without an allowed destination gets.</summary>
        private static readonly string[] TypeRule =
            ["A bulk type change must choose permanent, on-demand or redirect."];

        /// <summary>The sentence a delay change without a value gets.</summary>
        private static readonly string[] DelayValueRule =
            ["A bulk delay change needs the delay to write, in minutes."];

        /// <summary>The sentence an archive change without a value gets.</summary>
        private static readonly string[] ArchiveValueRule =
            ["A bulk archive change needs the archive length to write, in days."];

        private readonly IVideoStreamService _streams;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoStreamBulkService"/> class.
        /// </summary>
        /// <param name="streams">Service holding the single-stream operations.</param>
        public VideoStreamBulkService(IVideoStreamService streams)
        {
            _streams = streams;
        }

        /// <inheritdoc />
        public async Task<BulkVideoStreamOutcome> BulkAsync(
            BulkVideoStreamOperation operation,
            CancellationToken cancellationToken = default)
        {
            // A payload an operation needs is refused for every row rather than ignored. The
            // transport refuses the same mistakes with a 400 before any row is reached; if one
            // ever arrives here, every stream is answered with the field that was missing
            // instead of the run reporting success over rows it never changed.
            if (MissingPayload(operation) is { } missing)
            {
                return RefuseAll(operation, missing.Field, missing.Sentences);
            }

            var items = new List<BulkVideoStreamItemResult>(operation.StreamIds.Count);

            foreach (var streamId in operation.StreamIds)
            {
                var (outcome, warnings) = operation.Action switch
                {
                    BulkVideoStreamAction.Start =>
                        (await _streams.StartAsync(streamId, cancellationToken), null),

                    BulkVideoStreamAction.Stop =>
                        (await _streams.StopAsync(streamId, cancellationToken), null),

                    BulkVideoStreamAction.Restart =>
                        (await _streams.RestartAsync(streamId, cancellationToken), null),

                    BulkVideoStreamAction.Delete =>
                        (await _streams.DeleteAsync(streamId, cancellationToken), null),

                    BulkVideoStreamAction.MoveCategory => (
                        await _streams.UpdateAsync(
                            streamId,
                            new VideoStreamPatch { CategoryId = operation.CategoryId },
                            cancellationToken),
                        null),

                    BulkVideoStreamAction.ChangeType =>
                        await ChangeTypeAsync(streamId, operation.Type, cancellationToken),

                    BulkVideoStreamAction.ChangeDelay =>
                        await ChangeDelayAsync(streamId, operation.DelayMinutes, cancellationToken),

                    BulkVideoStreamAction.ChangeArchive =>
                        await ChangeArchiveAsync(streamId, operation.ArchiveDays, cancellationToken),

                    BulkVideoStreamAction.ChangeRestream =>
                        await ChangeRestreamAsync(streamId, operation, cancellationToken),

                    // Unreachable: the enum holds no other member and the transport refuses a
                    // name that maps to none. Thrown rather than skipped, because an entry
                    // missing from the answer is a stream the client believes it acted on and
                    // the server never mentioned.
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(operation),
                        operation.Action,
                        "Unknown bulk video stream action.")
                };

                items.Add(new BulkVideoStreamItemResult
                {
                    StreamId = streamId,
                    Action = outcome.Action,
                    Errors = outcome.Errors,
                    Warnings = warnings
                });
            }

            return new BulkVideoStreamOutcome { Action = operation.Action, Items = items };
        }

        /// <summary>
        /// Changes the kind of one stream, clearing the settings the destination cannot carry.
        /// </summary>
        /// <remarks>
        /// A cleared setting is reported as a warning on the row, not as a refusal: the operator
        /// asked for the new kind, and the values that kind cannot hold are the consequence of
        /// that choice. Refusing instead would make a whole selection fail because one stream
        /// happened to keep an archive.
        /// </remarks>
        private async Task<(VideoStreamOutcome Outcome, IReadOnlyList<VideoStreamBulkWarning>? Warnings)>
            ChangeTypeAsync(
                Guid streamId,
                VideoStreamType? type,
                CancellationToken cancellationToken)
        {
            if (type is not { } target || !IsUrlKind(target))
            {
                return (Invalid(streamId, nameof(BulkVideoStreamOperation.Type), TypeRule), null);
            }

            var stream = await _streams.GetAsync(streamId, cancellationToken);

            if (stream is null)
            {
                return (NotFound(streamId), null);
            }

            var warnings = new List<VideoStreamBulkWarning>();
            var patch = new VideoStreamPatch { Type = target };

            if (target is not VideoStreamType.Permanent)
            {
                if (stream.DelayMinutes > 0)
                {
                    patch = patch with { DelayMinutes = 0 };
                    warnings.Add(VideoStreamBulkWarning.DelayCleared);
                }

                if (stream.ArchiveDays > 0)
                {
                    patch = patch with { ArchiveDays = 0 };
                    warnings.Add(VideoStreamBulkWarning.ArchiveCleared);
                }
            }

            if (target is VideoStreamType.Redirect && HasRestreamSettings(stream))
            {
                patch = patch with
                {
                    RestreamUserAgent = string.Empty,
                    RestreamProxy = string.Empty,
                    RestreamCookies = string.Empty,
                    RestreamHeaders = string.Empty
                };

                warnings.Add(VideoStreamBulkWarning.RestreamCleared);
            }

            var outcome = await _streams.UpdateAsync(streamId, patch, cancellationToken);

            // A refused update changed nothing, so no setting was cleared and saying otherwise
            // would describe a row that did not move. An update that cleared nothing reports no
            // warnings at all rather than an empty list, so "nothing was cleared" has one shape.
            return (
                outcome,
                outcome.Action is VideoStreamAction.Updated && warnings.Count > 0 ? warnings : null);
        }

        /// <summary>Writes the chosen delay to one permanent stream.</summary>
        private async Task<(VideoStreamOutcome Outcome, IReadOnlyList<VideoStreamBulkWarning>? Warnings)>
            ChangeDelayAsync(
                Guid streamId,
                int? delayMinutes,
                CancellationToken cancellationToken)
        {
            if (delayMinutes is not { } delay)
            {
                return (Invalid(
                    streamId,
                    nameof(BulkVideoStreamOperation.DelayMinutes),
                    DelayValueRule), null);
            }

            var stream = await _streams.GetAsync(streamId, cancellationToken);

            if (stream is null)
            {
                return (NotFound(streamId), null);
            }

            if (stream.Type is not VideoStreamType.Permanent)
            {
                return (Invalid(
                    streamId,
                    nameof(BulkVideoStreamOperation.DelayMinutes),
                    DelayRule), null);
            }

            return (
                await _streams.UpdateAsync(
                    streamId,
                    new VideoStreamPatch { DelayMinutes = delay },
                    cancellationToken),
                null);
        }

        /// <summary>Writes the chosen archive length to one permanent stream.</summary>
        private async Task<(VideoStreamOutcome Outcome, IReadOnlyList<VideoStreamBulkWarning>? Warnings)>
            ChangeArchiveAsync(
                Guid streamId,
                int? archiveDays,
                CancellationToken cancellationToken)
        {
            if (archiveDays is not { } archive)
            {
                return (Invalid(
                    streamId,
                    nameof(BulkVideoStreamOperation.ArchiveDays),
                    ArchiveValueRule), null);
            }

            var stream = await _streams.GetAsync(streamId, cancellationToken);

            if (stream is null)
            {
                return (NotFound(streamId), null);
            }

            if (stream.Type is not VideoStreamType.Permanent)
            {
                return (Invalid(
                    streamId,
                    nameof(BulkVideoStreamOperation.ArchiveDays),
                    ArchiveRule), null);
            }

            return (
                await _streams.UpdateAsync(
                    streamId,
                    new VideoStreamPatch { ArchiveDays = archive },
                    cancellationToken),
                null);
        }

        /// <summary>Replaces all four restream settings of one permanent or on-demand stream.</summary>
        /// <remarks>
        /// All four are written even when the body named only one, because the operation means
        /// "the restream settings are these now" rather than "change this field". A blank value
        /// clears its setting.
        /// </remarks>
        private async Task<(VideoStreamOutcome Outcome, IReadOnlyList<VideoStreamBulkWarning>? Warnings)>
            ChangeRestreamAsync(
                Guid streamId,
                BulkVideoStreamOperation operation,
                CancellationToken cancellationToken)
        {
            var stream = await _streams.GetAsync(streamId, cancellationToken);

            if (stream is null)
            {
                return (NotFound(streamId), null);
            }

            if (stream.Type is not (VideoStreamType.Permanent or VideoStreamType.OnDemand))
            {
                return (Invalid(
                    streamId,
                    nameof(BulkVideoStreamOperation.RestreamUserAgent),
                    RestreamRule), null);
            }

            return (
                await _streams.UpdateAsync(
                    streamId,
                    new VideoStreamPatch
                    {
                        RestreamUserAgent = operation.RestreamUserAgent ?? string.Empty,
                        RestreamProxy = operation.RestreamProxy ?? string.Empty,
                        RestreamCookies = operation.RestreamCookies ?? string.Empty,
                        RestreamHeaders = operation.RestreamHeaders ?? string.Empty
                    },
                    cancellationToken),
                null);
        }

        /// <summary>
        /// The payload this operation cannot run without, or null when it has one.
        /// </summary>
        private static (string Field, string[] Sentences)? MissingPayload(
            BulkVideoStreamOperation operation)
        {
            var missing = operation.Action switch
            {
                BulkVideoStreamAction.MoveCategory when operation.CategoryId is null =>
                    (nameof(BulkVideoStreamOperation.CategoryId), CategoryRule),

                BulkVideoStreamAction.ChangeType when operation.Type is not { } type || !IsUrlKind(type) =>
                    (nameof(BulkVideoStreamOperation.Type), TypeRule),

                BulkVideoStreamAction.ChangeDelay when operation.DelayMinutes is not >= 0 =>
                    (nameof(BulkVideoStreamOperation.DelayMinutes), DelayValueRule),

                BulkVideoStreamAction.ChangeArchive when operation.ArchiveDays is not >= 0 =>
                    (nameof(BulkVideoStreamOperation.ArchiveDays), ArchiveValueRule),

                _ => ((string Field, string[] Sentences)?)null
            };

            return missing;
        }

        /// <summary>Answers every named stream with one payload refusal.</summary>
        private static BulkVideoStreamOutcome RefuseAll(
            BulkVideoStreamOperation operation,
            string field,
            string[] sentences)
            => new()
            {
                Action = operation.Action,
                Items = operation.StreamIds
                    .Select(streamId => new BulkVideoStreamItemResult
                    {
                        StreamId = streamId,
                        Action = VideoStreamAction.Invalid,
                        Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                        {
                            [field] = sentences
                        }
                    })
                    .ToList()
            };

        /// <summary>The three kinds that relay from a list of URLs rather than from anything else.</summary>
        private static bool IsUrlKind(VideoStreamType type)
            => type is VideoStreamType.Permanent
                or VideoStreamType.OnDemand
                or VideoStreamType.Redirect;

        /// <summary>Whether a row carries any restream setting at all.</summary>
        private static bool HasRestreamSettings(VideoStream stream)
            => stream.RestreamUserAgent is not null
                || stream.RestreamProxy is not null
                || stream.RestreamCookies is not null
                || stream.RestreamHeaders is not null;

        private static VideoStreamOutcome NotFound(Guid streamId)
            => new() { Action = VideoStreamAction.NotFound, StreamId = streamId };

        private static VideoStreamOutcome Invalid(Guid streamId, string field, string[] sentences)
            => new()
            {
                Action = VideoStreamAction.Invalid,
                StreamId = streamId,
                Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [field] = sentences
                }
            };
    }
}
