namespace Fluxy.Core.Settings.Utils
{
    /// <summary>
    /// The set-top-box models a MAG client may be allowed to connect as, in one fixed order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The catalogue is the installation's vocabulary for a device model: the operator's lists
    /// are stored as canonical spellings, and a model that names no entry is refused rather than
    /// stored as typed. Comparison is case-insensitive because the spelling is a name a person
    /// reads, not an identifier a protocol defines.
    /// </para>
    /// <para>
    /// The order is the order the operator's form offers, so the two arrays a fresh installation
    /// starts with - every model, and the three that may record - are in the same order the
    /// catalogue states.
    /// </para>
    /// </remarks>
    public static class StbTypeCatalog
    {
        /// <summary>Every model the installation knows, in the form's own order.</summary>
        public static IReadOnlyList<string> All { get; } =
        [
            "Aura HD",
            "Aura HD2",
            "Aura HD3",
            "Aura HD4",
            "Aura HD5",
            "Aura HD6",
            "Aura HD7",
            "Aura HD8",
            "Aura HD9",
            "MAG200",
            "MAG245",
            "MAG245D",
            "MAG250",
            "MAG254",
            "MAG255",
            "MAG256",
            "MAG257",
            "MAG260",
            "MAG270",
            "MAG275",
            "MAG322",
            "MAG323",
            "MAG324",
            "MAG325",
            "MAG349",
            "MAG350",
            "MAG351",
            "MAG352",
            "MAG420",
            "MAG520",
            "WR320"
        ];

        /// <summary>
        /// Reads one model name and answers the canonical spelling.
        /// </summary>
        /// <param name="value">Name as the operator typed it, possibly null.</param>
        /// <param name="canonical">The catalogue's spelling when the name matches one.</param>
        /// <returns>True when the name matches an entry, compared case-insensitively.</returns>
        public static bool TryCanonicalize(string? value, out string canonical)
        {
            var trimmed = value?.Trim();

            if (!string.IsNullOrEmpty(trimmed))
            {
                foreach (var entry in All)
                {
                    if (string.Equals(entry, trimmed, StringComparison.OrdinalIgnoreCase))
                    {
                        canonical = entry;

                        return true;
                    }
                }
            }

            canonical = string.Empty;

            return false;
        }

        /// <summary>
        /// Rewrites a list into its stored form: canonical spellings, de-duplicated in
        /// first-occurrence order, and every entry that names no model left out.
        /// </summary>
        /// <param name="values">Names as they arrived, possibly null.</param>
        /// <returns>The canonical list; empty when the input names nothing.</returns>
        /// <remarks>
        /// Called only after validation passed. A list that reached this method with an unknown
        /// name would lose it, which is why the refusal belongs to the phase before.
        /// </remarks>
        public static string[] Normalize(IReadOnlyList<string>? values)
        {
            if (values is null || values.Count == 0)
            {
                return [];
            }

            var normalized = new List<string>(values.Count);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var value in values)
            {
                if (TryCanonicalize(value, out var canonical) && seen.Add(canonical))
                {
                    normalized.Add(canonical);
                }
            }

            return [.. normalized];
        }
    }
}
