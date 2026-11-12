namespace Fluxy.Core.Content.Commands
{
    /// <summary>
    /// The two strings an operator supplies to replace source addresses across every playback
    /// list at once.
    /// </summary>
    /// <remarks>
    /// The search is a plain ordinal substring, the replacement is written in every place it
    /// occurs, and nothing is trimmed: the case an operator fixes a host or an authorization
    /// fragment is the case where the characters are the whole point.
    /// </remarks>
    public sealed record ReplaceSourcesOperation
    {
        /// <summary>Text to find in every playback source, matched ordinally.</summary>
        public required string? Find { get; init; }

        /// <summary>Text written over every occurrence, or null for the empty string.</summary>
        public string? Replace { get; init; }
    }
}
