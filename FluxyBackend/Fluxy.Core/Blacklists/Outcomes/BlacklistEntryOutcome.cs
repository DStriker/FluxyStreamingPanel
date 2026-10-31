using Fluxy.Core.Blacklists.Enums;

namespace Fluxy.Core.Blacklists.Outcomes
{
    /// <summary>
    /// The outcome of one admin operation on a blacklist entry: which
    /// <see cref="BlacklistEntryAction"/> ended it, which entry it was about, and what was
    /// refused when the input was the problem.
    /// </summary>
    /// <remarks>
    /// <see cref="Errors"/> is keyed by the C# name of the property the service rejected, the
    /// convention every other service here follows; the transport turns those into the
    /// camelCase names a form reads.
    ///
    /// A <see cref="BlacklistEntryAction.Cleared"/> outcome is about a whole list rather than
    /// one row, so it carries <see cref="Guid.Empty"/> in <see cref="EntryId"/>: there is no
    /// single entry the answer could name.
    /// </remarks>
    public sealed record BlacklistEntryOutcome
    {
        /// <summary>How the operation ended.</summary>
        public required BlacklistEntryAction Action { get; init; }

        /// <summary>
        /// The entry the operation was about, or <see cref="Guid.Empty"/> when the operation
        /// addressed a whole list.
        /// </summary>
        public required Guid EntryId { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
