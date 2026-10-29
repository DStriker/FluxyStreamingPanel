using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.Rows
{
    /// <summary>
    /// The tariff plan as one subscription save needs it: the knobs it owns, the groups it may
    /// be sold to, the bouquets it delivers and its length.
    /// </summary>
    /// <remarks>
    /// A projection shaped for one query, and nothing else - it never reaches a response. One
    /// read answers every question a create or a change asks of a plan: whether it exists,
    /// whether the owner may have it, what it derives and how long it runs for. Asking through
    /// three separate repository methods would be three round trips to answer one question.
    /// </remarks>
    public sealed record SubscriptionPlanContext
    {
        /// <summary>Identifier of the plan.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name, for the refusals and the detail's label.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the plan is a trial.</summary>
        public required bool IsTrial { get; init; }

        /// <summary>How much of <see cref="DurationUnit"/> the plan lasts for.</summary>
        public required int DurationValue { get; init; }

        /// <summary>The unit the plan's length is counted in.</summary>
        public required TariffPlanDurationUnit DurationUnit { get; init; }

        /// <summary>Whether the plan delivers a playlist.</summary>
        public required bool SupportsM3u { get; init; }

        /// <summary>Whether the plan delivers to a MAG device.</summary>
        public required bool SupportsMag { get; init; }

        /// <summary>Whether the plan delivers to an Enigma2 device.</summary>
        public required bool SupportsEnigma2 { get; init; }

        /// <summary>Whether the plan's MAG identifiers are checked strictly.</summary>
        public required bool StrictStbDeviceId { get; init; }

        /// <summary>Whether the plan allows restreaming.</summary>
        public required bool AllowRestream { get; init; }

        /// <summary>The plan's connection ceiling. Always set; a plan cannot allow none.</summary>
        public required int MaxConnections { get; init; }

        /// <summary>Whether the plan allows connections from more than one country.</summary>
        public required bool AllowMultipleCountries { get; init; }

        /// <summary>Whether the plan delivers MPEG-TS.</summary>
        public required bool OutputMpegTs { get; init; }

        /// <summary>Whether the plan delivers HLS.</summary>
        public required bool OutputHls { get; init; }

        /// <summary>Every group that may be sold the plan.</summary>
        public required IReadOnlyList<Guid> UserGroupIds { get; init; }

        /// <summary>Every bouquet the plan delivers, in the bouquets' own order.</summary>
        public required IReadOnlyList<Guid> BouquetIds { get; init; }
    }
}
