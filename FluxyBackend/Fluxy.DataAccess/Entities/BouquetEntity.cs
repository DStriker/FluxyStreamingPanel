using Fluxy.Core.Content.Models;
using Fluxy.Core.Persistence.Base;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of one bouquet - a named package of content. It is mutable, has no
    /// behaviour beyond mapping to <see cref="Bouquet"/>, and knows nothing about HTTP.
    /// </summary>
    /// <remarks>
    /// What the bouquet holds is not on this row. The four membership tables
    /// (<see cref="BouquetVideoStreamEntity"/>, <see cref="BouquetAudioStreamEntity"/>,
    /// <see cref="BouquetVodItemEntity"/>, <see cref="BouquetSeriesEntity"/>) own that, one per
    /// content kind, because each of them has to be ordered apart and only a real foreign key to
    /// the content table can keep a deleted video from leaving a link to nobody behind.
    /// </remarks>
    public sealed class BouquetEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BouquetEntity"/> class for a bouquet that
        /// is about to be inserted. The identifier and both audit timestamps are generated.
        /// </summary>
        public BouquetEntity()
        {
        }

        /// <summary>
        /// Display name, at most <see cref="Configurations.BouquetConfiguration.NameMaxLength"/>
        /// characters. Unique for the installation **without regard to case** - see the unique
        /// index over <c>lower(name)</c> the configuration's migration creates.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Position among the bouquets, lowest first. A plain integer rather than anything
        /// self-spacing: re-arranging is one write of 1..N over the rows the request names, and a
        /// deletion leaves a gap that nothing hurries to close.
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>Builds the business model out of this row.</summary>
        public Bouquet ToModel() => new()
        {
            Id = Id,
            Name = Name,
            SortOrder = SortOrder
        };

        /// <summary>Copies the changeable fields of the business model onto this row.</summary>
        /// <remarks>
        /// The position is copied here like any other field even though the service decides it,
        /// for the reason <see cref="ContentCategoryEntity.FromModel"/> gives: a mapping that
        /// silently ignored a field would be a difference the compiler cannot see.
        /// </remarks>
        public void FromModel(Bouquet model)
        {
            Name = model.Name;
            SortOrder = model.SortOrder;
        }
    }
}
