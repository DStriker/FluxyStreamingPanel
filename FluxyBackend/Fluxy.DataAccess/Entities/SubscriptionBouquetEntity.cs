using Fluxy.Core.Persistence.Base;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// One bouquet a subscription carries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A row per membership rather than a list in a column, exactly as
    /// <see cref="TariffPlanBouquetEntity"/> is: the database can enforce the pair with a unique
    /// index and a deleted bouquet leaves no link behind.
    /// </para>
    /// <para>
    /// These rows are used only while the subscription has no tariff plan. With a plan the
    /// plan's own bouquet list is the source of truth and these are kept unchanged but unused -
    /// which is why removing the plan re-activates them rather than starting from nothing.
    /// </para>
    /// </remarks>
    public sealed class SubscriptionBouquetEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionBouquetEntity"/> class for a
        /// membership that is about to be inserted. The identifier and both audit timestamps are
        /// generated.
        /// </summary>
        public SubscriptionBouquetEntity()
        {
        }

        /// <summary>Subscription this row belongs to.</summary>
        public Guid SubscriptionId { get; set; }

        /// <summary>That subscription. Removed with it.</summary>
        public SubscriptionEntity? Subscription { get; set; }

        /// <summary>Bouquet the subscription carries. Removed from every subscription when the bouquet goes.</summary>
        public Guid BouquetId { get; set; }
    }
}
