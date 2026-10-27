using Fluxy.Core.Persistence.Base;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// One video stream a bouquet holds, with its position inside that bouquet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A row per membership rather than a list in a column, for the reason the stream sources
    /// are rows: the order is read with a plain <c>ORDER BY sort_order</c> and the database can
    /// keep it honest with a unique index instead of the application remembering to.
    /// </para>
    /// <para>
    /// The same stream appears in as many bouquets as an operator puts it in, so the pair
    /// (bouquet, stream) is what is unique - not either of them alone.
    /// </para>
    /// <para>
    /// There are three siblings of this type, one per remaining content kind, because each kind
    /// is ordered independently and only a foreign key to the content's own table can make a
    /// deleted stream leave no link behind.
    /// </para>
    /// </remarks>
    public sealed class BouquetVideoStreamEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BouquetVideoStreamEntity"/> class for a
        /// membership that is about to be inserted. The identifier and both audit timestamps are
        /// generated.
        /// </summary>
        public BouquetVideoStreamEntity()
        {
        }

        /// <summary>Bouquet this row helps make up.</summary>
        public Guid BouquetId { get; set; }

        /// <summary>That bouquet.</summary>
        public BouquetEntity? Bouquet { get; set; }

        /// <summary>Stream the bouquet holds. Removed from every bouquet when the stream goes.</summary>
        public Guid VideoStreamId { get; set; }

        /// <summary>
        /// Position of the stream among the bouquet's video streams, lowest first. Written by the
        /// service when the whole list is replaced, never by anything else - a position is not a
        /// field an operator types.
        /// </summary>
        public int SortOrder { get; set; }
    }
}
