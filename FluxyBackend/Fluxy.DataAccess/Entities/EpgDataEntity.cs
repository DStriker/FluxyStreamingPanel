using Fluxy.Core.Epg.Models;
using Fluxy.Core.Persistence.Base;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of one programme in a guide. It is mutable, has no behaviour
    /// beyond mapping to <see cref="EpgData"/>, and knows nothing about HTTP.
    /// </summary>
    /// <remarks>
    /// Rows of this table are written and deleted by the guide updater alone; nothing in the
    /// admin surface creates one. The source is a real foreign key with a cascade, so a guide
    /// and everything parsed out of it disappear together.
    /// </remarks>
    public sealed class EpgDataEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EpgDataEntity"/> class for a
        /// programme that is about to be inserted. The identifier and both audit timestamps
        /// are generated.
        /// </summary>
        public EpgDataEntity()
        {
        }

        /// <summary>Source this programme belongs to.</summary>
        public Guid SourceId { get; set; }

        /// <summary>Channel key inside the guide file, not a stream of this database.</summary>
        public string ChannelId { get; set; } = string.Empty;

        /// <summary>Programme title.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Language the guide stated for this programme, or null.</summary>
        public string? Language { get; set; }

        /// <summary>Moment the programme starts.</summary>
        public DateTimeOffset StartDate { get; set; }

        /// <summary>Moment the programme ends.</summary>
        public DateTimeOffset EndDate { get; set; }

        /// <summary>Description the guide carries, or null.</summary>
        public string? Description { get; set; }

        /// <summary>Builds the business model out of this row.</summary>
        public EpgData ToModel() => new()
        {
            Id = Id,
            SourceId = SourceId,
            ChannelId = ChannelId,
            Title = Title,
            Language = Language,
            StartDate = StartDate,
            EndDate = EndDate,
            Description = Description
        };
    }
}
