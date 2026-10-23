using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Utils;

namespace Fluxy.Application.Streams.Utils
{
    /// <summary>
    /// The rules and sentences every write to a stream shares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A single create and a playlist import reach the same row through different requests, so
    /// the rules that are a fact about the row rather than about the request live here once.
    /// The alternative - a copy in each service - is how a form and the endpoint behind it start
    /// disagreeing about whether an archive is allowed, and the disagreement is invisible until
    /// an operator finds it.
    /// </para>
    /// <para>
    /// What is <i>not</i> here is what a request alone decides: a create has one name, an import
    /// has one name per entry, and a patch has "absent means keep". Those stay with the service
    /// that owns the shape of its own command.
    /// </para>
    /// </remarks>
    public static class VideoStreamRules
    {
        /// <summary>The sentence a name that is blank or too long gets.</summary>
        public static readonly string[] NameRule =
        [
            "The name must be between 1 and "
                + VideoStreamLimits.NameMaxLength
                + " characters."
        ];

        /// <summary>The sentence a type that names no member of the enum gets.</summary>
        public static readonly string[] TypeRule =
        [
            "The stream type must be one of: permanent, ondemand, redirect, " +
                "onlineseries, vodplaylist, fileplaylist."
        ];

        /// <summary>The sentence a type an import may not create gets.</summary>
        public static readonly string[] ImportTypeRule =
        [
            "The stream type of an imported playlist must be one of: permanent, " +
                "ondemand, redirect."
        ];

        /// <summary>The sentence a status that names no member of the enum gets.</summary>
        public static readonly string[] StatusRule =
            ["The status must be either active or disabled."];

        /// <summary>The sentence a category that names no row of the right kind gets.</summary>
        public static readonly string[] CategoryRule =
        [
            "The category must be an existing category of video streams."
        ];

        /// <summary>The sentence a delay that is not a whole number of minutes gets.</summary>
        public static readonly string[] DelayRule = ["The delay must be zero or more minutes."];

        /// <summary>The sentence an archive length that is not a whole number of days gets.</summary>
        public static readonly string[] ArchiveDaysRule =
            ["The archive length must be zero or more days."];

        /// <summary>The sentence an archive that only a permanent stream may keep gets.</summary>
        public static readonly string[] PermanentArchiveRule =
        [
            "Only a permanent stream may keep an archive. Set the archive length to zero, " +
                "or make the stream permanent."
        ];

        /// <summary>The sentence a user agent longer than its column gets.</summary>
        public static readonly string[] RestreamUserAgentRule =
        [
            "The user agent must be "
                + VideoStreamLimits.RestreamUserAgentMaxLength
                + " characters or fewer."
        ];

        /// <summary>The sentence a proxy address longer than its column gets.</summary>
        public static readonly string[] RestreamProxyRule =
        [
            "The proxy address must be "
                + VideoStreamLimits.RestreamProxyMaxLength
                + " characters or fewer."
        ];

        /// <summary>The sentence a URL list that holds nothing usable gets.</summary>
        public static readonly string[] SourcesRule =
        [
            "Add at least one source address, and no more than "
                + VideoStreamLimits.SourceUrlMaxLength
                + " characters each."
        ];

        /// <summary>The unique index making a stream's name unique within its category.</summary>
        /// <remarks>
        /// Shared so a create and an import compare against the same index name: the other
        /// unique indexes are on the two position columns, and a violation of either is a bug
        /// in a list writer rather than a taken name.
        /// </remarks>
        public const string NameIndexName = "IX_video_streams_category_id_name";

        /// <summary>Whether the type relays from a list of URLs rather than from anything else.</summary>
        /// <param name="type">Type to judge.</param>
        public static bool IsUrlKind(VideoStreamType type)
            => type is VideoStreamType.Permanent
                or VideoStreamType.OnDemand
                or VideoStreamType.Redirect;

