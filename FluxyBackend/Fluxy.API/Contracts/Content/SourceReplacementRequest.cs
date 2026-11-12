namespace Fluxy.API.Contracts.Content
{
    /// <summary>
    /// What the source replacement form sends: the text to find and the text to write over it.
    /// </summary>
    /// <remarks>
    /// Both values are used exactly as they arrive - no trimming - because the operation edits
    /// addresses character for character. An absent <see cref="Replace"/> is the empty string,
    /// which is how an operator deletes every occurrence of a fragment.
    /// </remarks>
    public sealed record SourceReplacementRequest
    {
        /// <summary>Text to find in every playback address, matched ordinally.</summary>
        public string? Find { get; init; }

        /// <summary>Text written over every occurrence; null means the empty string.</summary>
        public string? Replace { get; init; }
    }
}
