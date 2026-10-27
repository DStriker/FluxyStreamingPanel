namespace Fluxy.Core.Content.Enums
{
    /// <summary>
    /// The outcome of one admin operation on a bouquet: which <see cref="BouquetAction"/> ended
    /// it, which bouquet it was about, and which fields were refused when the input was the
    /// problem.
    /// </summary>
    /// <remarks>
    /// <see cref="Errors"/> is keyed by the C# name of the property the service rejected, the
    /// convention every other service here follows - the transport turns those into the
    /// camelCase names a form reads, in one place.
    /// </remarks>
    public sealed record BouquetOutcome
    {
        /// <summary>How the operation ended.</summary>
        public required BouquetAction Action { get; init; }

        /// <summary>
        /// Bouquet the operation was about. <see cref="Guid.Empty"/> for an outcome that never
        /// reached a row, such as a refusal made from the body alone.
        /// </summary>
        public Guid BouquetId { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

        /// <summary>
        /// How many content items the bouquet still holds, when <see cref="Action"/> is
        /// <see cref="BouquetAction.NotEmpty"/>. Zero for every other outcome.
        /// </summary>
        /// <remarks>
        /// **Every kind**, for the reason <c>ContentCategoryOutcome.UsedCount</c> gives: the
        /// question is "may this bouquet go?", and a bouquet holding one series is just as
        /// full as one holding a thousand streams.
        /// </remarks>
        public int ItemCount { get; init; }
    }
}
