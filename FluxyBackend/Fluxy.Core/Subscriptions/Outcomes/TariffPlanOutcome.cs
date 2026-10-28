using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.Outcomes
{
    /// <summary>
    /// The outcome of one admin operation on a tariff plan: which
    /// <see cref="TariffPlanAction"/> ended it, which plan it was about, and what was refused
    /// when the input was the problem.
    /// </summary>
    /// <remarks>
    /// <see cref="Errors"/> is keyed by the C# name of the property the service rejected, the
    /// convention every other service here follows; the transport turns those into the camelCase
    /// names a form reads.
    /// </remarks>
    public sealed record TariffPlanOutcome
    {
        /// <summary>How the operation ended.</summary>
        public required TariffPlanAction Action { get; init; }

        /// <summary>
        /// Plan the operation was about. <see cref="Guid.Empty"/> for an outcome that never
        /// reached a row, such as a refusal made from the body alone.
        /// </summary>
        public Guid PlanId { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
