using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.API.Controllers.Support
{
    /// <summary>
    /// Turns the text of a tariff plan query parameter or body field into the member it names.
    /// </summary>
    /// <remarks>
    /// Matched member by member rather than with enum binding: binding takes a member name
    /// spelled in full and would refuse the conventional lower-case one, before the action could
    /// run at all. Comparison happens after lowering, so <c>Months</c> is the same request as
    /// <c>months</c>.
    /// </remarks>
    internal static class TariffPlanTextParser
    {
        /// <summary>Turns text into the plan column it names, or null when it names none.</summary>
        /// <param name="value">Value from a query string.</param>
        public static TariffPlanSortField? ParseSortField(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "name" => TariffPlanSortField.Name,
                "cost" => TariffPlanSortField.Cost,
                "createdat" => TariffPlanSortField.CreatedAt,
                _ => null
            };

        /// <summary>Turns <c>asc</c> / <c>desc</c> into the plan table's order.</summary>
        /// <param name="value">Value from a query string.</param>
        public static TariffPlanSortOrder? ParseSortOrder(string? value)
            => SortOrderParser.Parse(
                value,
                TariffPlanSortOrder.Ascending,
                TariffPlanSortOrder.Descending);

        /// <summary>
        /// Turns text into the validity unit it names, or null when it names none.
        /// </summary>
        /// <param name="value">Value from a body or a query string.</param>
        public static TariffPlanDurationUnit? ParseDurationUnit(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "hours" => TariffPlanDurationUnit.Hours,
                "days" => TariffPlanDurationUnit.Days,
                "months" => TariffPlanDurationUnit.Months,
                "years" => TariffPlanDurationUnit.Years,
                _ => null
            };

        /// <summary>
        /// The text a client reads this unit by. The inverse of
        /// <see cref="ParseDurationUnit"/>, so the two cannot spell a unit differently.
        /// </summary>
        /// <param name="unit">Unit to name.</param>
        public static string NameOf(TariffPlanDurationUnit unit) => unit switch
        {
            TariffPlanDurationUnit.Hours => "hours",
            TariffPlanDurationUnit.Days => "days",
            TariffPlanDurationUnit.Months => "months",
            TariffPlanDurationUnit.Years => "years",
            _ => throw new ArgumentOutOfRangeException(
                nameof(unit),
                unit,
                "Unknown tariff plan duration unit.")
        };
    }
}
