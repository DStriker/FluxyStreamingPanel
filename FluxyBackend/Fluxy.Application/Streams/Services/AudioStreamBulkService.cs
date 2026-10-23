using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Interfaces;
using Fluxy.Core.Streams.Outcomes;

namespace Fluxy.Application.Streams.Services
{
    /// <summary>
    /// Applies one operation to a set of audio streams, one stream at a time.
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
    /// The menu is shorter than the video bulk endpoint's because an audio stream has fewer
    /// independent facts: no delay, no archive, no payload a kind can switch off. Both kinds
    /// carry the same sources and the same restream settings, so a type change clears nothing
    /// and reports no warnings.
    /// </para>
    /// </remarks>
    public sealed class AudioStreamBulkService : IAudioStreamBulkService
    {
        /// <summary>The sentence a move without a destination gets.</summary>
        private static readonly string[] CategoryRule =
            ["A bulk move needs the category to move the streams to."];

        /// <summary>The sentence a type change without an allowed destination gets.</summary>
        private static readonly string[] TypeRule =
            ["A bulk type change must choose on-demand or redirect."];

        private readonly IAudioStreamService _streams;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioStreamBulkService"/> class.
        /// </summary>
        /// <param name="streams">Service holding the single-stream operations.</param>
        public AudioStreamBulkService(IAudioStreamService streams)
        {
            _streams = streams;
        }

        /// <inheritdoc />
        public async Task<BulkAudioStreamOutcome> BulkAsync(
            BulkAudioStreamOperation operation,
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

            var items = new List<BulkAudioStreamItemResult>(operation.StreamIds.Count);

            foreach (var streamId in operation.StreamIds)
            {
                var outcome = operation.Action switch
                {
                    BulkAudioStreamAction.Start =>
                        await _streams.StartAsync(streamId, cancellationToken),

                    BulkAudioStreamAction.Stop =>
                        await _streams.StopAsync(streamId, cancellationToken),

                    BulkAudioStreamAction.Restart =>
                        await _streams.RestartAsync(streamId, cancellationToken),

                    BulkAudioStreamAction.Delete =>
                        await _streams.DeleteAsync(streamId, cancellationToken),

                    BulkAudioStreamAction.MoveCategory => await _streams.UpdateAsync(
                        streamId,
                        new AudioStreamPatch { CategoryId = operation.CategoryId },
                        cancellationToken),

                    BulkAudioStreamAction.ChangeType => await ChangeTypeAsync(
                        streamId,
                        operation.Type,
                        cancellationToken),

                    BulkAudioStreamAction.ChangeRestream => await ChangeRestreamAsync(
                        streamId,
                        operation,
                        cancellationToken),

                    // Unreachable: the enum holds no other member and the transport refuses a
                    // name that maps to none. Thrown rather than skipped, because an entry
                    // missing from the answer is a stream the client believes it acted on and
                    // the server never mentioned.
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(operation),
                        operation.Action,
                        "Unknown bulk audio stream action.")
                };

                items.Add(new BulkAudioStreamItemResult
                {
                    StreamId = streamId,
                    Action = outcome.Action,
                    Errors = outcome.Errors
                });
            }

            return new BulkAudioStreamOutcome { Action = operation.Action, Items = items };
        }

        /// <summary>Changes the kind of one stream.</summary>
        /// <remarks>
        /// Both kinds carry the same sources and restream settings, so this is the ordinary
        /// update with one field set - there is nothing to clear and therefore no warning to
        /// report.
        /// </remarks>
        private async Task<AudioStreamOutcome> ChangeTypeAsync(
            Guid streamId,
            AudioStreamType? type,
            CancellationToken cancellationToken)
        {
            if (type is not { } target || !Enum.IsDefined(target))
            {
                return Invalid(streamId, nameof(BulkAudioStreamOperation.Type), TypeRule);
            }

            return await _streams.UpdateAsync(
                streamId,
                new AudioStreamPatch { Type = target },
                cancellationToken);
        }

        /// <summary>Replaces all four restream settings of one stream.</summary>
        /// <remarks>
        /// All four are written even when the body named only one, because the operation means
        /// "the restream settings are these now" rather than "change this field". A blank value
        /// clears its setting. Both kinds accept the settings, so no row is refused for its
        /// type.
        /// </remarks>
        private async Task<AudioStreamOutcome> ChangeRestreamAsync(
            Guid streamId,
            BulkAudioStreamOperation operation,
            CancellationToken cancellationToken)
            => await _streams.UpdateAsync(
                streamId,
                new AudioStreamPatch
                {
                    RestreamUserAgent = operation.RestreamUserAgent ?? string.Empty,
                    RestreamProxy = operation.RestreamProxy ?? string.Empty,
                    RestreamCookies = operation.RestreamCookies ?? string.Empty,
                    RestreamHeaders = operation.RestreamHeaders ?? string.Empty
                },
                cancellationToken);

        /// <summary>
        /// The payload this operation cannot run without, or null when it has one.
        /// </summary>
        private static (string Field, string[] Sentences)? MissingPayload(
            BulkAudioStreamOperation operation)
        {
            var missing = operation.Action switch
            {
                BulkAudioStreamAction.MoveCategory when operation.CategoryId is null =>
                    (nameof(BulkAudioStreamOperation.CategoryId), CategoryRule),

                BulkAudioStreamAction.ChangeType when operation.Type is not { } type || !Enum.IsDefined(type) =>
                    (nameof(BulkAudioStreamOperation.Type), TypeRule),

                _ => ((string Field, string[] Sentences)?)null
            };

            return missing;
        }

        /// <summary>Answers every named stream with one payload refusal.</summary>
        private static BulkAudioStreamOutcome RefuseAll(
            BulkAudioStreamOperation operation,
            string field,
            string[] sentences)
            => new()
            {
                Action = operation.Action,
                Items = operation.StreamIds
                    .Select(streamId => new BulkAudioStreamItemResult
                    {
                        StreamId = streamId,
                        Action = AudioStreamAction.Invalid,
                        Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                        {
                            [field] = sentences
                        }
                    })
                    .ToList()
            };

        private static AudioStreamOutcome Invalid(Guid streamId, string field, string[] sentences)
            => new()
            {
                Action = AudioStreamAction.Invalid,
                StreamId = streamId,
                Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [field] = sentences
                }
            };
    }
}
