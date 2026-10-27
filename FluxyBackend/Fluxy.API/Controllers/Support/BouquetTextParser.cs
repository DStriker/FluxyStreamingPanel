using System.Globalization;

namespace Fluxy.API.Controllers.Support
{
    /// <summary>
    /// Turns the <c>bouquetIds</c> text list of a content form into the identifiers it names,
    /// for every endpoint that reads one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shared because four content controllers carry the same optional field, and the shape it
    /// has to keep is one sentence: <b>absent keeps, empty clears, a list replaces</b>. The three
    /// cases have to stay apart all the way from the body to the service, and the difference is
    /// carried by <c>null</c> against an empty list rather than by a second flag.
    /// </para>
    /// <para>
    /// Duplicates are collapsed here rather than refused, unlike the reorder lists: membership is
    /// a set, and two entries naming one bouquet are one membership, not two claims about a
    /// position. The <i>order</i> of the entries means nothing at all - the service appends a
    /// newly joined bouquet at the end of that bouquet's list and leaves every other position
    /// where it was.
    /// </para>
    /// </remarks>
    internal static class BouquetTextParser
    {
        /// <summary>Sentence an entry that is not a uuid gets.</summary>
        public const string Hint = "Every bouquet must be the uuid of a bouquet.";

        /// <summary>
        /// Turns the text list a body sent into the identifiers it names, or nothing at all once
        /// one of them is wrong.
        /// </summary>
        /// <param name="values">Text list a body sent, or null when the field was absent.</param>
        /// <param name="propertyName">C# name of the property, which the errors are keyed by.</param>
        /// <param name="errors">Errors of the request being parsed; a broken entry adds to them.</param>
        /// <returns>
        /// The identifiers, or <c>null</c> when the field was absent or an entry was refused -
        /// the caller checks <paramref name="errors"/> before trusting the answer.
        /// </returns>
        public static IReadOnlyList<Guid>? ParseOrNull(
            IReadOnlyList<string>? values,
            string propertyName,
            Dictionary<string, string[]> errors)
        {
            if (values is null)
            {
                return null;
            }

            var parsed = new List<Guid>(values.Count);
            var seen = new HashSet<Guid>();

            foreach (var text in values)
            {
                if (!Guid.TryParse(text, out var id))
                {
                    errors[propertyName] = [Hint];
                    return null;
                }

                if (seen.Add(id))
                {
                    parsed.Add(id);
                }
            }

            return parsed;
        }

        /// <summary>Renders an identifier the way every response does, as a lower-case uuid.</summary>
        /// <param name="id">Identifier to render.</param>
        public static string Text(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);
    }
}
