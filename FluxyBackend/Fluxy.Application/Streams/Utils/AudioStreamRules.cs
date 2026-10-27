using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Utils;

namespace Fluxy.Application.Streams.Utils
{
    /// <summary>
    /// The rules and sentences every write to an audio stream shares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A single create, a patch and a playlist import reach the same row through different
    /// requests, so the rules that are a fact about the row rather than about the request live
    /// here once. The alternative - a copy in each service - is how a form and the endpoint
    /// behind it start disagreeing about whether a name is usable, and the disagreement is
    /// invisible until an operator finds it.
    /// </para>
    /// <para>
    /// What is <i>not</i> here is what a request alone decides: a create has one name, an
    /// import has one name per entry, and a patch has "absent means keep". Those stay with the
    /// service that owns the shape of its own command.
    /// </para>
    /// </remarks>
    public static class AudioStreamRules
    {
        /// <summary>The sentence a name that is blank or too long gets.</summary>
        public static readonly string[] NameRule =
        [
            "The name must be between 1 and "
                + AudioStreamLimits.NameMaxLength
                + " characters."
        ];

        /// <summary>The sentence a type that names no member of the enum gets.</summary>
        public static readonly string[] TypeRule =
            ["The stream type must be one of: ondemand, redirect."];

        /// <summary>The sentence a type an import may not create gets.</summary>
        public static readonly string[] ImportTypeRule =
            ["The stream type of an imported playlist must be one of: ondemand, redirect."];

        /// <summary>The sentence a status that names no member of the enum gets.</summary>
        public static readonly string[] StatusRule =
            ["The status must be either active or disabled."];

        /// <summary>The sentence a category that names no row of the right kind gets.</summary>
        public static readonly string[] CategoryRule =
        [
            "The category must be an existing category of audio streams."
        ];

        /// <summary>The sentence a bouquet that names no row gets.</summary>
        public static readonly string[] BouquetsRule =
        [
            "Every bouquet must be an existing bouquet."
        ];

        /// <summary>The sentence a user agent longer than its column gets.</summary>
        public static readonly string[] RestreamUserAgentRule =
        [
            "The user agent must be "
                + AudioStreamLimits.RestreamUserAgentMaxLength
                + " characters or fewer."
        ];

        /// <summary>The sentence a proxy address longer than its column gets.</summary>
        public static readonly string[] RestreamProxyRule =
        [
            "The proxy address must be "
                + AudioStreamLimits.RestreamProxyMaxLength
                + " characters or fewer."
        ];

        /// <summary>The sentence a source list that holds nothing usable gets.</summary>
        public static readonly string[] SourcesRule =
        [
            "Add at least one source address, and no more than "
                + AudioStreamLimits.SourceUrlMaxLength
                + " characters each."
        ];

        /// <summary>The unique index making an audio stream's name unique within its category.</summary>
        /// <remarks>
        /// Shared so a create, an update and an import compare against the same index name: the
        /// other unique index is on the source position, and a violation of it is a bug in the
        /// list writer rather than a taken name.
        /// </remarks>
        public const string NameIndexName = "IX_audio_streams_category_id_name";

        /// <summary>Whether a name is non-blank and short enough to store.</summary>
        /// <param name="name">Name to judge.</param>
        public static bool IsUsableName(string name)
            => name.Trim().Length is >= 1 and <= AudioStreamLimits.NameMaxLength;

        /// <summary>
        /// Whether a list holds at least one entry that is not blank and none that is too long.
        /// </summary>
        /// <param name="entries">Entries to judge.</param>
        /// <param name="maxLength">Longest entry the column stores.</param>
        public static bool HasUsableEntries(IReadOnlyList<string> entries, int maxLength)
            => entries.Count > 0
                && entries.All(entry => !string.IsNullOrWhiteSpace(entry) && entry.Trim().Length <= maxLength);

        /// <summary>
        /// Checks the fields every create, patch and import has in common.
        /// </summary>
        /// <remarks>
        /// The keys are the C# property names of <see cref="Fluxy.Core.Streams.Commands.NewAudioStream"/>,
        /// which are the same names the patch and the import use for these fields. The
        /// transport normalizes them to camelCase for the client.
        /// </remarks>
        /// <param name="type">Kind of stream being written.</param>
        /// <param name="status">Whether the stream is active.</param>
        /// <param name="categoryId">Category the stream is filed under.</param>
        /// <param name="restreamUserAgent">User agent of the relayed requests.</param>
        /// <param name="restreamProxy">Proxy the relay goes out through.</param>
        /// <returns>One entry per rejected field, empty when all of them are usable.</returns>
        public static Dictionary<string, string[]> ValidateCommon(
            AudioStreamType type,
            AudioStreamStatus status,
            Guid categoryId,
            string? restreamUserAgent,
            string? restreamProxy)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (!Enum.IsDefined(type))
            {
                errors[nameof(Fluxy.Core.Streams.Commands.NewAudioStream.Type)] = TypeRule;
            }

            if (!Enum.IsDefined(status))
            {
                errors[nameof(Fluxy.Core.Streams.Commands.NewAudioStream.Status)] = StatusRule;
            }

            if (categoryId == Guid.Empty)
            {
                errors[nameof(Fluxy.Core.Streams.Commands.NewAudioStream.CategoryId)] = CategoryRule;
            }

            if (!string.IsNullOrWhiteSpace(restreamUserAgent)
                && restreamUserAgent.Trim().Length > AudioStreamLimits.RestreamUserAgentMaxLength)
            {
                errors[nameof(Fluxy.Core.Streams.Commands.NewAudioStream.RestreamUserAgent)] =
                    RestreamUserAgentRule;
            }

            if (!string.IsNullOrWhiteSpace(restreamProxy)
                && restreamProxy.Trim().Length > AudioStreamLimits.RestreamProxyMaxLength)
            {
                errors[nameof(Fluxy.Core.Streams.Commands.NewAudioStream.RestreamProxy)] =
                    RestreamProxyRule;
            }

            return errors;
        }

        /// <summary>Turns an empty string into null, so a blank field is stored as "none".</summary>
        /// <param name="value">Text a request sent, or null.</param>
        public static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
