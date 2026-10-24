using System.Globalization;

namespace Fluxy.Core.GeoIp.Utils
{
    /// <summary>
    /// The ISO 3166-1 alpha-2 codes this installation knows, and the canonical form a country
    /// is stored in.
    /// </summary>
    /// <remarks>
    /// A country is a fact about the world rather than about an account or a video, and two
    /// domains now record one: the login guard's allow list and an on-demand item's country.
    /// Building the set here once keeps the two from disagreeing about whether <c>DE</c> is a
    /// country, and deriving it from the runtime's own regions means the list cannot drift from
    /// what the platform recognizes.
    /// </remarks>
    public static class CountryCodes
    {
        /// <summary>Every code the runtime knows, upper case.</summary>
        private static readonly HashSet<string> Codes = Build();

        /// <summary>
        /// Canonical form of a country: upper case ISO 3166-1 alpha-2. Null when the value is
        /// not a code this installation knows.
        /// </summary>
        /// <param name="country">Value as typed, possibly null or lower case.</param>
        public static string? Normalize(string? country)
        {
            var trimmed = country?.Trim().ToUpperInvariant();

            return trimmed is not null && Codes.Contains(trimmed) ? trimmed : null;
        }

        private static HashSet<string> Build()
        {
            var codes = new HashSet<string>(StringComparer.Ordinal);

            foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
            {
                try
                {
                    codes.Add(new RegionInfo(culture.Name).TwoLetterISORegionName);
                }
                catch (ArgumentException)
                {
                    // A culture without a region is not a country and contributes nothing.
                }
            }

            return codes;
        }
    }
}
