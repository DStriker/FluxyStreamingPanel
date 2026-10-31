namespace Fluxy.Core.Blacklists.Commands
{
    /// <summary>
    /// The values a create or an update writes to a blacklist entry row.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Models.BlacklistEntry"/> because a write does not name what only
    /// the enforcement owns - the blocked-attempts counter and the added date - and because the
    /// service has already trimmed every value by the time a row is written.
    /// </remarks>
    public sealed record BlacklistEntryWrite
    {
        /// <summary>The value that is refused.</summary>
        public required string Value { get; init; }

        /// <summary>Free-text note, or null when there is none.</summary>
        public string? Note { get; init; }
    }
}
