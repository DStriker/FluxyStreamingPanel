using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Commands
{
    /// <summary>
    /// One update an import stages on an existing stream, sharing the import's single save.
    /// </summary>
    /// <remarks>
    /// Only the fields an import may change travel here: status, type, delay, archive length,
    /// restream settings, and one optional source to append. Name, logo, programme guide,
    /// Enigma2 service id and category all stay as they are.
    /// </remarks>
    public sealed record VideoStreamBatchUpdate
    {
        /// <summary>Identifier of the stream to change.</summary>
        public required Guid Id { get; init; }

        /// <summary>Kind to write when <see cref="OverwriteSettings"/> is true.</summary>
        public required VideoStreamType Type { get; init; }

        /// <summary>State to write when <see cref="OverwriteSettings"/> is true.</summary>
        public required VideoStreamStatus Status { get; init; }

        /// <summary>Delay to write when <see cref="OverwriteSettings"/> is true.</summary>
        public required int DelayMinutes { get; init; }

        /// <summary>Archive length to write when <see cref="OverwriteSettings"/> is true.</summary>
        public required int ArchiveDays { get; init; }

        /// <summary>Restream user agent to write when <see cref="OverwriteSettings"/> is true.</summary>
        public required string? RestreamUserAgent { get; init; }

        /// <summary>Restream proxy to write when <see cref="OverwriteSettings"/> is true.</summary>
        public required string? RestreamProxy { get; init; }

        /// <summary>Restream cookies to write when <see cref="OverwriteSettings"/> is true.</summary>
        public required string? RestreamCookies { get; init; }

        /// <summary>Restream headers to write when <see cref="OverwriteSettings"/> is true.</summary>
        public required string? RestreamHeaders { get; init; }

        /// <summary>
        /// Source URL to append to the stored source list when <see cref="AppendSource"/> is
        /// true and the stream's kind relays from sources.
        /// </summary>
        public required string? SourceToAppend { get; init; }

        /// <summary>Whether to append <see cref="SourceToAppend"/> to the stored sources.</summary>
        public required bool AppendSource { get; init; }

        /// <summary>Whether to overwrite status, type, delay, archive and restream settings.</summary>
        public required bool OverwriteSettings { get; init; }
    }
}
