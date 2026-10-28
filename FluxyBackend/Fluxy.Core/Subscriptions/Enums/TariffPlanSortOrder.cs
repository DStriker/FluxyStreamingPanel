namespace Fluxy.Core.Subscriptions.Enums
{
    /// <summary>Which way the tariff plan table runs.</summary>
    public enum TariffPlanSortOrder
    {
        /// <summary>Smallest first: A before Z, cheaper before dearer, older before newer.</summary>
        Ascending,

        /// <summary>Largest first: Z before A, dearer before cheaper, newer before older.</summary>
        Descending
    }
}
