using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Commands
{
    /// <summary>
    /// One update an import stages on an existing audio stream, sharing the import's single
    /// save.
    /// </summary>
    /// <remarks>
    /// Only the fields an import may change travel here: status, type, the restream settings,
    /// and one optional source to append. Name, logo, Enigma2 service id and category all stay
    /// as they are.
    /// </remarks>
    public sealed record AudioStreamBatchUpdate
    {
        /// <summary>Identifier of the stream to change.</summary>
        public required Guid Id { get; init; }

        /// <summary>Kind to write when <see cref="OverwriteSettings"/> is true.</summary>
        public required AudioStreamType Type { get; init; }

        /// <summary>State to write when <see cref="OverwriteSettings"/> is true.</summary>
        public required AudioStreamStatus Status { get; init; }

        /// <summary>Restream user agent to write when <see cref="OverwriteSettings"/> is true.</summary>
        public required string? RestreamUserAgent { get; init; }

        /// <summary>Restream proxy to write when <see cref="OverwriteSettings"/> is true.</summary>
        public required string? RestreamProxy { get; init; }

        /// <summary>Restream cookies to write when <see cref="OverwriteSettings"/> is true.</summary>
        public required string? RestreamCookies { get; init; }

        /// <summary>Restream headers to write when <see cref="OverwriteSettings"/> is true.</summary>
        public required string? RestreamHeaders { get; init; }

        /// <summary>Source the entry contributes, or null when the line named none.</summary>
        public required string? SourceToAppend { get; init; }

        /// <summary>Whether the contributed source is appended to the stored list.</summary>
        public required bool AppendSource { get; init; }

        /// <summary>Whether the shared settings replace the stored ones.</summary>
        public required bool OverwriteSettings { get; init; }
    }
}
