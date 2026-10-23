using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Models;

namespace Fluxy.Core.Streams.ReadModels
{
    /// <summary>
    /// One row of the audio streams table: what a line of it draws, and nothing it does not.
    /// </summary>
    /// <remarks>
    /// A projection rather than <see cref="AudioStream"/> for the same reason
    /// <c>AdminUser</c> is one: the table does not draw the restreaming parameters or the
    /// source list, and a list endpoint that answered with all of them would push twenty full
    /// rows over the wire to draw eight columns.
    /// </remarks>
    public sealed record AudioStreamListItem
    {
        /// <summary>Identifier of the stream, as text. The table shows it shortened.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name, unique within its category.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the stream is broadcasting right now.</summary>
        public required AudioStreamStatus Status { get; init; }

        /// <summary>The kind of stream.</summary>
        public required AudioStreamType Type { get; init; }

        /// <summary>Category the stream is filed under.</summary>
        public required Guid CategoryId { get; init; }

        /// <summary>Display name of that category, which is what the column draws.</summary>
        public required string CategoryName { get; init; }

        /// <summary>Address the icon is fetched from, or null.</summary>
        public string? LogoUrl { get; init; }

        /// <summary>Source the relaying process is using right now, or null.</summary>
        public string? ActiveSource { get; init; }
    }
}
