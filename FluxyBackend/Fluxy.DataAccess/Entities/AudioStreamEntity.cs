using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Models;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of one configured audio stream. It is mutable, has no behaviour
    /// beyond mapping to <see cref="AudioStream"/>, and knows nothing about HTTP.
    /// </summary>
    /// <remarks>
    /// The addresses a stream relays from are rows of their own
    /// (<see cref="AudioStreamSourceEntity"/>) rather than a column, for the reason the login
    /// guard rules and the group permissions are rows: the database can enforce "one position
    /// per stream" with a unique index instead of the application remembering to check, and an
    /// order can be read with plain SQL.
    /// </remarks>
    public sealed class AudioStreamEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AudioStreamEntity"/> class for a stream
        /// that is about to be inserted. The identifier and both audit timestamps are generated.
        /// </summary>
        public AudioStreamEntity()
        {
        }

        /// <summary>
        /// Display name, at most <see cref="Configurations.AudioStreamConfiguration.NameMaxLength"/>
        /// characters. Unique within its <see cref="CategoryId"/>, compared case sensitively.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Whether the stream is active right now.</summary>
        public AudioStreamStatus Status { get; set; }

        /// <summary>The kind of stream, which decides how the relay is started.</summary>
        public AudioStreamType Type { get; set; }

        /// <summary>Category the stream is filed under.</summary>
        public Guid CategoryId { get; set; }

        /// <summary>That category, read for the name the table column draws.</summary>
        public ContentCategoryEntity? Category { get; set; }

        /// <summary>Address the icon is fetched from, or null when the stream has none.</summary>
        public string? LogoUrl { get; set; }

        /// <summary>
        /// Source the relaying process is using right now, reported back rather than entered.
        /// Nothing in the interface writes it.
        /// </summary>
        public string? ActiveSource { get; set; }

        /// <summary>Enigma2 service reference as text, or null when the stream is not published there.</summary>
        public string? Enigma2Sid { get; set; }

        /// <summary>User agent the relayed requests carry, or null to send none.</summary>
        public string? RestreamUserAgent { get; set; }

        /// <summary>Proxy the relay goes out through, or null to go out directly.</summary>
        public string? RestreamProxy { get; set; }

        /// <summary>Cookies the relayed requests carry, or null to send none.</summary>
        public string? RestreamCookies { get; set; }

        /// <summary>Extra headers the relayed requests carry, as typed.</summary>
        public string? RestreamHeaders { get; set; }

        /// <summary>The addresses this stream relays from, in the order they are used.</summary>
        public ICollection<AudioStreamSourceEntity> StreamSources { get; set; } =
            new List<AudioStreamSourceEntity>();

        /// <summary>Builds the business model out of this row and the sources stored beside it.</summary>
        /// <param name="sources">Rows of <c>audio_stream_sources</c>, already in position order.</param>
        public AudioStream ToModel(IReadOnlyList<string> sources) => new()
        {
            Id = Id,
            Name = Name,
            Status = Status,
            Type = Type,
            CategoryId = CategoryId,
            CategoryName = Category?.Name ?? string.Empty,
            LogoUrl = LogoUrl,
            ActiveSource = ActiveSource,
            Enigma2Sid = Enigma2Sid,
            RestreamUserAgent = RestreamUserAgent,
            RestreamProxy = RestreamProxy,
            RestreamCookies = RestreamCookies,
            RestreamHeaders = RestreamHeaders,
            Sources = sources
        };
    }
}
