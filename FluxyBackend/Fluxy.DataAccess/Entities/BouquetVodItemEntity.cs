using Fluxy.Core.Persistence.Base;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// One on-demand video a bouquet holds, with its position inside that bouquet.
    /// </summary>
    /// <remarks>
    /// The sibling of <see cref="BouquetVideoStreamEntity"/>, and a table of its own for the
    /// reason that one states: the catalogue is ordered apart from the live streams, and a
    /// foreign key to <c>vod_items</c> is what makes a deleted video leave no link behind.
    /// </remarks>
    public sealed class BouquetVodItemEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BouquetVodItemEntity"/> class for a
        /// membership that is about to be inserted. The identifier and both audit timestamps are
        /// generated.
        /// </summary>
        public BouquetVodItemEntity()
        {
        }

        /// <summary>Bouquet this row helps make up.</summary>
        public Guid BouquetId { get; set; }

        /// <summary>That bouquet.</summary>
        public BouquetEntity? Bouquet { get; set; }

        /// <summary>On-demand video the bouquet holds. Removed from every bouquet when it goes.</summary>
        public Guid VodItemId { get; set; }

        /// <summary>Position of the video among the bouquet's on-demand videos, lowest first.</summary>
        public int SortOrder { get; set; }
    }
}
