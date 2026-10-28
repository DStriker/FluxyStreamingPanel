using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Commands
{
    /// <summary>
    /// Everything an administrator supplies to create audio streams from an M3U playlist.
    /// </summary>
    /// <remarks>
    /// The playlist travels as its text rather than as parsed entries, because reading a file
    /// is an input concern and every rule that decides what a stream may contain belongs behind
    /// the service. The service parses it, drops the entries it cannot use and reports them,
    /// then writes the survivors as one operation. The name is absent on purpose: names come
    /// from the playlist.
    /// </remarks>
    public sealed record NewAudioStreamImport
    {
        /// <summary>Kind every imported stream carries.</summary>
        public required AudioStreamType Type { get; init; }

        /// <summary>Whether every imported stream opens active or disabled.</summary>
        public required AudioStreamStatus Status { get; init; }

        /// <summary>Category every imported stream is filed under.</summary>
        public required Guid CategoryId { get; init; }

        /// <summary>Text of the uploaded playlist.</summary>
        public required string Playlist { get; init; }

        /// <summary>User agent every imported relay carries, or null.</summary>
        public string? RestreamUserAgent { get; init; }

        /// <summary>Proxy every imported relay goes out through, or null.</summary>
        public string? RestreamProxy { get; init; }

        /// <summary>Cookies every imported relay carries, or null.</summary>
        public string? RestreamCookies { get; init; }

        /// <summary>Extra headers every imported relay carries, as typed.</summary>
        public string? RestreamHeaders { get; init; }

        /// <summary>Whether an entry whose name is already taken appends its source instead of being skipped.</summary>
        public bool AppendSourceToExisting { get; init; }

        /// <summary>Whether an entry whose name is already taken overwrites the stored settings.</summary>
        public bool OverwriteExistingSettings { get; init; }

        /// <summary>
        /// Bouquets every stream this import creates joins, or null for none.
        /// </summary>
        /// <remarks>
        /// Applied to the rows the import **creates**, not to the ones it skips or appends to:
        /// those already exist, and an import is not an edit of a stream somebody else put in
        /// a package. A stream named in a bouquet is appended to the end of its list, exactly
        /// as it would be from the single-stream form.
        /// </remarks>
        public IReadOnlyList<Guid>? BouquetIds { get; init; }
    }
}
