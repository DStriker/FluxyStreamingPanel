namespace Fluxy.Core.Backups.Utils
{
    /// <summary>
    /// What a name may be before it is allowed to reach the filesystem.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The rule is deliberately narrower than the platform's.</b> A name is accepted only when
    /// it matches <c>^[A-Za-z0-9][A-Za-z0-9._-]{0,79}\.dump$</c>: it starts with a letter or a
    /// digit, carries no separator, no colon and no control character, and is at most 84
    /// characters. That excludes every traversal spelling, every drive letter and every alternate
    /// data stream before the filesystem ever sees the string.
    /// </para>
    /// <para>
    /// <b>Two platform traps stay excluded by name.</b> A trailing dot or space is silently
    /// stripped by Windows, which would make <c>x.dump.</c> and <c>x.dump</c> the same file; and
    /// the reserved device names (<c>CON</c>, <c>PRN</c>, <c>AUX</c>, <c>NUL</c>, <c>COM1-9</c>,
    /// <c>LPT1-9</c>) name devices rather than files. Both are refused case-insensitively.
    /// </para>
    /// <para>
    /// <see cref="IsInsideDirectory"/> is the belt to this brace: the resolved path must start
    /// with the resolved directory and its separator, so even a name that slipped the pattern
    /// cannot escape. The pair is checked together, and the API answers both failures with the
    /// same <c>404 backup_not_found</c> - an invalid name and a name resolving to nothing have to
    /// be indistinguishable, or the endpoint becomes a way to probe the host's paths.
    /// </para>
    /// </remarks>
    public static class BackupFileNameRules
    {
        /// <summary>
        /// Longest accepted name, in characters: the pattern's 1 + 79 characters, then the
        /// five of <c>.dump</c>.
        /// </summary>
        public const int MaxLength = 85;

        /// <summary>The reserved Windows device names, case-insensitive.</summary>
        private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>Whether a client-supplied name may be used at all.</summary>
        /// <param name="fileName">The name from the route.</param>
        /// <returns>Whether every rule holds.</returns>
        public static bool IsValid(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)
                || fileName.Length > MaxLength
                || !fileName.EndsWith(BackupFileName.Extension, StringComparison.Ordinal))
            {
                return false;
            }

            if (!IsAllowedStart(fileName[0]))
            {
                return false;
            }

            foreach (var character in fileName.AsSpan(1))
            {
                if (!IsAllowed(character))
                {
                    return false;
                }
            }

            // The extension alone is a name with nothing in front of it.
            if (fileName.Length <= BackupFileName.Extension.Length)
            {
                return false;
            }

            var stem = fileName[..^BackupFileName.Extension.Length];

            if (stem.EndsWith('.') || stem.EndsWith(' '))
            {
                return false;
            }

            return !ReservedDeviceNames.Contains(stem);
        }

        /// <summary>
        /// Whether the resolved path of a name really sits inside the directory.
        /// </summary>
        /// <param name="directory">The backup directory, relative or absolute.</param>
        /// <param name="fileName">The name to resolve against it.</param>
        /// <returns>Whether the resolved path is a child of the resolved directory.</returns>
        public static bool IsInsideDirectory(string directory, string fileName)
        {
            try
            {
                var root = Path.GetFullPath(directory);
                var full = Path.GetFullPath(Path.Combine(root, fileName));
                var prefix = root.EndsWith(Path.DirectorySeparatorChar)
                    ? root
                    : root + Path.DirectorySeparatorChar;

                return full.StartsWith(
                    prefix,
                    OperatingSystem.IsWindows()
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        /// <summary>Whether a character may open a name: a letter or a digit.</summary>
        private static bool IsAllowedStart(char character)
            => char.IsAsciiLetterOrDigit(character);

        /// <summary>Whether a character may appear anywhere after the first.</summary>
        private static bool IsAllowed(char character)
            => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-';
    }
}
