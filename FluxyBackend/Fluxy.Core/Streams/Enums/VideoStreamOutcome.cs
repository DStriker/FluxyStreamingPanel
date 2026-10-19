namespace Fluxy.Core.Streams.Enums
{
    /// <summary>
    /// The outcome of one admin operation on a stream: which <see cref="VideoStreamAction"/>
    /// ended it, which stream it was about, and which fields were refused when the input was
    /// the problem.
    /// </summary>
    /// <remarks>
    /// <see cref="Errors"/> is keyed by the C# name of the property the service rejected, the
    /// convention every other service here follows - the transport turns those into the
    /// camelCase names a form reads, in one place.
    /// </remarks>
    public sealed record VideoStreamOutcome
    {
        /// <summary>How the operation ended.</summary>
        public required VideoStreamAction Action { get; init; }

        /// <summary>
        /// Stream the operation was about. <see cref="Guid.Empty"/> for an outcome that never
        /// reached a row, such as a refusal made from the body alone.
        /// </summary>
        public Guid StreamId { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
