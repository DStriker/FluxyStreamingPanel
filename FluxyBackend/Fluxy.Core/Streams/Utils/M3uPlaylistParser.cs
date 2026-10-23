using System.Globalization;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Outcomes;

namespace Fluxy.Core.Streams.Utils
{
    /// <summary>
    /// Reads the two shapes of M3U line that describe a stream: an <c>#EXTINF</c> directive and
    /// the absolute address that follows it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The parser is deliberately forgiving about everything that does not decide whether a
    /// stream can be created. Unknown directives are ignored, a title may contain commas, and a
    /// URL without an <c>#EXTINF</c> before it still becomes an entry with the fallback name.
    /// What it refuses, it refuses one line at a time: a dangling directive or an address that
    /// is not an absolute URL becomes a problem and the rest of the file is still read, so an
    /// operator does not have to fix a five thousand line file one upload at a time.
    /// </para>
    /// <para>
    /// An HLS playlist is the exception, and it is refused whole. Its <c>#EXT-X-</c> tags name
    /// variants of one stream rather than separate streams, so importing them as a list of
    /// channels would create a channel per bitrate - a result nobody asked for and one that is
    /// hard to recognise after the fact.
    /// </para>
    /// </remarks>
    public static class M3uPlaylistParser
    {
        /// <summary>Directive that starts the description of one stream entry.</summary>
        private const string ExtInfPrefix = "#EXTINF";

        /// <summary>Prefix shared by every tag of an HLS playlist.</summary>
        private const string HlsTagPrefix = "#EXT-X-";

