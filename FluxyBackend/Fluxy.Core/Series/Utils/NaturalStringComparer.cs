namespace Fluxy.Core.Series.Utils
{
    /// <summary>
    /// Orders file names the way a person reads them: a run of digits is compared as a number,
    /// not as text, so <c>Episode 2</c> comes before <c>Episode 10</c>.
    /// </summary>
    /// <remarks>
    /// The season form uses this to number the files of a folder. An ordinal comparison would
    /// give episode 1 the tenth file; this comparer is the whole reason the numbering matches
    /// the order the folder is listed in. The comparison is case insensitive, with an ordinal
    /// tie-break so two names that differ only in case still have a stable order.
    /// </remarks>
    public sealed class NaturalStringComparer : IComparer<string>
    {
        /// <summary>The shared instance, because the comparer is stateless.</summary>
        public static NaturalStringComparer Instance { get; } = new();

        /// <inheritdoc />
        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            var left = 0;
            var right = 0;

            while (left < x.Length && right < y.Length)
            {
                if (char.IsAsciiDigit(x[left]) && char.IsAsciiDigit(y[right]))
                {
                    var leftStart = left;
                    var rightStart = right;

                    while (left < x.Length && char.IsAsciiDigit(x[left]))
                    {
                        left++;
                    }

                    while (right < y.Length && char.IsAsciiDigit(y[right]))
                    {
                        right++;
                    }

                    var leftDigits = x.AsSpan(leftStart, left - leftStart).TrimStart('0');
                    var rightDigits = y.AsSpan(rightStart, right - rightStart).TrimStart('0');

                    if (leftDigits.Length != rightDigits.Length)
                    {
                        return leftDigits.Length.CompareTo(rightDigits.Length);
                    }

                    var numeric = leftDigits.SequenceCompareTo(rightDigits);

                    if (numeric != 0)
                    {
                        return numeric;
                    }

                    continue;
                }

                var comparison = char.ToUpperInvariant(x[left])
                    .CompareTo(char.ToUpperInvariant(y[right]));

                if (comparison != 0)
                {
                    return comparison;
                }

                left++;
                right++;
            }

            var length = (x.Length - left).CompareTo(y.Length - right);

            return length != 0 ? length : string.CompareOrdinal(x, y);
        }
    }
}
