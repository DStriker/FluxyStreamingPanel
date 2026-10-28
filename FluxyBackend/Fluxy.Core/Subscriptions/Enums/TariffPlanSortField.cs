namespace Fluxy.Core.Subscriptions.Enums
{
    /// <summary>
    /// Which column of the tariff plan table a caller asked to order by.
    /// </summary>
    /// <remarks>
    /// Every member is a stored column the query can honestly sort on. The identifier is
    /// deliberately absent - it is a random uuid, and an order on it is an order nothing asked
    /// for. The validity period is absent too: hours, days, months and years are not comparable
    /// without converting them, and a sort that silently converted them would be a claim about
    /// lengths this domain does not make (see <see cref="TariffPlanDurationUnit"/>).
    /// </remarks>
    public enum TariffPlanSortField
    {
        /// <summary>Display name, compared by the database collation.</summary>
        Name,

        /// <summary>Cost in credits.</summary>
        Cost,

        /// <summary>When the plan was created, newest or oldest first.</summary>
        CreatedAt
    }
}
