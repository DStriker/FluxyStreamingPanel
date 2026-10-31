namespace Fluxy.API.Contracts.Blacklists
{
    /// <summary>
    /// What the add-entry dialog sends to create a blacklist entry.
    /// </summary>
    /// <remarks>
    /// No data annotations on purpose: framework validation runs before the action, so a body
    /// that failed one would never reach the antiforgery check. Every field is checked in the
    /// service after the CSRF pair has been accepted - and the list itself is parsed by the
    /// controller, because a value that names no list is a fact about this request rather than
    /// about a row.
    /// </remarks>
    public sealed record CreateBlacklistEntryRequest
    {
        /// <summary>Which of the three lists the entry belongs to: <c>ip</c>, <c>isp</c> or <c>user-agent</c>.</summary>
        public string? Type { get; init; }

        /// <summary>The value that is refused.</summary>
        public string? Value { get; init; }

        /// <summary>Free-text note, or null when there is none.</summary>
        public string? Note { get; init; }
    }
}
