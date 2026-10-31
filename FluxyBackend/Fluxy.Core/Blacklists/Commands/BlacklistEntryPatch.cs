namespace Fluxy.Core.Blacklists.Commands
{
    /// <summary>
    /// What an administrator changes about an existing blacklist entry: the value and the note,
    /// and nothing else.
    /// </summary>
    /// <remarks>
    /// The type is immutable - moving an entry between lists would change what the enforcement
    /// matches on while the value stayed the same - and the counter and the added date have no
    /// write path. Both properties are optional, and an absent one means "leave it alone".
    /// </remarks>
    public sealed record BlacklistEntryPatch
    {
        /// <summary>New value, or null to keep the one on the row.</summary>
        public string? Value { get; init; }

        /// <summary>
        /// New note, or null to keep the one on the row. An empty string clears the note, which
        /// is the one field here with a "clear" state.
        /// </summary>
        public string? Note { get; init; }
    }
}