        /// <summary>
        /// Parses <paramref name="content"/> into the entries it can import and the lines it
        /// cannot.
        /// </summary>
        /// <param name="content">Text of the uploaded playlist.</param>
        /// <param name="maxEntries">Most entries one parse accepts.</param>
        /// <returns>
        /// The status of the file, the usable entries in playlist order, and one problem per
        /// line that could not become an entry.
        /// </returns>
        public static M3uPlaylistOutcome Parse(
            string? content,
            int maxEntries = VideoStreamImportLimits.MaxEntries)
        {
            var text = content?.TrimStart('\uFEFF');

            if (string.IsNullOrWhiteSpace(text))
            {
                return Status(M3uPlaylistStatus.Empty);
            }

            if (text.Contains('\0'))
            {
                return Status(M3uPlaylistStatus.Unreadable);
            }

            var entries = new List<M3uPlaylistEntry>();
            var problems = new List<M3uPlaylistProblem>();
            var entryCount = 0;
            var pending = false;
            var pendingLine = 0;
            string? pendingTitle = null;
            string? pendingTvgName = null;
            string? pendingTvgLogo = null;

            using var reader = new StringReader(text);
            var lineNumber = 0;

            while (reader.ReadLine() is { } line)
            {
                lineNumber++;
                var trimmed = line.Trim();

                if (trimmed.Length == 0)
                {
                    continue;
                }

                // HLS tags are checked before anything else: a master playlist can otherwise
                // look like a list of addresses, one per variant stream.
                if (trimmed.StartsWith(HlsTagPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return Status(M3uPlaylistStatus.Hls);
                }

                if (trimmed.StartsWith(ExtInfPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    entryCount++;
                    if (entryCount > maxEntries)
                    {
                        return Status(M3uPlaylistStatus.TooManyEntries);
                    }

                    // A second directive before an address means the first one described a
                    // stream that has none. Report it without losing the one that follows.
                    if (pending)
                    {
                        problems.Add(Dangling(pendingLine, ResolveName(pendingTitle, pendingTvgName, entryCount)));
                    }

                    (pendingTitle, pendingTvgName, pendingTvgLogo) = ParseExtInf(trimmed);
                    pending = true;
                    pendingLine = lineNumber;
                    continue;
                }

                // Every other directive is metadata for the entry below it, not an entry.
                if (trimmed.StartsWith('#'))
                {
                    continue;
                }

                if (!pending)
                {
                    entryCount++;
                    if (entryCount > maxEntries)
                    {
                        return Status(M3uPlaylistStatus.TooManyEntries);
                    }
                }

                var entryLine = pending ? pendingLine : lineNumber;
                var name = pending
                    ? ResolveName(pendingTitle, pendingTvgName, entryCount)
                    : VideoStreamImportLimits.FallbackNamePrefix
                        + entryCount.ToString(CultureInfo.InvariantCulture);

                if (!IsAbsoluteUrl(trimmed))
                {
                    problems.Add(new M3uPlaylistProblem
                    {
                        LineNumber = entryLine,
                        Name = name,
                        Message = "The stream address is not an absolute URL."
                    });
                }
                else
                {
                    entries.Add(new M3uPlaylistEntry
                    {
                        Name = name,
                        Source = trimmed,
                        LogoUrl = pendingTvgLogo,
                        LineNumber = entryLine
                    });
                }

                pending = false;
                pendingTitle = null;
                pendingTvgName = null;
                pendingTvgLogo = null;
            }

            if (pending)
            {
                problems.Add(Dangling(pendingLine, ResolveName(pendingTitle, pendingTvgName, entryCount)));
            }

            pendingTvgLogo = null;

            if (entries.Count == 0 && problems.Count == 0)
            {
                return Status(M3uPlaylistStatus.Unreadable);
            }

            return new M3uPlaylistOutcome
            {
                Status = M3uPlaylistStatus.Parsed,
                Entries = entries,
                Problems = problems
            };
        }

        /// <summary>
        /// Reads the title and <c>tvg-name</c> attribute of one <c>#EXTINF</c> line.
        /// </summary>
        /// <remarks>
        /// The title is everything after the first comma that is not inside a quoted attribute
        /// value. That is what lets a channel called "News, weather and sport" keep its comma
        /// instead of being cut at it.
        /// </remarks>
        private static (string? Title, string? TvgName, string? TvgLogo) ParseExtInf(string line)
        {
            var body = line.Length > ExtInfPrefix.Length
                ? line[ExtInfPrefix.Length..].TrimStart()
                : string.Empty;

            if (body.StartsWith(':'))
            {
                body = body[1..];
            }

            var comma = IndexOfUnquotedComma(body);
            var header = comma >= 0 ? body[..comma] : body;
            var title = comma >= 0 ? body[(comma + 1)..].Trim() : null;

            return (
                string.IsNullOrWhiteSpace(title) ? null : title,
                ReadAttribute(header, "tvg-name"),
                ReadAttribute(header, "tvg-logo"));
        }

        /// <summary>Finds the comma that starts the title, ignoring quoted attribute values.</summary>
        private static int IndexOfUnquotedComma(string value)
        {
            var quote = '\0';

            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];

                if (quote != '\0')
                {
                    if (current == quote)
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (current is '"' or '\'')
                {
                    quote = current;
                    continue;
                }

                if (current == ',')
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>Reads one quoted or bare attribute out of an <c>#EXTINF</c> header.</summary>
        private static string? ReadAttribute(string header, string name)
        {
            var index = 0;

            while (index < header.Length)
            {
                while (index < header.Length && char.IsWhiteSpace(header[index]))
                {
                    index++;
                }

                var keyStart = index;
                while (index < header.Length && !char.IsWhiteSpace(header[index]) && header[index] != '=')
                {
                    index++;
                }

                var key = header[keyStart..index];

                if (index >= header.Length || header[index] != '=')
                {
                    // A bare token (the duration, for example) rather than an assignment.
                    continue;
                }

                index++;

                string value;
                if (index < header.Length && header[index] is '"' or '\'')
                {
                    var quote = header[index++];
                    var valueStart = index;

                    while (index < header.Length && header[index] != quote)
                    {
                        index++;
                    }

                    value = header[valueStart..index];

                    if (index < header.Length)
                    {
                        index++;
                    }
                }
                else
                {
                    var valueStart = index;
                    while (index < header.Length && !char.IsWhiteSpace(header[index]))
                    {
                        index++;
                    }

                    value = header[valueStart..index];
                }

                if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                {
                    var trimmed = value.Trim();
                    return trimmed.Length == 0 ? null : trimmed;
                }
            }

            return null;
        }

        /// <summary>Whether a line is an address a relay can be pointed at.</summary>
        private static bool IsAbsoluteUrl(string value)
            => Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && uri.Scheme.Length > 1;

        /// <summary>The name an entry gets, preferring the title, then <c>tvg-name</c>, then a number.</summary>
        private static string ResolveName(string? title, string? tvgName, int number)
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title.Trim();
            }

            if (!string.IsNullOrWhiteSpace(tvgName))
            {
                return tvgName.Trim();
            }

            return VideoStreamImportLimits.FallbackNamePrefix
                + number.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Builds the problem a directive with no address after it gets.</summary>
        private static M3uPlaylistProblem Dangling(int lineNumber, string name) => new()
        {
            LineNumber = lineNumber,
            Name = name,
            Message = "The #EXTINF line is not followed by a stream address."
        };

        /// <summary>An outcome that carries only a status.</summary>
        private static M3uPlaylistOutcome Status(M3uPlaylistStatus status) => new() { Status = status };
    }
}
