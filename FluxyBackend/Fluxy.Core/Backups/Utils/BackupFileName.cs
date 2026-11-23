namespace Fluxy.Core.Backups.Utils
{
    /// <summary>
    /// The one naming rule the whole feature reads and writes by:
    /// <c>fluxy-yyyyMMddTHHmmssZ.dump</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The name is the record.</b> There is no metadata row, so the instant in the name is the
    /// only answer to "when was this taken" and the only thing rotation can order by. The format
    /// is UTC at second precision with a literal <c>Z</c>, which makes lexicographic order equal
    /// chronological order - a property the rotation relies on and the tests pin.
    /// </para>
    /// <para>
    /// A collision inside one second is not an error: <see cref="BuildCollision"/> appends
    /// <c>-2</c> to <c>-9</c>, and a tenth attempt in the same second is reported as a failure
    /// rather than overwriting a file. <see cref="TryParse"/> reads the timestamp back out and
    /// ignores the suffix, so a suffixed name still sorts by its own instant.
    /// </para>
    /// </remarks>
    public static class BackupFileName
    {
        /// <summary>Prefix every archive name starts with.</summary>
        public const string Prefix = "fluxy-";

        /// <summary>Extension every archive carries.</summary>
        public const string Extension = ".dump";

        /// <summary>The format of the instant inside the name.</summary>
        public const string TimestampFormat = "yyyyMMdd'T'HHmmss'Z'";

        /// <summary>
        /// How many characters <see cref="TimestampFormat"/> produces: what the parse slices by.
        /// </summary>
        /// <remarks>
        /// Separate from the format string because that one carries four quote characters the
        /// output does not: a slice by <c>TimestampFormat.Length</c> would take two characters of
        /// the suffix with it and every name would fail to parse.
        /// </remarks>
        public const int TimestampLength = 16;

        /// <summary>First suffix a collision attempt uses.</summary>
        public const int FirstCollisionAttempt = 2;

        /// <summary>Last suffix a collision attempt uses.</summary>
        public const int LastCollisionAttempt = 9;

        /// <summary>The name of a dump taken at the given instant.</summary>
        /// <param name="utc">Instant the dump was taken; its UTC reading is what the name carries.</param>
        /// <returns>The archive name.</returns>
        public static string Build(DateTimeOffset utc)
            => $"{Prefix}{utc.UtcDateTime.ToString(TimestampFormat, System.Globalization.CultureInfo.InvariantCulture)}{Extension}";

        /// <summary>
        /// The name of a collision attempt: the same instant with <c>-2</c> through <c>-9</c>
        /// appended.
        /// </summary>
        /// <param name="utc">Instant the dump was taken.</param>
        /// <param name="attempt">The attempt number; values outside 2..9 are clamped into it.</param>
        /// <returns>The archive name.</returns>
        public static string BuildCollision(DateTimeOffset utc, int attempt)
        {
            var bounded = Math.Clamp(attempt, FirstCollisionAttempt, LastCollisionAttempt);
            var stamp = utc.UtcDateTime.ToString(TimestampFormat, System.Globalization.CultureInfo.InvariantCulture);

            return $"{Prefix}{stamp}-{bounded}{Extension}";
        }

        /// <summary>
        /// Reads the instant a file name carries.
        /// </summary>
        /// <param name="fileName">The file name, with or without a collision suffix.</param>
        /// <param name="utc">The instant the name encodes, when the name is well formed.</param>
        /// <returns>Whether the name is one this feature writes.</returns>
        public static bool TryParse(string fileName, out DateTimeOffset utc)
        {
            utc = default;

            if (string.IsNullOrEmpty(fileName)
                || !fileName.StartsWith(Prefix, StringComparison.Ordinal)
                || !fileName.EndsWith(Extension, StringComparison.Ordinal))
            {
                return false;
            }

            var body = fileName[Prefix.Length..^Extension.Length];

            // A collision suffix is not part of the instant: the timestamp is the first sixteen
            // characters, and anything after it must be the `-n` shape this feature writes.
            if (body.Length < TimestampLength)
            {
                return false;
            }

            var stamp = body[..TimestampLength];

            if (body.Length > TimestampLength)
            {
                var suffix = body[TimestampLength..];

                if (suffix.Length != 2
                    || suffix[0] != '-'
                    || suffix[1] is < '2' or > '9')
                {
                    return false;
                }
            }

            if (!DateTime.TryParseExact(
                    stamp,
                    TimestampFormat,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal
                        | System.Globalization.DateTimeStyles.AssumeUniversal,
                    out var parsed))
            {
                return false;
            }

            utc = new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Utc));

            return true;
        }
    }
}
