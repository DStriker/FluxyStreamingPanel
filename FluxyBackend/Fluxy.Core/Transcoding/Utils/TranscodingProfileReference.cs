namespace Fluxy.Core.Transcoding.Utils
{
    /// <summary>
    /// Resolves the three-state reference a content PATCH sends for its transcoding profile.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference travels as text because a nullable uuid cannot tell "leave it" from
    /// "clear it", and those are two different requests. The convention is the one the streams
    /// already follow for a series and a guide source: an absent or <c>null</c> value keeps
    /// what the row holds, an empty string clears it, and anything else must be the uuid of the
    /// profile it names.
    /// </para>
    /// <para>
    /// This lives in Core rather than in one service because four aggregates point at a profile -
    /// a video stream, an audio stream, an on-demand video and a series - and the rule has to
    /// mean the same thing in all four.
    /// </para>
    /// </remarks>
    public static class TranscodingProfileReference
    {
        /// <summary>The sentence a value that is not a uuid gets.</summary>
        public static readonly string[] IdentifierRule =
        [
            "That value must be the uuid of the transcoding profile it names."
        ];

        /// <summary>The sentence a value that names no existing profile gets.</summary>
        public static readonly string[] UnknownRule =
        [
            "There is no transcoding profile with that identifier."
        ];

        /// <summary>
        /// Resolves one reference of a change: absent keeps, empty clears, anything else is
        /// parsed.
        /// </summary>
        /// <param name="sent">Text the patch carried, possibly null.</param>
        /// <param name="stored">Value the row currently holds.</param>
        /// <param name="value">The value the row must hold after the change.</param>
        /// <param name="error">The sentence a value that is not a uuid gets, or null.</param>
        /// <returns>False when the text was present and named no uuid.</returns>
        public static bool TryMerge(
            string? sent,
            Guid? stored,
            out Guid? value,
            out string[]? error)
        {
            error = null;

            if (sent is null)
            {
                value = stored;
                return true;
            }

            if (string.IsNullOrWhiteSpace(sent))
            {
                value = null;
                return true;
            }

            if (!Guid.TryParse(sent.Trim(), out var parsed))
            {
                value = null;
                error = IdentifierRule;
                return false;
            }

            value = parsed;
            return true;
        }
    }
}
