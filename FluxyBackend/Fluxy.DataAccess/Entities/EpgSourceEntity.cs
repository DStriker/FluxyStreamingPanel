using Fluxy.Core.Epg.Models;
using Fluxy.Core.Persistence.Base;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of one programme guide source. It is mutable, has no behaviour
    /// beyond mapping to <see cref="EpgSource"/>, and knows nothing about HTTP.
    /// </summary>
    /// <remarks>
    /// Two of the columns are owned by the updater rather than by an operator: the moment of
    /// the last refresh and the raw guide itself. Nothing in this pass writes either, and the
    /// raw guide is deliberately absent from every read the API performs.
    /// </remarks>
    public sealed class EpgSourceEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EpgSourceEntity"/> class for a source
        /// that is about to be inserted. The identifier and both audit timestamps are
        /// generated.
        /// </summary>
        public EpgSourceEntity()
        {
        }

        /// <summary>Display name, unique across the installation.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Absolute http(s) address the guide is fetched from.</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>Days of programmes kept for this source.</summary>
        public int DaysKeep { get; set; }

        /// <summary>When the guide was last refreshed, or null when it never has been.</summary>
        public DateTimeOffset? LastEpgUpdatedAt { get; set; }

        /// <summary>The guide exactly as the last update received it, or null.</summary>
        public string? RawData { get; set; }

        /// <summary>Builds the business model out of this row.</summary>
        public EpgSource ToModel() => new()
        {
            Id = Id,
            Name = Name,
            Url = Url,
            DaysKeep = DaysKeep,
            LastEpgUpdatedAt = LastEpgUpdatedAt,
            RawData = RawData
        };
    }
}
