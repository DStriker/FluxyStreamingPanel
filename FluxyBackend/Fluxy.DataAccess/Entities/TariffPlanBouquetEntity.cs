using Fluxy.Core.Persistence.Base;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// One bouquet a tariff plan delivers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A row per membership rather than a list in a column, for the reason the bouquet's own
    /// membership tables are rows: the database can enforce the pair with a unique index and a
    /// deleted bouquet leaves no link behind.
    /// </para>
    /// <para>
    /// There is no position column. The order a plan lists its bouquets in means nothing - the
    /// subscriber's device fetches a package, not an arrangement - which is exactly what
    /// separates this relation from the content inside a bouquet.
    /// </para>
    /// </remarks>
    public sealed class TariffPlanBouquetEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TariffPlanBouquetEntity"/> class for a
        /// membership that is about to be inserted. The identifier and both audit timestamps are
        /// generated.
        /// </summary>
        public TariffPlanBouquetEntity()
        {
        }

        /// <summary>Plan this row delivers.</summary>
        public Guid TariffPlanId { get; set; }

        /// <summary>That plan. Removed with it.</summary>
        public TariffPlanEntity? TariffPlan { get; set; }

        /// <summary>Bouquet the plan delivers. Removed from every plan when the bouquet goes.</summary>
        public Guid BouquetId { get; set; }
    }
}
