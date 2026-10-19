namespace Fluxy.Core.Content.Enums
{
    /// <summary>
    /// The outcome of one admin operation on a content category: which
    /// <see cref="ContentCategoryAction"/> ended it, which category it was about, and which
    /// fields were refused when the input was the problem.
    /// </summary>
    /// <remarks>
    /// <see cref="Errors"/> is keyed by the C# name of the property the service rejected, the
    /// convention every other service here follows - the transport turns those into the
    /// camelCase names a form reads, in one place.
    /// </remarks>
    public sealed record ContentCategoryOutcome
    {
        /// <summary>How the operation ended.</summary>
        public required ContentCategoryAction Action { get; init; }

        /// <summary>
        /// Category the operation was about. <see cref="Guid.Empty"/> for an outcome that
        /// never reached a row, such as a refusal made from the body alone.
        /// </summary>
        public Guid CategoryId { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

        /// <summary>
        /// How many rows still file under the category, when <see cref="Action"/> is
        /// <see cref="ContentCategoryAction.InUse"/>. Zero for every other outcome.
        /// </summary>
        /// <remarks>
        /// A count rather than a list, for the same reason the accounts page reports a
        /// <c>total</c> and not the whole table: the sentence an operator needs is "this many
        /// streams still point at it", and the screens that could do something about it are
        /// the ones that list those streams.
        /// </remarks>
        public int UsedCount { get; init; }
    }
}
