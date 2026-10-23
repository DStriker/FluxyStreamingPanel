using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Commands
{
    /// <summary>
    /// Everything an administrator supplies to create streams from an M3U playlist.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The playlist travels as its text rather than as parsed entries, because reading a file is
    /// an input concern and every rule that decides what a stream may contain belongs behind the
    /// service. The service parses it, drops the entries it cannot use and reports them, then
    /// writes the survivors as one operation.
    /// </para>
    /// <para>
    /// The name is absent on purpose: names come from the playlist. The category and the status
    /// are shared by every stream the file creates, as are the delay, the archive length and
    /// the restream settings - they are the choices the import form makes once for the whole
    /// batch.
    /// </para>
    /// </remarks>
    public sealed record NewVideoStreamImport
    {
        /// <summary>Kind every imported stream carries.</summary>
        public required VideoStreamType Type { get; init; }

        /// <summary>Whether every imported stream opens active or disabled.</summary>
        public required VideoStreamStatus Status { get; init; }

        /// <summary>Category every imported stream is filed under.</summary>
        public required Guid CategoryId { get; init; }

        /// <summary>Text of the uploaded playlist.</summary>
        public required string Playlist { get; init; }

        /// <summary>How far behind live every imported relay runs, in minutes.</summary>
        public int DelayMinutes { get; init; }

        /// <summary>How many days of archive every imported stream keeps; permanent only.</summary>
        public int ArchiveDays { get; init; }

        /// <summary>User agent every imported relay carries, or null to send none.</summary>
        public string? RestreamUserAgent { get; init; }

        /// <summary>Proxy every imported relay goes out through, or null to go out directly.</summary>
        public string? RestreamProxy { get; init; }

        /// <summary>Cookies every imported relayed request carries, or null to send none.</summary>
        public string? RestreamCookies { get; init; }

        /// <summary>Extra headers every imported relayed request carries, as typed.</summary>
        public string? RestreamHeaders { get; init; }

        /// <summary>
        /// Append the entry's source URL to an existing stream's source list instead of
        /// skipping the entry when its name is already taken.
        /// </summary>
        public bool AppendSourceToExisting { get; init; }

        /// <summary>
        /// Overwrite an existing stream's status, type, delay, archive length and restream
        /// settings instead of skipping the entry when its name is already taken.
        /// </summary>
        public bool OverwriteExistingSettings { get; init; }
    }
}
