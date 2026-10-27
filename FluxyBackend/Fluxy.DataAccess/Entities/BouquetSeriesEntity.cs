using Fluxy.Core.Persistence.Base;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// One series a bouquet holds, with its position inside that bouquet.
    /// </summary>
    /// <remarks>
    /// The sibling of <see cref="BouquetVideoStreamEntity"/>, and a table of its own for the
    /// reason that one states: the series are ordered apart from the other three kinds, and a
    /// foreign key to <c>series</c> is what makes a deleted series leave no link behind.
    /// </remarks>
    public sealed class BouquetSeriesEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BouquetSeriesEntity"/> class for a
        /// membership that is about to be inserted. The identifier and both audit timestamps are
        /// generated.
        /// </summary>
        public BouquetSeriesEntity()
        {
        }

        /// <summary>Bouquet this row helps make up.</summary>
        public Guid BouquetId { get; set; }

        /// <summary>That bouquet.</summary>
        public BouquetEntity? Bouquet { get; set; }

        /// <summary>Series the bouquet holds. Removed from every bouquet when it goes.</summary>
        public Guid SeriesId { get; set; }

        /// <summary>Position of the series among the bouquet's series, lowest first.</summary>
        public int SortOrder { get; set; }
    }
}
