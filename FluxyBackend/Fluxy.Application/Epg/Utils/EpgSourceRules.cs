using Fluxy.Core.Epg.Commands;
using Fluxy.Core.Epg.Utils;

namespace Fluxy.Application.Epg.Utils
{
    /// <summary>
    /// The rules and sentences every write to a programme guide source shares.
    /// </summary>
    /// <remarks>
    /// A create and a patch reach the same row through different requests, so the rules that
    /// are a fact about the row rather than about the request live here once. What is not
    /// here is what a request alone decides: a create resolves its default retention, a patch
    /// resolves "absent means keep", and both stay with the service.
    /// </remarks>
    public static class EpgSourceRules
    {
        /// <summary>The sentence a name that is blank or too long gets.</summary>
        public static readonly string[] NameRule =
        [
            "The name must be between 1 and " + EpgSourceLimits.NameMaxLength + " characters."
        ];

        /// <summary>The sentence an address that is not an absolute http(s) one gets.</summary>
        public static readonly string[] UrlRule =
        [
            "The address must be an absolute http or https URL of at most " +
                EpgSourceLimits.UrlMaxLength + " characters."
        ];

        /// <summary>The sentence a retention outside its range gets.</summary>
        public static readonly string[] DaysKeepRule =
        [
            "The number of days to keep must be between " + EpgSourceLimits.DaysKeepMin +
                " and " + EpgSourceLimits.DaysKeepMax + "."
        ];

        /// <summary>The unique index making a source's name unique across the installation.</summary>
        public const string NameIndexName = "IX_epg_sources_name";

        /// <summary>Whether a name is non-blank and short enough to store.</summary>
        /// <param name="name">Name to judge.</param>
        public static bool IsUsableName(string name)
            => name.Trim().Length is >= 1 and <= EpgSourceLimits.NameMaxLength;

        /// <summary>
        /// Whether an address is one this installation can fetch a guide from: an absolute
        /// http or https URL that fits the column.
        /// </summary>
        /// <param name="url">Address to judge.</param>
        public static bool IsUsableUrl(string url)
        {
            var trimmed = url.Trim();

            return trimmed.Length is >= 1 and <= EpgSourceLimits.UrlMaxLength
                && Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>Whether a retention is inside its range.</summary>
        /// <param name="daysKeep">Days to judge.</param>
        public static bool IsUsableDaysKeep(int daysKeep)
            => daysKeep is >= EpgSourceLimits.DaysKeepMin and <= EpgSourceLimits.DaysKeepMax;

        /// <summary>
        /// Checks everything about a source that can be judged without asking the database.
        /// </summary>
        /// <remarks>
        /// The keys are the C# property names of <see cref="NewEpgSource"/>, which are the
        /// same names the patch uses for these fields. The transport normalizes them to
        /// camelCase for the client.
        /// </remarks>
        /// <param name="name">Display name.</param>
        /// <param name="url">Address the guide is fetched from.</param>
        /// <param name="daysKeep">Days of programmes to keep.</param>
        public static Dictionary<string, string[]> Validate(string name, string url, int daysKeep)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (!IsUsableName(name))
            {
                errors[nameof(NewEpgSource.Name)] = NameRule;
            }

            if (!IsUsableUrl(url))
            {
                errors[nameof(NewEpgSource.Url)] = UrlRule;
            }

            if (!IsUsableDaysKeep(daysKeep))
            {
                errors[nameof(NewEpgSource.DaysKeep)] = DaysKeepRule;
            }

            return errors;
        }
    }
}
