namespace Fluxy.API.Contracts
{
    /// <summary>
    /// The answer to a bulk operation: what the whole run was called, how much of it landed,
    /// and how each row ended.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Always a 200, because a well formed bulk request is not a partial failure.</b> What
    /// happened to each row is stated in <see cref="Items"/> instead of being folded into one
    /// status: a status can only describe the request as a whole, and the request as a whole
    /// was carried out - every identifier it named was answered. Folding it into a 409 would
    /// make "nine accounts blocked, one of them was your own" indistinguishable from "nothing
    /// happened", and a client would be unable to redraw the list without guessing.
    /// </para>
    /// <para>
    /// The two counts are derived rather than sent by the service, because what counts as
    /// success is a fact about the status a row's outcome maps to - and the mapping lives with
    /// the other response tables, not in a rule.
    /// </para>
    /// </remarks>
    public sealed class BulkOperationResponse
    {
        /// <summary>Machine readable outcome of the run as a whole.</summary>
        public required string Code { get; init; }

        /// <summary>English fallback sentence naming how much of it landed.</summary>
        public required string Message { get; init; }

        /// <summary>How many rows the operation actually changed.</summary>
        public required int Succeeded { get; init; }

        /// <summary>How many rows were refused, and why - each reason in <see cref="Items"/>.</summary>
        public required int Failed { get; init; }

        /// <summary>One entry per identifier the request named, in the order it was named.</summary>
        public required IReadOnlyList<BulkItemResponse> Items { get; init; }
    }

    /// <summary>How one row inside a bulk operation ended.</summary>
    /// <remarks>
    /// The <c>code</c> is the very code the single-row endpoint answers with, so a client that
    /// already knows how to branch on <c>cannot_block_self</c> from one row does not need a
    /// second vocabulary for the same fact inside a bulk run. <see cref="Ok"/> is what turns
    /// that status into a yes-or-no - it is here rather than in the message so a client never
    /// has to parse English to learn whether anything happened.
    /// </remarks>
    public sealed class BulkItemResponse
    {
        /// <summary>The row this entry is about, as a uuid string.</summary>
        public required string Id { get; init; }

        /// <summary>True when the operation changed that row, or the state it asked for held.</summary>
        public required bool Ok { get; init; }

        /// <summary>Machine readable outcome for this row alone.</summary>
        public required string Code { get; init; }

        /// <summary>English fallback for this row alone.</summary>
        public required string Message { get; init; }

        /// <summary>
        /// Rejected fields, keyed by the camelCase name of the property, when the input was
        /// the problem. Null whenever nothing was rejected.
        /// </summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
