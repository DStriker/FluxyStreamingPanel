using Fluxy.Core.Persistence.Base;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// One user group that may be sold a tariff plan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A row per membership rather than a list in a column, for the reason the group's
    /// permissions are rows: the database can keep "no duplicates" with a unique index instead of
    /// the application remembering to, and a membership can be counted and joined with plain SQL.
    /// </para>
    /// <para>
    /// The pair (plan, group) is what is unique - not either of them alone - because the same
    /// group may be offered as many plans as an operator puts in front of it.
    /// </para>
    /// </remarks>
    public sealed class TariffPlanUserGroupEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TariffPlanUserGroupEntity"/> class for a
        /// membership that is about to be inserted. The identifier and both audit timestamps are
        /// generated.
        /// </summary>
        public TariffPlanUserGroupEntity()
        {
        }

        /// <summary>Plan this row helps offer.</summary>
        public Guid TariffPlanId { get; set; }

        /// <summary>That plan. Removed with it.</summary>
        public TariffPlanEntity? TariffPlan { get; set; }

        /// <summary>Group that may be sold the plan. Removed from every plan when the group goes.</summary>
        public Guid UserGroupId { get; set; }
    }
}
