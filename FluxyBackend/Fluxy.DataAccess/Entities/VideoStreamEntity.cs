using Fluxy.Core.Models.Streams;
using Fluxy.Core.Models.Users;
using Fluxy.Core.Persistence.Base;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of one configured stream. It is mutable, has no behaviour
    /// beyond mapping to <see cref="VideoStream"/>, and knows nothing about HTTP.
    /// </summary>
    /// <remarks>
    /// The two lists a stream broadcasts from are rows of their own
    /// (<see cref="VideoStreamSourceEntity"/>, <see cref="VideoStreamFileEntity"/>) rather
    /// than a column, for the reason the login guard rules and the group permissions are rows:
    /// the database can enforce "one position per stream" with a unique index instead of the
    /// application remembering to check, and an order can be read with plain SQL.
    /// </remarks>
    public sealed class VideoStreamEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VideoStreamEntity"/> class for a stream
        /// that is about to be inserted. The identifier and both audit timestamps are generated.
        /// </summary>
        public VideoStreamEntity()
        {
        }

        /// <summary>
        /// Display name, at most <see cref="Configurations.VideoStreamConfiguration.NameMaxLength"/>
        /// characters. Unique within its <see cref="CategoryId"/>, compared case sensitively -
        /// the same rule the accounts, the groups and the categories are held to.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Whether the stream is broadcasting right now.</summary>
        public VideoStreamStatus Status { get; set; }

        /// <summary>The kind of stream, which decides which of the payload rows is used.</summary>
        public VideoStreamType Type { get; set; }

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

        /// <summary>How far behind live the relay runs, in minutes. Zero means no deliberate delay.</summary>
        public int DelayMinutes { get; set; }

        /// <summary>
        /// Series this stream plays, for <see cref="VideoStreamType.OnlineSeries"/> and nothing
        /// else. Not a foreign key: no table of series exists in this database yet.
        /// </summary>
        public Guid? SeriesId { get; set; }

        /// <summary>
        /// On-demand item this stream plays, for <see cref="VideoStreamType.VodPlaylist"/> and
        /// nothing else. Not a foreign key, for the same reason.
        /// </summary>
        public Guid? VodId { get; set; }

        /// <summary>
        /// Programme guide the schedule is read from, or null when the stream carries none. Not
        /// a foreign key, for the same reason.
        /// </summary>
        public Guid? EpgSourceId { get; set; }

        /// <summary>Key the channel is filed under inside that guide, or null when there is none.</summary>
        public string? EpgChannelKey { get; set; }

        /// <summary>
        /// How many days of archive are kept, and only for
        /// <see cref="VideoStreamType.Permanent"/>. Zero means no archive.
        /// </summary>
        public int ArchiveDays { get; set; }

        /// <summary>
        /// Position among the streams of the same category, lowest first. Assigned when the
        /// stream is created and re-assigned to the end when it moves to another category.
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>User agent the relayed requests carry, or null to send none.</summary>
        public string? RestreamUserAgent { get; set; }

        /// <summary>Proxy the relay goes out through, or null to go out directly.</summary>
        public string? RestreamProxy { get; set; }

        /// <summary>Cookies the relayed requests carry, or null to send none.</summary>
        public string? RestreamCookies { get; set; }

        /// <summary>Extra headers the relayed requests carry, as typed.</summary>
        public string? RestreamHeaders { get; set; }

        /// <summary>The URLs this stream relays from, in the order they are used.</summary>
        public ICollection<VideoStreamSourceEntity> StreamSources { get; set; } =
            new List<VideoStreamSourceEntity>();

        /// <summary>The files this stream plays, in order.</summary>
        public ICollection<VideoStreamFileEntity> StreamFiles { get; set; } =
            new List<VideoStreamFileEntity>();

        /// <summary>
        /// Builds the business model out of this row and the two lists stored beside it.
        /// </summary>
        /// <param name="sources">Rows of <c>video_stream_sources</c>, already in position order.</param>
        /// <param name="files">Rows of <c>video_stream_files</c>, already in position order.</param>
        public VideoStream ToModel(
            IReadOnlyList<string> sources,
            IReadOnlyList<string> files) => new()
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
            DelayMinutes = DelayMinutes,
            SeriesId = SeriesId,
            VodId = VodId,
            EpgSourceId = EpgSourceId,
            EpgChannelKey = EpgChannelKey,
            ArchiveDays = ArchiveDays,
            SortOrder = SortOrder,
            RestreamUserAgent = RestreamUserAgent,
            RestreamProxy = RestreamProxy,
            RestreamCookies = RestreamCookies,
            RestreamHeaders = RestreamHeaders,
            Sources = sources,
            Files = files
        };
    }
}