        /// <summary>Whether a name is non-blank and short enough to store.</summary>
        /// <param name="name">Name to judge.</param>
        public static bool IsUsableName(string name)
            => name.Trim().Length is >= 1 and <= VideoStreamLimits.NameMaxLength;

        /// <summary>
        /// Whether a list holds at least one entry that is not blank and none that is too long.
        /// </summary>
        /// <remarks>
        /// Both halves matter. A list of blanks is a list nobody filled in, and a single entry
        /// over the column's length is a row the database would refuse - so both are the
        /// caller's to hear about rather than the provider's exception to surface later.
        /// </remarks>
        public static bool HasUsableEntries(IReadOnlyList<string> entries, int maxLength)
            => entries.Count > 0
                && entries.All(entry => !string.IsNullOrWhiteSpace(entry) && entry.Trim().Length <= maxLength);

        /// <summary>
        /// Checks the fields every create, patch and import has in common.
        /// </summary>
        /// <remarks>
        /// The keys are the C# property names of <see cref="NewVideoStream"/>, which are the
        /// same names the patch and the import use for these fields. The transport normalizes
        /// them to camelCase for the client.
        /// </remarks>
        /// <param name="type">Kind of stream being written.</param>
        /// <param name="status">Whether the stream is active.</param>
        /// <param name="categoryId">Category the stream is filed under.</param>
        /// <param name="delayMinutes">How far behind live the relay runs.</param>
        /// <param name="archiveDays">How many days of archive the stream keeps.</param>
        /// <param name="restreamUserAgent">User agent of the relayed requests.</param>
        /// <param name="restreamProxy">Proxy the relay goes out through.</param>
        /// <returns>One entry per rejected field, empty when all of them are usable.</returns>
        public static Dictionary<string, string[]> ValidateCommon(
            VideoStreamType type,
            VideoStreamStatus status,
            Guid categoryId,
            int delayMinutes,
            int archiveDays,
            string? restreamUserAgent,
            string? restreamProxy)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            // A number in the column outside the members would be a kind every reader has to
            // guess about - refused rather than coerced.
            if (!Enum.IsDefined(type))
            {
                errors[nameof(NewVideoStream.Type)] = TypeRule;
            }

            // The same rule for the state: a number outside the members would be a status
            // every reader has to guess about, so it is refused rather than coerced.
            if (!Enum.IsDefined(status))
            {
                errors[nameof(NewVideoStream.Status)] = StatusRule;
            }

            if (categoryId == Guid.Empty)
            {
                errors[nameof(NewVideoStream.CategoryId)] = CategoryRule;
            }

            if (delayMinutes < 0)
            {
                errors[nameof(NewVideoStream.DelayMinutes)] = DelayRule;
            }

            if (archiveDays < 0)
            {
                errors[nameof(NewVideoStream.ArchiveDays)] = ArchiveDaysRule;
            }
            else if (archiveDays > 0 && type is not VideoStreamType.Permanent)
            {
                // Not cleared silently, unlike the fields the type does not own: an archive is
                // a number somebody chose, and storing zero instead of it would be the server
                // disagreeing with an answer it was given rather than ignoring one it could
                // not use.
                errors[nameof(NewVideoStream.ArchiveDays)] = PermanentArchiveRule;
            }

            if (!string.IsNullOrWhiteSpace(restreamUserAgent)
                && restreamUserAgent.Trim().Length > VideoStreamLimits.RestreamUserAgentMaxLength)
            {
                errors[nameof(NewVideoStream.RestreamUserAgent)] = RestreamUserAgentRule;
            }

            if (!string.IsNullOrWhiteSpace(restreamProxy)
                && restreamProxy.Trim().Length > VideoStreamLimits.RestreamProxyMaxLength)
            {
                errors[nameof(NewVideoStream.RestreamProxy)] = RestreamProxyRule;
            }

            return errors;
        }

        /// <summary>Turns an empty string into null, so a blank field is stored as "none".</summary>
        /// <param name="value">Text a request sent, or null.</param>
        public static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
